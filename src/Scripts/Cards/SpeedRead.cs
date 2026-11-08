using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using System.Linq;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 速读 - 0 费 - 技能 - 罕见
/// 抽 1 张牌。如果你的手牌中存在[保留]牌，则获得 1 费。升级后获得「固有」。
///
/// <para>
/// <b>技术口径（设计文档明确）：先抽牌，再判定当前手牌</b> ——
/// 所以刚抽上来的那张如果带「保留」，也能让本牌给费。
/// </para>
///
/// <para>
/// ⚠️ <b>「保留」的判定用 <c>card.Keywords</c>，不能用 <c>CanonicalKeywords</c></b>：
/// 后者只包含"卡面固有"的词条，而「保留」还可能来自升级
/// （例如「镜像」「燃烧之魂」的升级效果）或其他来源；
/// <c>Keywords</c> 返回的是**合并后**的当前词条集合（<c>GetKeywordsWithSources(All)</c>）。
/// </para>
///
/// <para>
/// 本牌没有可升级的数值（设计文档的「0」没有括号），空实现。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class SpeedRead : WizardCardModel
{
    private const int Cost = 0;

    /// <summary>抽牌数（固定 1，设计未给升级项）。</summary>
    private const int DrawCount = 1;

    /// <summary>手牌里有保留牌时给的能量（固定 1）。</summary>
    private const decimal EnergyGain = 1m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    /// <summary>
    /// 手上**当前**就有「保留」牌时金边亮起。
    ///
    /// <para>
    /// ⚠️ 结算口径是"先抽 1 张、再判定"，所以金边只是"打出前就已满足"的提示
    /// —— 即使此刻没有保留牌、但抽上来的那张是保留牌，打出后仍会给费（只是不预先亮）。
    /// 这与用户的要求一致（"如果打出之前手中已有保留牌，则它也会亮起"）。
    /// </para>
    /// </summary>
    protected override bool ShouldGlowGoldInternal
        => CombatState != null
           && PileType.Hand.GetPile(Owner).Cards.Any(c => c.Keywords.Contains(CardKeyword.Retain));

    public SpeedRead()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先抽牌。
        await CardPileCmd.Draw(choiceContext, DrawCount, Owner);

        // ② 再判定"当前手牌里有没有保留牌"（含刚抽上来的那张）。
        bool hasRetain = PileType.Hand
            .GetPile(Owner)
            .Cards
            .Any(c => c.Keywords.Contains(CardKeyword.Retain));

        if (hasRetain)
        {
            await PlayerCmd.GainEnergy(EnergyGain, Owner);
        }
    }

    /// <summary>升级后获得「固有」（升级前没有该词条，也不在文案里写）。</summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
