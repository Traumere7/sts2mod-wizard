using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 焚尽 - X 费 - 技能 - 罕见
/// 给予全体敌人 X(X+1) 层燃烧。获得 X(X+1) 层法术增益。消耗。
///
/// <para>
/// <b>X 费照抄原版「串刺」<c>Skewer</c>（X 费攻击）与「挽歌」<c>Dirge</c>（X 费技能）：</b>
/// <list type="bullet">
///   <item><c>protected override bool HasEnergyCostX =&gt; true;</c></item>
///   <item>构造函数里的 baseCost 传 <b>0</b>（X 费卡的"基数"，不是实际费用）</item>
///   <item><c>OnPlay</c> 里用 <c>ResolveEnergyXValue()</c> 取实际 X</item>
/// </list>
/// <c>ResolveEnergyXValue()</c> 已经过 <c>Hook.ModifyXValue</c>，
/// 因此「化学X」<c>ChemicalX</c> 这类"X 值 +N"的遗物会自动生效
/// （见 <c>CardModel.cs:1113-1124</c>）—— <b>不要自己读能量，那样会漏掉 X 修正</b>。
/// </para>
///
/// <para>
/// <b>升级是"X+1"，且用 <c>{IfUpgraded}</c> 表达</b>，照抄原版「萎靡」<c>Malaise</c>：
/// <code>
/// int powerAmount = ResolveEnergyXValue();
/// if (base.IsUpgraded) { powerAmount++; }
/// </code>
/// 之所以不把 X 写进 <c>DynamicVars</c> 让它自动升级：X 不是定值，
/// 它在<b>打出的那一刻</b>才由能量决定，声明成 DynamicVar 反而要额外同步。
/// 本地化文案对应写 <c>X{IfUpgraded:show:+1}</c>。
/// </para>
///
/// <para>
/// <b>燃烧走 <see cref="SpellCmd.ApplyBurnToAll"/></b>（不是直接 <c>PowerCmd.Apply</c>）——
/// 本 mod 的燃烧有"不可叠加、取最高值"的特殊语义，
/// 且需要无条件触发"给予燃烧时"的效果（「纵火狂」<see cref="PyromaniacPower"/>、
/// 「全知形态」<see cref="OmniscientFormPower"/>），这些全封在
/// <see cref="SpellCmd.ApplyBurn"/> 里，不能绕过。
/// </para>
///
/// <para>
/// <b>法术增益加给自己</b>（<c>Owner.Creature</c>），不是敌人 ——
/// 设计写的是"获得"，与「镇静」<see cref="CalmMind"/> 同一个施加对象。
/// </para>
///
/// <para>
/// <b>结算顺序：先给敌人上燃烧、再给自己法术增益。</b>
/// 两者互不影响（燃烧伤害不吃法术增益，法术增益也不吃燃烧），
/// 但保持"先作用于敌方、后作用于己方"的读感顺序。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class BurnToAshes : WizardCardModel
{
    /// <summary>X 费卡：baseCost 传 0，真实费用在打出时由 X 决定。</summary>
    private const int Cost = 0;

    private const string BurnKey = nameof(BurnPower);
    private const string SpellPowerKey = nameof(SpellPowerPower);

    /// <summary>「消耗」是初始就有的词条（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    /// <summary>
    /// 本牌是 X 费。声明这个之后引擎才会把费用显示成 X，
    /// 并让 <c>ResolveEnergyXValue()</c> 可用（照抄「串刺」「挽歌」）。
    /// </summary>
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 卡面上的 X 无法用 DynamicVar 表达（它是打出时才定的），
        // 这里两个变量只作为"每 1 点 X 的换算基数"存在，恒为 1。
        new PowerVar<BurnPower>(1m),
        new PowerVar<SpellPowerPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public BurnToAshes()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // ResolveEnergyXValue 已含 Hook.ModifyXValue（「化学X」等 X 修正遗物会生效）。
        int x = ResolveEnergyXValue();

        // 升级：X → X+1（照抄原版「萎靡」Malaise 的写法）。
        if (IsUpgraded)
        {
            x++;
        }

        if (x <= 0)
        {
            return;
        }

        // ① 先给全体敌人上燃烧（走本 mod 的统一收口，保证"取高值"与事件触发）。
        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        await SpellCmd.ApplyBurnToAll(
            choiceContext,
            enemies,
            x,
            Owner.Creature,
            this);

        // ② 再给自己法术增益。
        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            Owner.Creature,
            x,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 升级是"X+1"，由 <see cref="OnPlay"/> 里的 <c>IsUpgraded</c> 分支处理，
    /// 没有可升级的 DynamicVar —— 与「萎靡」<c>Malaise</c> 一样留空。
    /// </summary>
    protected override void OnUpgrade()
    {
    }
}
