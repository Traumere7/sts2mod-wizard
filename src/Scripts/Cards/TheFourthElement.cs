using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 第四元素 - 1 费 - 能力 - 稀有
/// 在你的回合开始时，将 1 张「以太箭」加入你的手牌。升级后获得「固有」。
///
/// <para>
/// 本身没有数值可升级（设计文档只给了「固有」与固定 1 张），
/// 所以 <see cref="OnUpgrade"/> 是空实现 —— 与「失忆」同一条处理方式。
/// </para>
///
/// <para>
/// ⚠️ <b>不是法术牌</b>：本牌不造成伤害，用 <see cref="WizardCardModel"/> 而不是
/// <c>WizardSpellCard</c>（后者会自动被"法术增益/寒冷/超导"等机制当成法术）。
/// 真正需要"以太 = 三系"判定的是它生成的「以太箭」。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class TheFourthElement : WizardCardModel
{
    private const int Cost = 1;

    private const string PowerKey = nameof(TheFourthElementPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 层数 = 每回合生成几张「以太箭」；目前设计固定 1 张。
        new PowerVar<TheFourthElementPower>(1m),
    ];

    public TheFourthElement()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TheFourthElementPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[PowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级后获得「固有」（升级前没有该词条，也不在文案里写）。</summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
