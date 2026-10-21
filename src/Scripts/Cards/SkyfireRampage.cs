using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 天火浩荡 - 4 费 - 技能 - 稀有
/// 将所有敌人的燃烧<b>增加</b> 8（10）层。立刻触发一次燃烧结算。消耗。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 「增加」不等于「给予」—— 不能用 <see cref="SpellCmd.ApplyBurnToAll"/>
/// ══════════════════════════════════════════════════════════════════
/// 本 mod 的燃烧是"<b>取高值、不叠加</b>"的（见 <see cref="BurnPower"/>）：
/// <c>ApplyBurn</c> 的语义是"把层数<b>设为</b> max(现值, 请求值)"。
/// 若这里调它，则目标已有 15 层燃烧时施加 10 层 ⇒ 仍是 15 层 ⇒ <b>根本没"增加"</b>，
/// 与设计文案「增加 10 层」直接冲突。
///
/// 所以本牌走 <c>ApplyAbsolute(现值 + 增量)</c> —— 把"加算"在调用方算好，
/// 再交给那个"取高值"的收口。因为 <c>现值 + 增量 &gt; 现值</c> 恒成立，
/// 取高值的结果<b>必然</b>是"真的加了 10 层"。
/// </para>
///
/// <para>
/// <b>为什么仍然走 <see cref="BurnPower.ApplyAbsolute"/></b>（而不是直接
/// <c>PowerCmd.Apply</c> 或自己 <c>SetAmount</c>）：
/// <list type="bullet">
///   <item>它内部有 <c>ApplyLock</c>，"读现状 → 算差值 → 写入"是原子的，
///         不会在多次施加时把层数压回低值；</item>
///   <item>它返回 <see cref="BurnApplicationResult"/>，能被
///         <c>WizardEvents.BurnApplied</c> 用于"是否算一次给予燃烧"的判断
///         （「纵火狂」「巨焱拳」「全知形态」依赖这条）。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>本牌会触发「纵火狂」等"每当给予燃烧时"的效果 10 次吗？</b>
/// 不会 —— 只触发 <b>1 次</b>。因为这里是<b>一次</b> <c>ApplyAbsolute(现值+10)</c>，
/// 而 <c>SpellCmd.ApplyBurn</c> 才负责在施加后发<b>一次</b>
/// <c>WizardEvents.BurnApplied</c>。本牌自己发事件（见 <see cref="OnPlay"/>），
/// 所以口径与"用一张牌给敌人上燃烧"完全一致：<b>一个目标一次</b>。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「立刻触发一次燃烧结算」照抄原版「毒性爆发」<c>Outbreak</c>
/// ══════════════════════════════════════════════════════════════════
/// 原版实现（<c>Outbreak.cs</c>，3 费稀有技能，效果是"给全体上毒 + 立刻触发一次中毒"）：
/// <code>
/// // 第一趟：先给全体上满
/// foreach (Creature e in CombatState.HittableEnemies)
///     await PowerCmd.Apply&lt;PoisonPower&gt;(choiceContext, e, amount, Owner.Creature, this);
///
/// // 第二趟：再逐个立刻结算
/// foreach (Creature e in CombatState.HittableEnemies)
///     e.GetPower&lt;PoisonPower&gt;()?.Trigger();
/// </code>
/// <b>两趟分开</b>是必要的：必须先让所有敌人都拿到层数，再统一结算；
/// 若在同一个循环里"上一个、立刻烧一个"，视觉与结算顺序都会乱。
///
/// 本牌同构，只是把 <c>PoisonPower.Trigger()</c> 换成
/// <see cref="BurnPower.Trigger"/>（我为这个需求专门从
/// <c>AfterSideTurnStart</c> 里抽出来的，结构与 <c>PoisonPower</c> 一致）。
/// </para>
///
/// <para>
/// ⚠️ <b>结算发生在层数增加<b>之后</b></b>：所以这次立刻结算烧的是
/// "加完 10 层后"的层数 —— 设计文案「增加 10 层。立刻触发一次」的语序就是这个意思。
/// </para>
///
/// <para>
/// ⚠️ <b>用 <c>CombatState.HittableEnemies</c></b>（照抄 Outbreak）而不是
/// <c>AllEnemies</c>：前者排除已死/不可命中的目标，
/// 避免给尸体上燃烧、或对尸体结算。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class SkyfireRampage : WizardSpellCard
{
    /// <summary>4 费。</summary>
    private const int Cost = 4;

    /// <summary>基础增加层数（升级前）。</summary>
    private const int BurnToAdd = 8;

    /// <summary>升级后额外增加（8 → 10）。</summary>
    private const int UpgradeAdd = 2;

    /// <summary>
    /// 变量名用 <c>nameof(BurnPower)</c>（= "BurnPower"），
    /// 与本 mod 其它燃烧牌（「焚尽」「焚尽成灰」「决心」）一致 ——
    /// 卡面文案写 <c>{BurnPower:diff()}</c> 即可自动跟随升级变化。
    /// </summary>
    private const string BurnKey = nameof(BurnPower);

    /// <summary>火系法术。</summary>
    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>「消耗」是初始就有的词条（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BurnPower>(BurnToAdd),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public SkyfireRampage()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        decimal add = DynamicVars[BurnKey].BaseValue;

        // ── 第一趟：给所有敌人「增加」层数（现值 + 增量，不是"设为"）──
        //
        // 先快照目标列表：ApplyAbsolute 内部有 await，
        // 直接 foreach 活动集合会在施加过程中被改动的风险（原版 Outbreak 也是先取 HittableEnemies 再遍历）。
        List<Creature> targets = combatState.HittableEnemies.ToList();

        foreach (Creature enemy in targets)
        {
            int current = enemy.GetPowerAmount<BurnPower>();
            int desired = current + (int)add;

            BurnApplicationResult result = await BurnPower.ApplyAbsolute(
                choiceContext,
                enemy,
                desired,
                Owner.Creature,
                this);

            // ⚠️ 手动补发"给予燃烧"事件 —— 我们<b>绕过</b>了
            // <see cref="SpellCmd.ApplyBurn"/>（因为需要加算而非取高值），
            // 而「纵火狂」「巨焱拳的燃烧计数」「全知形态」全都订阅这个事件，
            // 不发它们就完全收不到本牌。
            //
            // ⚠️ 只在真的落上时计入 —— 与 ApplyBurn 的口径一致（见 BurnApplicationResult）。
            if (result.Landed)
            {
                await WizardEvents.RaiseBurnApplied(enemy, Owner.Creature, add, landed: true);
            }
        }

        // ── 第二趟：再逐个立刻结算一次（照抄 Outbreak 的两趟结构）──
        foreach (Creature enemy in targets)
        {
            BurnPower? burn = enemy.GetPower<BurnPower>();
            if (burn != null)
            {
                await burn.Trigger();
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[BurnKey].UpgradeValueBy(UpgradeAdd);
    }
}
