using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 回忆 / Recollection - 1(0) 费 - 能力 - 稀有
/// 在你的回合开始时，将你上个回合打出的最后 2 张非[gold]消耗[/gold]的牌加入你的手牌。
///
/// <para>
/// ⚠️⚠️ <b>2026-10-10 口径修改：弹回张数固定为 2，升级改为降费。</b>
///
/// <list type="bullet">
///   <item><b>旧</b>：卡面 <c>最后1（2）张</c>，升级把能力层数 1 → 2
///         （张数随层数变）</item>
///   <item><b>新</b>：卡面 <c>最后2张</c>，<b>升级与不升级都是 2 张</b>；
///         升级改为<b>耗能 1 → 0</b></item>
/// </list>
///
/// 所以本卡现在是"施加**固定 2 层**的 <see cref="RecollectionPower"/>，
/// 升级只降费"。层数不再承担升级语义 ——
/// 它只是"弹回几张"这个固定数值的载体。
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
    private const int Cost = 1;

    /// <summary>
    /// 弹回张数 —— **固定 2**，不随升级变化（升级只降费）。
    ///
    /// <para>
    /// 用 <c>PowerVar</c> 而不是裸常量，是为了让卡面文案能用
    /// <c>{RecollectionPower:diff()}</c> 占位（显示"2张"），
    /// 且能力图标上的数字与实际行为一致。
    /// </para>
    /// </summary>
    private const int CardsBack = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RecollectionPower>(CardsBack),
    ];

    public Recollection()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
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
        EnergyCost.UpgradeBy(-1);
    }
}
