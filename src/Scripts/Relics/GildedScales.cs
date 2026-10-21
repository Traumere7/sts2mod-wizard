using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 鎏金天平 / GildedScales - Common
/// 每当你获得[gold]力量[/gold]时，获得等量的[purple]法术增益[/purple]。
///
/// <para>
/// <b>照抄原版能力「夺力」<c>PossessStrengthPower</c>（读取层数）与遗物
/// 「锈蚀头盔」<c>RuinedHelmet</c>（遗物侧的"获得力量"判定）：</b>
/// <code>
/// if (power is not StrengthPower) return;      // PossessStrengthPower.cs:41
/// if (power.Owner != Owner.Creature) return;   // SwordSagePower.cs:34
/// if (amount &lt;= 0m) return;                    // RuinedHelmet.cs:43（判定"获得"）
/// </code>
/// </para>
///
/// <para>
/// <b>本牌无状态</b> —— 不需要标志、计数器或重置。
/// </para>
///
/// <para>
/// ⚠️ <b>会被 <c>TemporaryStrengthPower</c> 触发</b>：「临时力量」在
/// <c>BeforeApplied</c> 里施加真实力量、又在自身层数变化时再施加一次。
/// 但这里<b>刻意不排除</b>它 —— 与「铅咒板」不同，本遗物的语义是
/// "<b>你实际获得了多少力量</b>"，临时力量也是真的获得了力量，
/// 多给一份等量法术增益是符合设计的（原版 <c>RuinedHelmet</c> 同样不排除）。
/// </para>
///
/// <para>
/// ⚠️ <b>这个钩子在"增量为 0"时不会触发</b>（<c>PowerCmd.cs:247</c>）——
/// 对本遗物正好：没获得力量就不该给法术增益。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class GildedScales : ArcaneRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpellPowerPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只认力量（这条同时天然排除了对 SpellPowerPower 自身的重入）。
        if (power is not StrengthPower)
        {
            return;
        }

        // 只认"我身上的"（没有 target 参数，用 power.Owner）。
        if (power.Owner != Owner.Creature)
        {
            return;
        }

        // 只认"获得"（正数），失去力量不触发。
        if (amount <= 0m)
        {
            return;
        }

        Flash();

        // ⚠️ 必须传 cardSource：本遗物是"转赠"，把原来源一并带过去，
        //    让下游的"施加能力时"类效果能追溯到真正的来源。
        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            cardSource);
    }
}
