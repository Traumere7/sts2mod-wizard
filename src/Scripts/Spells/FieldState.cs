using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 「场上状态」的只读查询集。
///
/// <para>
/// 存在的理由：<b>「场上的燃烧层数」这个口径被多张牌共用</b>
/// （「落日」的每段加伤、「真理派对」的抽牌张数），
/// 而它的定义有个容易写错的地方 —— <b>必须包含玩家自己身上的燃烧</b>。
/// </para>
///
/// <para>
/// ⚠️ <b>非常容易踩的坑</b>：直觉上会写
/// <c>CombatState.GetOpponentsOf(Owner.Creature)</c> —— 那只是<b>敌人</b>。
/// 但本 mod 的「高温消毒」会给玩家自己上燃烧，所以玩家身上的层数也是"场上的燃烧"，
/// 漏掉就会在"自己烧得比敌人还狠"时算出偏低的数值。
/// 正确取法是 <see cref="ICombatState.Creatures"/>（双方全部生物）。
/// </para>
///
/// <para>
/// 另一个细节：倍率函数在<b>非战斗</b>场合（卡牌库预览、卡组浏览）也会被调用，
/// 那时读不到 <c>CombatState</c>，必须返回 0 让卡面显示基础值 ——
/// 这就是 <see cref="TryHighestBurn"/> 存在的意义。
/// </para>
/// </summary>
public static class FieldState
{
    /// <summary>
    /// <b>仅供卡面预览</b>用的倍率：手里显示基础值，拖到生物上才显示动态值。
    ///
    /// <para>
    /// 复刻原版「欺凌」<c>Bully</c> 的手感 —— 它在手里显示 4（基础值），
    /// 拖出来悬停到敌人上才显示 <c>4 + 2×易伤</c>。
    /// 原理是 <c>CalculatedVar.Calculate(target)</c>：手里时
    /// <c>target == null</c>（<c>NCard.cs:872</c> 取
    /// <c>_previewTarget ?? Model.CurrentTarget</c>，两者都为 null），
    /// 倍率返回 0 → 卡面 = <c>CalculationBase</c>。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>为什么不能直接拿它当结算值</b>：落日是 <c>TargetType.Self</c>，
    /// 打出时 <c>PlayCardAction</c> 传的 target 也是 null，
    /// 于是 <c>Calculate(null)</c> 恒为 0 → 实际伤害会退化成基础值。
    /// 所以<b>结算必须直接调 <see cref="HighestBurn"/></b>，
    /// 本方法只服务于卡面。
    /// </para>
    ///
    /// <para>
    /// <c>TargetType.Self</c> 卡拖拽时引擎不会调 <c>SetPreviewTarget</c>
    /// （<c>NCardPlay.ShowMultiCreatureTargetingVisuals</c> 只处理
    /// <c>AllEnemies</c>/<c>RandomEnemy</c>），但拖拽过程中鼠标划过任意生物节点
    /// 都会触发 <c>OnCreatureHover</c> → <c>SetPreviewTarget</c> → 刷新卡面，
    /// 所以"拖出来就看到动态值"仍然成立。
    /// </para>
    /// </summary>
    public static decimal HighestBurnForPreview(CardModel card, Creature? target)
    {
        // 没有预览目标 ⇒ 在手里 ⇒ 返回 0，卡面显示 CalculationBase。
        if (target == null)
        {
            return 0m;
        }

        return HighestBurn(card, target);
    }

    /// <summary>
    /// 取场上最高的燃烧层数（<b>双方所有存活生物</b>，含玩家自己）；
    /// 非战斗场合返回 0。
    ///
    /// <para>
    /// 返回 <see cref="decimal"/> 而不是 <c>int</c>：它是给
    /// <c>CalculatedVar.WithMultiplier</c> 当倍率函数用的，
    /// 那个委托的签名就是 <c>Func&lt;CardModel, Creature?, decimal&gt;</c>
    /// —— 返回 <c>int</c> 会编译不过（CS0407）。
    /// </para>
    ///
    /// <para>
    /// 供两处用途：
    /// <list type="bullet">
    ///   <item>卡面倍率函数：用 <see cref="HighestBurnForPreview"/></item>
    ///   <item>结算取值：<c>Calculate(null)</c> 不适用，直接调本方法</item>
    /// </list>
    /// </para>
    /// </summary>
    public static decimal HighestBurn(CardModel card, Creature? _)
    {
        // 非战斗（卡牌库、卡组界面）没有棋局可言，返回 0 → 卡面显示基础值。
        if (!TryHighestBurn(card, out int burn))
        {
            return 0m;
        }

        return burn;
    }

    /// <summary>
    /// 结算用途的版本：能拿到棋局就输出最高燃烧层数并返回 <c>true</c>，
    /// 否则返回 <c>false</c>（调用方据此决定是显示基础值还是直接返回）。
    /// </summary>
    public static bool TryHighestBurn(CardModel card, out int highest)
    {
        highest = 0;

        if (!card.IsMutable || card.Owner == null)
        {
            return false;
        }

        // 卡牌可能不在任何棋局里（卡牌库预览）。注意 Owner.Creature.CombatState
        // 在脱离棋局时同样可能为 null，所以两条路都要判。
        ICombatState? combatState = card.CombatState ?? card.Owner.Creature?.CombatState;
        if (combatState == null)
        {
            return false;
        }

        // ⚠️ 用 Creatures（双方）而不是 GetOpponentsOf（仅敌人）—— 见类注释。
        foreach (Creature creature in combatState.Creatures)
        {
            if (!creature.IsAlive)
            {
                continue;
            }

            int burn = creature.GetPowerAmount<BurnPower>();
            if (burn > highest)
            {
                highest = burn;
            }
        }

        return true;
    }
}
