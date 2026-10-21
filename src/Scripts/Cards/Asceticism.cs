using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 修行 - 2(1) 费 - 能力 - 稀有
/// 本次战斗结束时额外获得 1 组卡牌奖励。
///
/// <para>
/// 实现照抄原版「狩猎」<c>TheHunt</c> 的奖励部分：
/// <c>combatRoom.AddExtraReward(player, new CardReward(...))</c>。
/// 但触发点不同 —— 狩猎是「击杀时立刻给」，本牌是「战斗结束时给」，
/// 所以真正的逻辑放在 <see cref="AsceticismPower.AfterCombatVictory"/> 里，
/// 本卡只负责施加能力（狩猎也是这个分工：效果在卡里，能力只做视觉指示）。
/// </para>
///
/// <para>
/// <b>升级只降费（2 → 1）</b>，所以 <c>CanonicalVars</c> 为空。
/// 这也和狩猎不同（狩猎升级加伤害）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Asceticism : WizardCardModel
{
    private const int Cost = 2;

    /// <summary>
    /// ⚠️ <b>不能被战斗内生成</b>（<c>CardModel.cs:643</c> 的虚属性，默认 <c>true</c>）。
    ///
    /// <para>
    /// <b>为什么</b>：本牌效果是"本次战斗结束时额外获得 1 组卡牌奖励"——
    /// 属于**局外收益**（战斗结束后才结算）。原版把所有"产生局外收益"的牌
    /// 都设成不可战斗内生成，例如：
    /// <list type="bullet">
    ///   <item>回复生命：<c>NotYet</c>（时候未到）</item>
    ///   <item>获得生命上限：<c>Feed</c>（狂宴）</item>
    ///   <item>获得药水：<c>Alchemize</c>（炼制药水）</item>
    ///   <item>获得金币：<c>HandOfGreed</c>（贪婪之手）、<c>Royalties</c>（王国资产）</item>
    ///   <item>获得卡牌奖励：<c>TheHunt</c>（狩猎）</item>
    /// </list>
    /// 否则「暗黑神秘学」这类"战斗内生成一张牌"的效果就能凭空造出收益 ——
    /// 等于在战斗里白嫖局外奖励。
    /// </para>
    ///
    /// <para>
    /// 该标记只影响 <c>CardFactory.FilterForCombat</c>（<c>CardFactory.cs:159-162</c>，
    /// 被 <c>GetDistinctForCombat</c> / <c>GetForCombat</c> 调用）
    /// 与"战斗内变形"（<c>:203</c>）—— <b>不影响</b>普通卡牌奖励、商店、牌库。
    /// </para>
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    public Asceticism()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Log.Info($"[Wizard/Asceticism] 打出卡牌，准备施加能力；CombatState={(Owner.Creature.CombatState != null)}");

        var applied = await PowerCmd.Apply<AsceticismPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        Log.Info($"[Wizard/Asceticism] 施加结果: applied={(applied != null)} " +
                 $"身上现有能力数={Owner.Creature.Powers.Count} " +
                 $"含 AsceticismPower={Owner.Creature.HasPower<AsceticismPower>()}");
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
