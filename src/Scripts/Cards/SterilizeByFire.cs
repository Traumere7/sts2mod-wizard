using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 高温消毒 - 0 费 - 技能 - 罕见
/// 移除自身所有负面效果。给予自己 1 层燃烧。消耗。（升级移除消耗）
///
/// <para>
/// <b>「消耗」+ 升级移除：完全照抄原版「蜃景」<c>Mirage</c>。</b>
/// <list type="bullet">
///   <item><c>CanonicalKeywords</c> 声明 <c>CardKeyword.Exhaust</c>（初始就有）</item>
///   <item><c>OnUpgrade</c> 调 <c>RemoveKeyword(CardKeyword.Exhaust)</c></item>
/// </list>
/// <b>本地化文案里不要写"升级移除消耗"</b> —— 关键字由引擎渲染成卡面词条，
/// 升级后标签自动消失。（升级**添加**词条同理，见原版「独白」<c>Monologue</c>
/// 的 <c>AddKeyword(CardKeyword.Retain)</c>。）
/// </para>
///
/// <para>
/// ⚠️ 曾自行用 <c>_upgraded</c> 布尔标志 + <c>CanonicalKeywords</c> 返回不同集合来实现，
/// 行为虽然也对，但偏离了原版惯例，还逼得我额外往描述里手写升级提示 ——
/// 那才是多余且容易写错的。用 <c>RemoveKeyword</c> 两者都不需要。
/// </para>
///
/// <para>
/// <b>"移除自身所有负面效果"的实现细节：</b>
/// 遍历 <c>Owner.Creature.Powers</c> 找出 <c>Type == PowerType.Debuff</c> 的，
/// 逐个调 <c>PowerCmd.Remove</c>。<b>必须先 <c>ToList()</c> 再删</b> ——
/// <c>Remove</c> 会改动底层集合，边遍历边删会抛
/// <c>InvalidOperationException</c>（集合已修改）。
/// </para>
///
/// <para>
/// <b>燃烧也会被清掉，然后自己重新上 1 层 —— 这是设计如此。</b>
/// 顺序很重要：先清负面、再上燃烧，否则刚上的燃烧会被自己清掉。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class SterilizeByFire : WizardCardModel
{
    private const int Cost = 0;

    private const string BurnKey = nameof(BurnPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BurnPower>(1m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public SterilizeByFire()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;
        if (self.CombatState == null)
        {
            return;
        }

        // 先快照再删：Remove 会改底层集合。
        List<PowerModel> debuffs = self.Powers
            .Where(p => p.Type == PowerType.Debuff)
            .ToList();

        foreach (PowerModel debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }

        // 清完才上燃烧，否则会被上面的循环一并清掉。
        await SpellCmd.ApplyBurn(choiceContext, self, DynamicVars[BurnKey].BaseValue, self, this);
    }

    /// <summary>升级移除「消耗」（照抄原版「蜃景」<c>Mirage</c>）。</summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
