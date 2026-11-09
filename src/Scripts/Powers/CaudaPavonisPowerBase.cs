using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Utils;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 孔雀之尾（能力）的公共实现：**你每生成 1 张卡牌，就给予随机敌人
/// <see cref="LayersPerTrigger"/> 层随机负面效果 `Amount` 次**。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么拆成两个子类（用户 2026-11-09 定案）
/// ══════════════════════════════════════════════════════════════════
/// 未升级版每次给 2 层、升级版每次给 3 层。如果放在**同一个**能力里，
/// "先打未升级再打升级过的"就得决定一个混乱的合并规则（取大？取小？各算各的？）。
/// 用户的要求是：<b>2 层的是一个 buff 实例、3 层的是另一个 buff 实例，
/// 各自只和同类叠层</b> —— 于是拆成两个模型（两个 <c>ModelId</c>）：
/// <list type="bullet">
///   <item><see cref="CaudaPavonisPower"/>：2 层，未升级牌挂它；</item>
///   <item><see cref="CaudaPavonisUpgradedPower"/>：3 层，升级牌挂它。</item>
/// </list>
/// 两个都是 <c>PowerStackType.Counter</c>，所以"打两张未升级 ⇒ n=2"、
/// "打两张升级 ⇒ n=2"、"各打一张 ⇒ 两个图标各 n=1" —— 各自独立、语义干净。
/// 名称在本地化里都叫「孔雀之尾」。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 生成检测：照抄本 mod「催化」<c>CatalyzationPower</c>
/// ══════════════════════════════════════════════════════════════════
/// 订阅 <c>AfterCardGeneratedForCombat(card, creator)</c>，只认自己生成的牌
/// （<c>creator.Creature == Owner</c>）。走普通 <c>CardPileCmd.Add</c> 的加牌不算生成。
/// </para>
///
/// <para>
/// 钩子没有 <c>PlayerChoiceContext</c>，按原版「炮火」<c>ArsenalPower</c> 的先例
/// 用 <c>new ThrowingPlayerChoiceContext()</c>。
/// 随机：敌人用 <c>Rng.CombatTargets</c>，负面效果种类用 <c>Rng.Niche</c>。
/// </para>
/// </summary>
public abstract class CaudaPavonisPowerBase : CustomPowerModel
{
 
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WizardTips.RandomDebuff,
    ];
    /// <summary>"每次给几层负面效果"的变量名（能力文案写 <c>{Layers}</c>）。</summary>
    public const string LayersKey = "Layers";

    /// <summary>每次触发给几层负面效果（未升级 2 / 升级 3）。</summary>
    public abstract decimal LayersPerTrigger { get; }

    public override PowerType Type => PowerType.Buff;

    /// <summary>层数 = n（每次触发结算几次）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(LayersKey, LayersPerTrigger),
    ];

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只认"自己生成的牌"（与「催化」同一条口径）。
        if (creator == null || creator.Creature != Owner)
        {
            return;
        }

        Player? player = Owner.Player;
        ICombatState? combatState = Owner.CombatState;
        if (player == null || combatState == null)
        {
            return;
        }

        int times = (int)Amount;
        if (times <= 0)
        {
            return;
        }

        Flash();

        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();

        for (int i = 0; i < times; i++)
        {
            // 每次都重新取一遍存活敌人：中途可能被打死。
            List<Creature> enemies = combatState.HittableEnemies.Where(c => c.IsAlive).ToList();
            if (enemies.Count == 0)
            {
                return;
            }

            Creature? enemy = player.RunState.Rng.CombatTargets.NextItem(enemies);
            if (enemy == null)
            {
                return;
            }

            RandomDebuffKind kind = RandomDebuff.Pick(enemy, player.RunState.Rng.Niche);

            // source 传 null：这是能力触发，不属于任何一张牌的伤害/施加。
            await RandomDebuff.Apply(choiceContext, enemy, kind, LayersPerTrigger, Owner, null);
        }
    }
}
