using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 火球术 - 1 费 - 攻击 - 初始卡组
/// 造成 4(5) 点火焰伤害 2 次，给予 2 层燃烧。
///
/// <para>
/// <b>欧洛巴斯「古老牙齿」<c>ArchaicTooth</c> 会把它换成「落日」<see cref="Sunset"/>。</b>
/// 实现方式见类声明上的 <see cref="ITranscendenceCard"/>。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Fireball : WizardSpellCard, ITranscendenceCard
{
    private const int Cost = 1;
    private const int Hits = 2;

    private const string BurnKey = nameof(BurnPower);

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>
    /// 「古老牙齿」<c>ArchaicTooth</c> 的替换目标：本牌 → 「落日」。
    ///
    /// <para>
    /// <b>不需要写 Harmony。</b> BaseLib 已有
    /// <c>ArchaicToothTranscendenceUpgradesPatch</c> —— 它对原版那张写死的
    /// <c>TranscendenceUpgrades</c> 静态表加了 <c>[HarmonyPostfix]</c>，
    /// 扫描 <c>ModelDb.AllCards</c> 里所有实现了 <see cref="ITranscendenceCard"/>
    /// 的牌，把 <c>本牌 → GetTranscendenceTransformedCard()</c> 并进表里。
    /// 所以 mod 侧只要实现这个接口即可。
    /// </para>
    ///
    /// <para>
    /// <b>连带效果：</b>原版 <c>DustyTome</c>（达弗「尘封魔典」）的筛选条件是
    /// <c>Rarity == Ancient &amp;&amp; !ArchaicTooth.TranscendenceCards.Contains(c)</c> ——
    /// 「落日」一旦进了这张表，就会被「尘封魔典」的随机池**自动排除**，
    /// 正是设计要的（魔典只该给「远古残章」）。
    /// </para>
    /// </summary>
    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<Sunset>();

    /// <summary>悬停时展示相关机制说明。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<BurnPower>(2m),
    ];

    public Fireball()
        : base(Cost, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(Hits)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        await SpellCmd.ApplyBurn(
            choiceContext,
            cardPlay.Target,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
