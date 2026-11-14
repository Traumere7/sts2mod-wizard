using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 节制 - 0 费 - 技能 - 普通
/// 抽 2(3) 张牌。本回合你只能再打出 2 张牌。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 关键：**不叠加、不刷新**（用户 2026-11-14 明确，与原版「安可」不同）
/// ══════════════════════════════════════════════════════════════════
/// 原版「安可」<c>EncorePower</c> 是可叠加的（两层 = 再打两张）。
/// 节制刻意反过来：
/// <list type="bullet">
///   <item>打出时先抽牌，再<b>检查身上是否已经有「节制」</b>；</item>
///   <item>已经有 ⇒ <b>完全不施加</b> ⇒ 层数不增加、计数也不重置，
///         所以"打完节制之后真的只能再打 2 张牌"；</item>
///   <item>再打出的那张「节制」自己仍算"已打出的 1 张"（它也会走
///         <see cref="TemperancePower.AfterCardPlayed"/>），这与原版「安可」一致。</item>
/// </list>
/// 「节制」是<b>负面效果</b>（<c>PowerType.Debuff</c>），因此可以被
/// 「高温消毒」<see cref="SterilizeByFire"/> 这类"移除自身负面效果"的牌清掉 —— 这是设计留的出口。
/// </para>
///
/// <para><b>升级</b>：抽牌 2 → 3（"只能再打 2 张"不变）。</para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Temperance : WizardCardModel
{
    private const int Cost = 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 抽牌数：2（升级 3）
        new CardsVar(2),
    ];

    // ⚠️ 卡面**不**挂「节制」能力本身的解释（用户 2026-11-14：与卡名同名的 buff 不在卡面解释）。
    //    能力的效果由卡面文案 + 能力图标自身的悬停承担。

    public Temperance()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先抽牌（设计顺序：抽 2(3) 张牌 → 再限制本回合剩余出牌数）。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        // ② 已经有「节制」就不施加：不叠层数、不刷新计数器（见类注释）。
        if (Owner.Creature.HasPower<TemperancePower>())
        {
            Entry.Logger.Info("[节制] 身上已有「节制」，本次不施加（不叠层、不刷新计数）。");
            return;
        }

        await PowerCmd.Apply<TemperancePower>(
            choiceContext,
            Owner.Creature,
            TemperancePower.AllowedPlays,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 抽 2 → 3
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
