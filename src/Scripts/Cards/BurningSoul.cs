using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 燃烧之魂 - 1 费 - 技能 - 罕见
/// 这个回合，你的每一段攻击结算后都会插入一段等量的火焰伤害，并给予 1 层燃烧。
/// **升级后获得「保留」。**
///
/// <para>
/// <b>升级添加词条完全照抄原版「独白」<c>Monologue</c>：</b>
/// <list type="bullet">
///   <item>初始<b>不</b>在 <c>CanonicalKeywords</c> 里声明任何关键字</item>
///   <item><c>OnUpgrade</c> 调 <c>AddKeyword(CardKeyword.Retain)</c></item>
/// </list>
/// <b>本地化文案里不要写"（保留）"</b> —— 关键字由引擎渲染成卡面词条，
/// 升级后标签自动出现。（升级**移除**词条同理，见原版「蜃景」<c>Mirage</c>
/// 的 <c>RemoveKeyword(CardKeyword.Exhaust)</c>。）
/// </para>
///
/// <para>
/// ⚠️ 曾自行用 <c>_upgraded</c> 布尔标志 + <c>CanonicalKeywords</c> 返回不同集合来实现，
/// 行为虽然也对，但偏离了原版惯例，还逼得我额外往描述里手写升级提示 ——
/// 那才是多余且容易写错的。<c>AddKeyword</c> 两者都不需要。
/// </para>
///
/// <para>
/// 效果全部由 <see cref="BurningSoulPower"/> 承载，本卡只是施加它
/// （数量恒为 1，真正的语义是"本回合有效"而非层数）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class BurningSoul : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    public BurningSoul()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BurningSoulPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>升级获得「保留」（照抄原版「独白」<c>Monologue</c>）。</summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
