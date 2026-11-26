using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 回忆 / Recollection - 0 费 - 能力 - 稀有
/// 在你的回合开始时，将你上个回合打出的最后 1(2) 张非[gold]消耗[/gold]的牌加入你的手牌。
///
/// <para>
/// ⚠️⚠️ <b>2026-11-27 design 改版：费用 1 → 0，张数改为 1(2)（升级加张数）。</b>
///
/// <list type="bullet">
///   <item><b>旧</b>：0 费 + 张数随层数 1(2)（2026-10-10 曾改成 1(0) 费 + 固定 2 张）</item>
///   <item><b>新</b>：<b>0 费</b>；施加 1 层的 <see cref="RecollectionPower"/>，
///         升级把层数 1 → 2（卡面 <c>最后1（2）张</c>）</item>
/// </list>
///
/// 层数重新承担"弹回几张"的升级语义。
/// </para>
///
/// <para>
/// <b>这是能力牌</b>（<c>CardType.Power</c>），对商店卡位生成有正面作用
/// （详见 <c>Superconduct</c> 的注释）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Recollection : WizardCardModel
{
    private const int Cost = 0;

    /// <summary>
    /// 弹回张数的基础值 —— **1**，升级 +1 → 2。
    ///
    /// <para>
    /// 用 <c>PowerVar</c> 而不是裸常量，是为了让卡面文案能用
    /// <c>{RecollectionPower:diff()}</c> 占位（显示"1(2) 张"），
    /// 且能力图标上的数字与实际行为一致。
    /// </para>
    /// </summary>
    private const int CardsBack = 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RecollectionPower>(CardsBack),
    ];

    /// <summary>
    /// 卡面**要**解释「消耗」（buff 侧不解释）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public Recollection()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<RecollectionPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(RecollectionPower)].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 升级 1 费 → 0 费。
    ///
    /// <para>
    /// ⚠️ <b>不要再升级 <c>RecollectionPower</c> 的层数</b> ——
    /// 张数是固定的 2，升级只降费（2026-10-10 口径）。
    /// </para>
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars[nameof(RecollectionPower)].UpgradeValueBy(1m);
    }
}
