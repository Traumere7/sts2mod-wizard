using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 巨焱拳 - 2 费 - 攻击 - 稀有
/// 造成 4(6) 点火焰伤害 3 次。
/// 额外造成等同于**本场战斗中给予的燃烧层数**的伤害。
///
/// <para>
/// <b>⚠️ 口径（2026-10-01 定稿）：数"施加动作"，不数"实际写入"。</b>
/// 燃烧是"取高值、不叠加"的，所以"给已有 2 层的敌人再施加 1 层"实际写入 0 层 ——
/// 但这<b>算一次有效的给予</b>，本牌要计 1。
/// </para>
///
/// <para>
/// <b>例</b>：怪身上 2 层燃烧 → 打「观火」（施加 1 层）→ 层数仍是 2，
/// 但「巨焱拳」的计数 +1。
/// </para>
///
/// <para>
/// <b>因此不能用战斗历史 <c>PowerReceivedEntry</c>：</b>
/// 那本表只在<b>数值真的变了</b>时才记录 ——
/// <c>PowerCmd.ModifyAmount</c> 有 <c>if ((int)modifiedOffset != 0)</c> 判断
/// （<c>PowerCmd.cs:242-243</c>），取高值导致无变化时那条记录<b>根本不存在</b>，
/// 事后无法区分"没施加"与"施加了但没生效"。
/// </para>
///
/// <para>
/// <b>改用 <see cref="BurnTally"/> 累加器</b>，它订阅
/// <see cref="WizardEvents.BurnApplied"/> —— 那个事件由
/// <see cref="SpellCmd.ApplyBurn"/> 在<b>所有分支上无条件</b>触发，
/// 携带的是<b>请求层数</b>（不是最终层数），正是本牌要的口径。
/// <c>ApplyBurn</c> 是全部燃烧来源的唯一收口，所以不会漏。
/// </para>
///
/// <para>
/// <b>只统计"我给的"</b>：<see cref="BurnTally"/> 按施加者分别累计，
/// 这里取 <c>Owner.Creature</c> 那一份，所以敌人给自己上的燃烧不算。
/// 多人环境下也各算各的，不会互相污染。
/// </para>
///
/// <para>
/// <b>燃烧免疫时不计</b>：<c>ApplyBurn</c> 遇到免疫会直接 return、连事件都不发 ——
/// 那时"施加动作"确实不存在，不计符合设计。
/// </para>
///
/// <para>
/// <b>段数用 <c>WithHitCount</c> 一次结算</b>（照抄「炽焰术」「静电」），
/// 走引擎的多段攻击通道，让"每段各算一次"的钩子（电流外溢、燃烧之魂）正确触发。
/// 额外伤害<b>加在每一段上</b>（设计要求"额外造成等同于…的伤害"，
/// 与「落日」把最高燃烧加到每段同一个口径）。
/// </para>
///
/// <para>
/// <b>卡面文案按 <c>design.md</c> 逐字实现</b>（2026-10-01 定稿）：
/// <code>
/// 造成{CalculatedDamage:diff()}点[orange]火焰伤害[/orange]3次。
/// 额外造成等同于本场战斗中给予的[gold]燃烧[/gold]层数的伤害。
/// </code>
/// 但那个数字<b>不是印刷伤害，而是已计入累计燃烧的最终值</b> ——
/// 用 <c>CalculatedDamage</c> 承载，<b>手里看的就是算完的数</b>。
/// 算式就是 <see cref="CalculatedVar"/> 的通用形式
/// <c>CalculationBase + CalculationExtra × 倍率</c>：
/// <list type="bullet">
///   <item><c>CalculationBase</c> = 卡的印刷伤害（4，升级后 6）</item>
///   <item><c>ExtraDamage</c> = 1</item>
///   <item>倍率 = <see cref="TotalBurnAppliedThisCombat(CardModel?)"/>（本场累计燃烧）</item>
/// </list>
/// 于是 <c>n = 4 + 本场累计燃烧</c>，文案里 <c>{CalculatedDamage:diff()}</c> 就是它。
/// </para>
///
/// <para>
/// <b>这个"数字用动态值 + 保留解释句"的组合就是原版惯例</b>，
/// 例如「完美打击」<c>PerfectedStrike</c>：
/// <code>
/// 造成{CalculatedDamage:diff()}点伤害。
/// 你每有一张名字中含有"打击"的牌，伤害+{ExtraDamage:diff()}。
/// </code>
/// 「欺凌」<c>Bully</c>、「新月矛」<c>CrescentSpear</c> 等 19 张原版卡同理。
/// </para>
///
/// <para>
/// ⚠️ <b>不要在这里再放一个 <c>DamageVar</c>。</b>
/// 卡面数字由 <c>CalculatedDamage</c> 单独承担；多放一个会造成
/// "两个变量都叫伤害"的歧义（本地化里到底引用哪个、升级时该升哪个）。
/// 结算与展示**都走 <c>CalculatedDamage.Calculate(target)</c>**，
/// 天然一致。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class BlazingPunch : WizardSpellCard
{
    private const int Cost = 2;

    private const int Hits = 3;

    /// <summary>印刷伤害（= <c>CalculationBase</c>），升级 +2 → 6。</summary>
    private const decimal BaseDamage = 4m;

    public override SpellSchool School => SpellSchool.Fire;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // ⚠️ CalculationBase / ExtraDamage 是 CalculatedDamageVar 的**硬前置**。
        //
        //    注意这里是 ExtraDamageVar（键名 "ExtraDamage"），
        //    **不是** CalculationExtraVar（键名 "CalculationExtra"）！
        //    基类 CalculatedVar 读的是 CalculationExtra，
        //    但 CalculatedDamageVar 把 GetExtraVar() 重写成了 DynamicVars.ExtraDamage
        //    （见 CalculatedDamageVar.cs 末尾），声明错了会在卡面刷新时抛
        //    KeyNotFoundException: 'ExtraDamage' was not present in the dictionary
        //    —— 实测：用控制台加进手里才炸，构建期完全看不出来。
        new CalculationBaseVar(BaseDamage),
        new ExtraDamageVar(1m),

        // 卡面那个 n：= CalculationBase + 1 × 本场累计燃烧。
        // Props 带上火焰流派标记，保证寒冷/超导/法术增益等按流派生效。
        new CalculatedDamageVar(ValueProp.Move | SpellProps.For(School))
            .WithMultiplier((CardModel card, Creature? _) => TotalBurnAppliedThisCombat(card)),
    ];

    public BlazingPunch()
        : base(Cost, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // 每段伤害 = 卡面那个 n = 印刷伤害 + 本场累计燃烧。
        //
        // ⚠️ 用 CalculatedDamage.Calculate(target) 而不是自己拼：
        //    这样"结算值"与"卡面显示值"走同一条路径，天然一致；
        //    而且 .WithValueProp(...) 仍把火焰流派标记传下去，
        //    寒冷/超导/法术增益照常生效。
        //    取的是**施放瞬间**的值，3 段都用同一个数（不逐段递增）。
        decimal perHit = DynamicVars.CalculatedDamage.Calculate(cardPlay.Target);

        await DamageCmd.Attack(perHit)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(Hits)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 本场战斗中**自己****施加**的燃烧层数总和
    /// （口径 = 施加动作，含"取高值导致层数没变"的那些次）。
    ///
    /// <para>
    /// 实现就是读 <see cref="BurnTally"/> —— 它在每次
    /// <see cref="WizardEvents.BurnApplied"/> 时按施加者累加请求层数，
    /// 由 <c>BurnTallyResetPatch</c> 在每场战斗开头清零。
    /// </para>
    ///
    /// <para>
    /// <paramref name="card"/> 只用来到 <c>Owner.Creature</c>（= 施加者）。
    /// 拿不到主人（卡组/卡牌库预览）时返回 0。
    /// </para>
    /// </summary>
    public static decimal TotalBurnAppliedThisCombat(CardModel? card = null)
        => BurnTally.TotalFor(card?.Owner?.Creature);

    protected override void OnUpgrade()
    {
        // 4 → 6（段数不变）。卡面那个 n 的"印刷伤害"部分就是 CalculationBase，
        // 所以升它一处即可 —— 倍率（累计燃烧）不受升级影响。
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
    }
}
