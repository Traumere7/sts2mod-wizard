using System.Linq;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 幻化 / Transmogrify - 1 费 - 攻击 - 普通
/// 造成 8(11) 点雷电伤害。选择 1 张手牌变化。
///
/// <para>
/// <b>「变化」照抄原版「熵」<c>EntropyPower</c></b>
/// （<c>EntropyPower.cs:24-38</c>）：
/// 先 <c>CardSelectCmd.FromHand</c> 选牌，再对每张
/// <c>CardCmd.TransformToRandom(card, player.RunState.Rng.CombatCardSelection)</c>。
/// 唯一差别是熵由能力在<b>每回合开始</b>触发，本牌是<b>打出时</b>触发。
/// </para>
///
/// <para>
/// ⚠️ <b>RNG 必须用 <c>RunState.Rng.CombatCardSelection</c></b>（战斗内选牌随机源），
/// 与原版一致；不要用 <c>CombatCardGeneration</c> 或 <c>Shared</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>筛选条件用 <c>IsTransformable</c></b>：<c>CardCmd.Transform</c>
/// （<c>CardCmd.cs:374-409</c>）在
/// <c>!original.IsTransformable</c> 时会<b>抛异常</b>
/// （"Can't transform … because it's un-transformable."），
/// 所以选牌阶段就要把不可变化的牌排除掉。
/// </para>
///
/// <para>
/// <b>伤害走雷电法术</b>（<c>ValueProp.Move | SpellProps.For(School)</c>）：
/// 吃「法术增益」与「寒冷」加成，不吃力量 —— 与本 mod 其它雷电法术一致。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Transmogrify : WizardSpellCard
{
    private const int Cost = 1;

    private const int Damage = 8;

    private const int DamageUpgrade = 3;

    /// <summary>选择几张手牌变化。</summary>
    private const int TransformCount = 1;

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Transform),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move | SpellProps.For(School)),
    ];

    public Transmogrify()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 雷电伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        // ② 选 1 张手牌变化（照抄 EntropyPower 的 FromHand + TransformToRandom）。
        List<CardModel> chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, TransformCount),
            filter: c => c.IsTransformable,
            this)).ToList();

        foreach (CardModel card in chosen)
        {
            await CardCmd.TransformToRandom(card, Owner.RunState.Rng.CombatCardSelection);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(DamageUpgrade);
    }
}
