using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 清净世界 - 2 费 - 攻击 - 稀有
/// 虚无。对所有敌人造成 17(23) 点寒冰伤害。
/// 给予所有敌人 2 层虚弱、2 层冻结。
///
/// <para>
/// <b>群体伤害 + 群体负面</b>，是本 mod 寒冰系的重手段：
/// 伤害用 <c>TargetingAllOpponents(combatState)</c>（照抄「降雷」「电磁波」），
/// 负面效果逐个敌人 <c>PowerCmd.Apply</c>（照抄「屏蔽」<see cref="Screen"/>）。
/// </para>
///
/// <para>
/// <b>结算顺序：先伤害、再上负面。</b>
/// 若先上冻结/虚弱，本次伤害就会吃到自己刚施加的效果，强度与设计不符 ——
/// 与「落日」<see cref="Sunset"/>「先伤害后上燃烧」同一条原则。
/// </para>
///
/// <para>
/// <b>「虚无」<c>Ethereal</c> 是初始就有的词条</b>，直接进
/// <see cref="CanonicalKeywords"/>（同「闪爆」<see cref="Flashburst"/>、
/// 「落日」<see cref="Sunset"/>）。回合结束时若还在手里，引擎会自动消耗掉。
/// </para>
///
/// <para>
/// <b>为什么用「虚弱」而不是本 mod 自己的负面</b>：
/// 「虚弱」是原版减益（目标造成的伤害 ×0.75），与「冻结」（锁意图）配合，
/// 一个削弱输出、一个锁住行为，正好覆盖"清净世界"的压制语义。
/// 设计文档明确写的就是"虚弱"。
/// </para>
///
/// <para>
/// <b>⚠️ 升级只涨伤害（17 → 23）</b>，虚弱与冻结<b>恒为 2 层、不再提升</b> ——
/// 这是 2026-09-25 的设计改动。当前文档写的是
/// "造成 17（23）点寒冰伤害。给予所有敌人 2 层虚弱、2 层冻结"，
/// 两个负面的数字<b>没有括号</b>，所以 <c>OnUpgrade</c> 里<b>不要</b>再给它们
/// <c>UpgradeValueBy</c>。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ClearWorld : WizardSpellCard
{
    private const int Cost = 2;

    /// <summary>虚弱层数：升级不提升（设计如此）。</summary>
    private const decimal WeakStacks = 2m;

    /// <summary>冻结层数：升级不提升（设计如此）。</summary>
    private const decimal FreezeStacks = 2m;

    private const string WeakKey = nameof(WeakPower);
    private const string FreezeKey = nameof(FreezePower);

    public override SpellSchool School => SpellSchool.Ice;

    /// <summary>「虚无」是初始就有的词条（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(17m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<WeakPower>(WeakStacks),
        new PowerVar<FreezePower>(FreezeStacks),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<FreezePower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
    ];

    public ClearWorld()
        : base(Cost, CardType.Attack, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        if (enemies.Count == 0)
        {
            return;
        }

        // ① 先造成群体寒冰伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        // ② 再给每个敌人上虚弱与冻结。
        decimal weak = DynamicVars[WeakKey].BaseValue;
        decimal freeze = DynamicVars[FreezeKey].BaseValue;

        foreach (Creature enemy in enemies)
        {
            // 上一段伤害可能已击杀部分敌人，逐个复查。
            if (!enemy.IsAlive)
            {
                continue;
            }

            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                enemy,
                weak,
                Owner.Creature,
                this);

            await PowerCmd.Apply<FreezePower>(
                choiceContext,
                enemy,
                freeze,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        // 17 → 23。
        // ⚠️ 虚弱/冻结**刻意不升级**（2026-09-25 设计改动）——
        //    设计文档里这两个数字没有括号，加了 UpgradeValueBy 就是错的。
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
