using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 巫术百科 / Maleficarum - Rare
/// 每当累计给予敌人 3 种本场战斗开始前<b>未给予过</b>的[gold]负面效果[/gold]时，获得 3 费。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 口径（2026-10-12 用户拍板）
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>计数器显示在遗物图标下方</b>（<c>ShowCounter</c> + <c>DisplayAmount</c>）。</item>
///   <item><b>每场战斗开始时计数器归 0，"记忆"也一并清空</b>（两者都<b>不</b>跨战斗）
///         —— 所以都是普通字段 + <c>BeforeCombatStart</c> 里重置，
///         <b>不需要</b> <c>[SavedProperty]</c>。</item>
///   <item><b>只有"本场战斗未给过"的种类才计数</b>；同一种类本场只计一次。
///         因为记忆每场清空，"本场没给过"就等价于"本场战斗开始前未给过"。</item>
///   <item><b>计数器每达到 3 的倍数就触发</b>（3、6、9…）给 3 费。
///         实现上只需在自增后判 <c>% 3 == 0</c>：因为计数器<b>不重置</b>，
///         3、6、9 自然各命中一次 —— 比"每次清零再数满 3"更贴合"累计"二字。</item>
/// </list>
///
/// <para>
/// <b>「种类」用什么作键</b>：照抄原版「不祥之灯」<c>UnsettlingLamp.cs:171</c>
/// 的做法，用 <c>power.GetType()</c>（<c>System.Type</c>）。
/// 不用 <c>power.Id</c> 是因为 <c>Type</c> 是原版既有惯例，且天然去重。
/// </para>
///
/// <para>
/// ⚠️ <b>这个钩子在"增量为 0"时不会触发</b>（<c>PowerCmd.cs:247</c>）。
/// 对"新种类"语义无影响 —— 新种类第一次必然是从 0 变成正数。
/// 但注意：<b>本 mod 的燃烧是"取高值"</b>，敌人已有 4 层时再施加 1 层
/// 增量为 0、不触发。若将来需要"施加动作本身算数"，得改用 <c>WizardEvents</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>没有 <c>target</c> 参数</b>，目标是 <c>power.Owner</c>（<c>PowerModel.cs:264</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b>排除 <c>ITemporaryPower</c></b>（<c>SleightOfFleshPower.cs:23</c>）：
/// 「临时力量」这类会施加内层真实能力，不排除会把一种负面算成两种。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class Maleficarum : ArcaneRelicModel
{
    /// <summary>触发步长：每这么多种新负面给一次费。</summary>
    private const int Step = 3;

    private int _count;
    private HashSet<Type> _countedThisCombat = [];

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool ShowCounter => true;

    public override int DisplayAmount => Count;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3),
        new DynamicVar("Step", Step),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
    ];

    /// <summary>
    /// 本场战斗的计数（<b>每场战斗归 0</b> ⇒ 不进存档）。
    /// </summary>
    private int Count
    {
        get => _count;
        set
        {
            AssertMutable();
            _count = value;
            InvokeDisplayAmountChanged();
        }
    }

    /// <summary>
    /// "这场战斗已经给过哪些种类"的**记忆** —— 本场内不重复计数。
    /// </summary>
    private HashSet<Type> GivenThisCombat => _givenThisCombat;

    /// <summary>
    /// ⚠️⚠️ <b>2026-10-12 实测修正：这个"记忆"<b>不跨战斗</b>，每场战斗开始都要清空。</b>
    ///
    /// <para>
    /// 用户原话："每场战斗开始时不仅计数器清 0，'记忆'（即记录哪些负面效果已经给予过了）
    /// 也要清空。下场战斗不会继承本场战斗哪些给予过的记忆。"
    /// </para>
    ///
    /// <para>
    /// 所以它只是<b>普通字段</b>（<c>BeforeCombatStart</c> 里 <c>Clear()</c>），
    /// <b>不再</b>是 <c>[SavedProperty]</c> ——
    /// 初版把它做成跨战斗持久，是把"本场战斗中未给予过"误读成了"历史上未给予过"。
    /// </para>
    ///
    /// <para>
    /// 顺带这也消掉了初版那个"<c>HashSet&lt;Type&gt;</c> 不被 <c>[SavedProperty]</c> 支持"
    /// 的问题（那条坑仍记录在 <c>RELICS-REFERENCE.md</c> §2.5，供以后参考）。
    /// </para>
    /// </summary>
    private HashSet<Type> _givenThisCombat = [];

    /// <summary>
    /// 每场战斗开始：**计数器归 0、"记忆"也清空**（两者都不跨战斗）。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        Count = 0;
        GivenThisCombat.Clear();
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // ① 只认"我们自己施加的"。
        if (applier != Owner.Creature)
        {
            return;
        }

        // ② 只认"落在敌人身上"的。
        if (!power.Owner.IsEnemy)
        {
            return;
        }

        // ③ 跳过隐藏的内部能力。
        if (!power.IsVisible)
        {
            return;
        }

        // ④ 必须是负面（按带符号的量判定）。
        if (power.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return;
        }

        // ⑤ 排除临时属性能力（会施加内层真实能力，导致一种被算成两种）。
        if (power is ITemporaryPower)
        {
            return;
        }

        // ⑥ 神器会挡掉，不算"给上了"。
        if (power.Owner.HasPower<ArtifactPower>())
        {
            return;
        }

        Type kind = power.GetType();

        // ⑦ "本场战斗是否已经给过这个种类" —— 这是**唯一**的判据。
        //    记忆每场清空（见 BeforeCombatStart），所以"本场没给过"就等价于
        //    "本场战斗开始前未给过"（用户口径）。
        //    初版还额外查了一份跨战斗的"历史集合"，那是把口径理解错了，已移除。
        if (!GivenThisCombat.Add(kind))
        {
            return;
        }

        Count++;

        // 计数器不重置 ⇒ 3、6、9… 各命中一次。
        if (Count % Step != 0)
        {
            return;
        }

        Flash();

        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}
