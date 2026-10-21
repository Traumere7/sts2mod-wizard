using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 燃烧之魂：**本回合内，你每一次攻击结算后，立刻插入
/// <c>Amount</c> 段等量的火焰伤害，并给予 <c>Amount</c> 层燃烧。**
///
/// <para>
/// <b>"每一次攻击"是准确的语义：</b>多段攻击的每一段都会单独触发一次
/// （例：火球术 = 3 伤 ×2 段，开 1 层燃烧之魂后结算为
/// 3伤 → 3火焰 → 3伤 → 3火焰，共 4 段）。
/// 这是 <c>AfterDamageGiven</c> 每段各触发一次的天然结果，不需要额外处理。
/// </para>
///
/// <para>
/// ⚠️ <b>2026-10-04 改动：由"固定 1 次"改为"每层 1 次"（可叠层）。</b>
/// 打第 1 张 = 1 层 → 每次攻击追 1 段；打第 2 张 = 2 层 → 每次攻击追 2 段，以此类推。
/// 原本声明的是 <see cref="PowerStackType.Single"/>，打两张也只追 1 段。
/// </para>
///
/// <para>
/// <b>触发点</b>用 <c>AfterDamageGiven</c>（范本 <c>EnvenomPower</c> 涂毒）。
/// 判定"这是一次攻击"用 <c>props.IsPoweredAttack()</c> ——
/// 它等价于 <c>Move &amp;&amp; !Unpowered</c>。
/// </para>
///
/// <para>
/// <b>"等量"取该段的结算伤害</b>（<c>DamageResult</c> 的数值），
/// 而不是卡面的基础伤害 —— 多段攻击时"等量"必须按段算，
/// 且这样能自然跟随易伤/增益等一切影响该段结果的因素。
/// <b>叠层时每一段追的都是同一个 <c>bonus</c></b>（都是"该段伤害"），
/// 不递增、不衰减 —— 因为设计说的是"追加 n 次<b>等量</b>火焰伤害"。
/// </para>
///
/// <para>
/// <b>⚠️ 不可递归：</b>燃烧之魂**不能**响应它自己产生的伤害。
/// 有两道保险：
/// <list type="number">
///   <item>插入的那段伤害走 <c>Unpowered</c>（不带 <c>Move</c>），
///         所以 <c>props.IsPoweredAttack()</c> 为 false，天然不匹配；</item>
///   <item><c>_resolving</c> 标志在任何异步重入的情况下兜底。</item>
/// </list>
/// 两层都要留着 —— 第 1 条依赖 ValueProp 的传播正确，
/// 一旦将来有人改了这段伤害的 props，第 2 条还能挡住无限递归。
/// <b>叠层后这两道保险更重要</b>：循环里每一段都可能触发钩子。
/// </para>
///
/// <para>
/// 插入的伤害不带学派、dealer 与 cardSource 均为 null：
/// 这样它不吃法术增益/寒冷/超导等任何加成（设计文档未要求它们参与）。
/// </para>
///
/// <para>
/// ⚠️ 但那层**燃烧**的 applier 必须传 <c>Owner</c>（不是 null）——
/// 「纵火狂」<see cref="PyromaniacPower"/> 的判定是 <c>applier == Owner</c>，
/// 传 null 会让它不响应。初版就是传了 null，实测反馈"燃烧之魂不触发纵火狂"。
/// 注意伤害的 dealer 仍然保持 null（那才是"无归属"的正确表达），
/// 两者是不同的参数、要分开对待。
/// </para>
///
/// <para>
/// <b>在玩家回合结束时移除</b> —— 设计是"这个回合"的效果。
/// 这与 <see cref="FrostWallPower"/> 相反：那个要覆盖"敌人这一回合的攻击"，
/// 所以必须在敌人回合末才移除。**两者时机不同，不要抄错。**
/// </para>
/// </summary>
public class BurningSoulPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 每次攻击追加几段火焰伤害（也 = 给几层燃烧）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/burning_soul_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/burning_soul_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    /// <summary>防重入：万一插入的伤害仍被判定为攻击，用它兜底。</summary>
    private bool _resolving;

    /// <summary>
    /// 追加的段数。
    /// <c>Math.Max(Amount, 1)</c> 兜底：层数为 0 时不该发生，
    /// 但也不该退化成"完全不触发"。
    /// </summary>
    private int ExtraHits => Math.Max(Amount, 1);

    /// <summary>玩家回合结束即移除（"这个回合"语义）。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (_resolving || dealer != Owner)
        {
            return;
        }

        // 只响应"攻击"。IsPoweredAttack = Move && !Unpowered。
        // 我们自己产生的火焰伤害走 Unpowered，所以天然不会递归。
        if (!props.IsPoweredAttack() || !target.IsAlive)
        {
            return;
        }

        // "等量" = 该段实际造成的伤害。叠层时每段追的都是这个同一个值。
        decimal bonus = result.TotalDamage;

        if (bonus < 1m)
        {
            return;
        }

        int hits = ExtraHits;

        // ⚠️⚠️ <b>每次请求的燃烧层数必须是 `hits`，不能写死 1。</b>
        //
        // 这是 2026-10-04 实测发现的第二个问题（第一个是 StackType）。
        // 原来循环里是 `SpellCmd.ApplyBurn(..., 1m, ...)`，于是
        // 「3 张燃烧之魂」＝循环 3 次 × 每次请求 1 层 ——
        // 而燃烧是**取高值**的（`BurnPower.ApplyAbsolute`：
        // `current >= desired` 时直接返回、不写入），
        // **请求 1 层 3 次，结果仍然是 1 层**，层数根本不会累加。
        //
        // 实测现象（打 3 张后打「火球术」，日志逐条对得上）：
        // <code>
        // 第1段 ① 请求1 → 写入1     ② 请求1 → 无需改动   ③ 请求1 → 无需改动
        // 第2段 ④⑤⑥ 请求1 → 全部"无需改动"（现值仍是 1）
        // 火球术 ⑦ 请求2 → 1 -> 2     ← 火球术自己的 2 层把结果盖成 2
        // </code>
        // 最终只有 2 层。改成每次请求 `hits` 层后：
        // 3 张燃烧之魂 → 每段请求 3 层 → 落地 3 层；
        // 火球术再请求 2 层时被取高值吃掉（3 >= 2），仍是 3 层 —— 与设计一致。
        _resolving = true;
        try
        {
            for (int i = 0; i < hits; i++)
            {
                // 每段前复查目标是否还活着 —— 前几段可能已把它打死。
                if (!target.IsAlive)
                {
                    break;
                }

                // 刻意走"无 dealer"的 6 参重载：
                // dealer 为 null 时这段伤害没有任何归属者，从源头上杜绝
                // 任何"造成伤害时"的能力把它当成自己造成的伤害而递归响应。
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    bonus,
                    ValueProp.Unpowered,
                    null,
                    null);

                await SpellCmd.ApplyBurn(choiceContext, target, hits, Owner, null);
            }
        }
        finally
        {
            _resolving = false;
        }
    }
}
