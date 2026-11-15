using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 伟大法袍 - 1 费 - 能力 - 罕见
/// 你每给予敌人 1 层[gold]负面效果[/gold]，就获得 1 点[gold]格挡[/gold]。
///
/// <para>
/// ⚠️ <b>"给低层数也算"</b>：目标已有高层燃烧时，再给它低层燃烧<b>仍然触发</b>，
/// 并按<b>给予的层数</b>给格挡（不是按层数增量）。判定细节见
/// <see cref="MagusRobePower"/> 的类注释 —— 它组合了引擎钩子与本 mod 的燃烧事件。
/// </para>
///
/// <para>
/// <b>无触发上限</b>：一张牌给多种负面就触发多次，每次按层数给。
/// </para>
///
/// <para><b>升级</b>：获得「固有」（不写进文案，引擎渲染词条）。</para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class MagusRobe : WizardCardModel
{
    private const int Cost = 1;

    private const string PowerKey = nameof(MagusRobePower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每层负面给几倍格挡：1（两张法袍叠加成 2）
        new PowerVar<MagusRobePower>(1m),
    ];

    /// <summary>
    /// 悬停：**只挂「格挡」**（卡面文案提到了[gold]格挡[/gold]，必须解释）。
    ///
    /// <para>
    /// ⚠️ 两条规矩（本项目统一口径）：
    /// <list type="bullet">
    ///   <item><b>不挂与卡名同名的能力</b> ⇒ 不挂 <see cref="MagusRobePower"/>；</item>
    ///   <item><b>文案里出现的词条都要解释</b>（唯一例外是「消耗」——原版规范不解释），
    ///         所以「格挡」必须挂。这张牌自己<b>不</b>获得格挡（是能力触发给格挡），
    ///         引擎不会通过 <c>GainsBlock</c> 自动补，必须像「铅咒板」一样手动挂
    ///         <c>StaticHoverTip.Block</c>。</item>
    /// </list>
    /// 悬停文本走原版 <c>static_hover_tips</c> 表，**四种语言自动跟随**，无需按语言分别处理。
    /// </para>
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    public MagusRobe()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<MagusRobePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[PowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级：获得「固有」（与「第四元素」等"升级只给词条"的牌同口径）。</summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
