using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 灵光 / Flicker - 0 费 - 普通 - 攻击
/// 造成 5(7) 点雷电伤害。你每生成 2 张卡牌，就把这张牌放入你的手牌。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 常驻"生成计数"（人不在手牌里也要能触发）
/// ══════════════════════════════════════════════════════════════════
/// 卡牌只要在**任一战内牌堆**里，就会被 <c>CombatState.IterateHookListeners()</c>
/// 列为钩子监听者（它会遍历 <c>PlayerCombatState.AllPiles</c> 里的每张牌），
/// 所以直接覆写 <see cref="AfterCardGeneratedForCombat"/> 即可，
/// 不需要额外挂一个能力载体（与「结晶」「永恒回响」读历史的思路不同 ——
/// 这里要的是"从此刻起计数"，所以用实例计数 + <see cref="AfterCreated"/> 归零）。
/// </para>
///
/// <para>
/// ⚠️ <b>生效范围 = 抽牌堆 / 弃牌堆</b>（用户 2026-11-26 明确）：
/// 在手里时不需要"放回"；在**消耗堆**里则不能被拉回（<c>PileType.Exhaust</c> 不处理）。
/// </para>
///
/// <para>
/// ⚠️ <b>只认"你生成的"</b>：<paramref name="creator"/> 必须是自己
/// （与「孔雀之尾」「催化」同一口径）；自己生成自己（<c>card == this</c>）不计。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Flicker : WizardSpellCard
{
    private const int Cost = 0;

    /// <summary>每生成几张牌把本牌拉回手牌。</summary>
    private const int GeneratedPerReturn = 2;

    /// <summary>自上次回到手牌后，自己生成的牌数（战斗内实例私有）。</summary>
    private int _generatedSinceReturn;

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move | SpellProps.For(School)),
    ];

    public Flicker()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    /// <summary>
    /// 战斗内克隆/新建时计数归零（<c>CreateClone</c> 会浅拷贝字段，
    /// 不归零会继承原型身上残留的计数）。
    /// </summary>
    public override void AfterCreated()
    {
        base.AfterCreated();
        _generatedSinceReturn = 0;
    }

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // ① 只认自己生成的牌；自己生成自己不算。
        if (creator != Owner || ReferenceEquals(card, this))
        {
            return;
        }

        // ② 只有躺在抽牌堆 / 弃牌堆里时才"拉回手牌"（消耗堆里不生效）。
        CardPile? pile = Pile;
        if (pile == null || pile.Type is not (PileType.Draw or PileType.Discard))
        {
            return;
        }

        // ③ 计数：每 2 张触发一次。
        _generatedSinceReturn++;
        if (_generatedSinceReturn < GeneratedPerReturn)
        {
            return;
        }

        _generatedSinceReturn -= GeneratedPerReturn;

        CardPileAddResult result = await CardPileCmd.Add(this, PileType.Hand);

        // ★ 飞牌演出 + 手牌/牌堆计数即时刷新（与「梦魇」同款）。
        CardCmd.PreviewCardPileAdd(result);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(WizardVfx.LightningOrbImpact)
            .Execute(choiceContext);
    }

    /// <summary>升级：伤害 5 → 7。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
