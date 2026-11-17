using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 水蓝之心 / AquaHeart —— **事件遗物**（未知海洋）。
/// 每当你打出一张带有附魔的卡牌，对所有敌人造成 3 点伤害。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么<b>必须</b>带 <c>[Pool]</c>（⚠️ 踩过坑）
/// ══════════════════════════════════════════════════════════════════
/// 第一版**没有**加 <c>[Pool]</c>，结果游戏<b>启动失败</b>：
/// <code>
/// System.Exception: Model Wizard.Scripts.Relics.AquaHeart
///     must be marked with a PoolAttribute to determine which pool to add it to.
/// </code>
/// 原因：BaseLib 的 <c>CustomContentDictionary.AddModel</c>
/// （<c>CustomContentDictionary.cs</c>）对**每一个**自定义模型都强制要求 <c>PoolAttribute</c>，
/// 缺失直接抛异常、中断模型注册 —— <b>原版事件遗物不带池，但那是原版，不是 BaseLib 的自定义模型</b>。
/// </para>
///
/// <para>
/// 放进 <see cref="WizardRelicPool"/> 是安全的：本遗物稀有度是
/// <c>RelicRarity.Event</c>，而随机遗物只掷
/// <c>Common / Uncommon / Rare</c>（<c>RelicFactory.RollRarity</c>）
/// 与 <c>Shop</c>（<c>RelicGrabBag</c>），<b>Event 永远不会被掷到</b>。
/// 所以它只会通过「未知海洋」事件的 <c>RelicCmd.Obtain&lt;AquaHeart&gt;(Owner)</c> 出现。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 触发：<c>AfterCardPlayed</c> + 判定"这张牌有附魔"
/// ══════════════════════════════════════════════════════════════════
/// <c>CardModel.Enchantment</c> 非空即"带附魔"（原版 <c>EnchantmentModel</c> 的唯一载体）。
/// 伤害写法参照原版事件遗物 <c>ForgottenSoul</c>：
/// 先 <c>VfxCmd.PlayOnCreatureCenter</c> 播命中特效，再
/// <c>CreatureCmd.Damage(...)</c> 用 <c>ValueProp.Unpowered</c>（不吃力量/易伤，固定 3 点）。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class AquaHeart : ArcaneRelicModel
{
    /// <summary>每次触发的伤害（design 固定 3）。</summary>
    private const decimal DamagePerTrigger = 3m;

    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>打出一张带附魔的牌 ⇒ 对所有敌人造成 3 点伤害。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只认自己打出的牌（多人时别的玩家出牌不该触发）。
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Enchantment == null)
        {
            return;
        }

        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        List<Creature> targets = combatState.HittableEnemies.ToList();
        if (targets.Count == 0)
        {
            return;
        }

        Flash();

        foreach (Creature enemy in targets)
        {
            VfxCmd.PlayOnCreatureCenter(enemy, "vfx/vfx_attack_blunt");
        }

        await CreatureCmd.Damage(
            choiceContext,
            targets,
            DamagePerTrigger,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
