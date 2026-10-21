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
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 铅咒板 / CurseTablet - Uncommon
/// 每回合你第一次给予敌人[gold]负面效果[/gold]时，获得 4 [gold]格挡[/gold]。
///
/// <para>
/// <b>两个原版模板组合而成：</b>
/// <list type="number">
///   <item><b>「每回合第一次」的标志与重置</b> —— 照抄原版「恶毒之舌」
///         <c>DemonTongue</c>：一个<b>普通字段</b>（<b>不是</b> <c>[SavedProperty]</c>，
///         因为它不该跨存档存活），在 <c>BeforeSideTurnStart</c> 里重置。</item>
///   <item><b>「给敌人上了负面」的判定</b> —— 照抄「不祥之灯」<c>UnsettlingLamp</c>
///         与能力「血肉戏法」<c>SleightOfFleshPower</c> 的组合条件。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>为什么用 <c>AfterPowerAmountChanged</c> 而不是本 mod 的 <c>WizardEvents</c>：</b>
/// 那张板子的语义是"<b>上了负面效果</b>"，判定主体是<b>引擎的能力层数变化</b>，
/// 而不是"本 mod 自己的燃烧/寒冷施加动作"。而且它要能响应<b>任何</b>负面
/// （易伤、虚弱、中毒、以及原版/其它 mod 的负面），所以必须用引擎级钩子。
/// </para>
///
/// <para>
/// ⚠️ <b>这个钩子在"增量为 0"时不会触发</b>（<c>PowerCmd.cs:247</c>：
/// <c>if ((int)modifiedOffset != 0)</c>）。对"首次给予"的语义来说这没问题 ——
/// 敌人已经有该负面时再加，本来也不该算"第一次给"。
/// </para>
///
/// <para>
/// ⚠️ <b>这个钩子<b>没有</b> <c>target</c> 参数</b>，目标要读 <c>power.Owner</c>
/// （<c>PowerModel.cs:264</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b>排除 <c>ITemporaryPower</c></b>（<c>SleightOfFleshPower.cs:23</c> 的写法）：
/// 「临时力量」这类能力会在内部<b>再施加一次</b>真实能力，
/// 不排除的话一次出牌会触发两次。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class CurseTablet : ArcaneRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>本回合是否已经给过负面（照抄 <c>DemonTongue.cs:15</c> 的普通字段）。</summary>
    private bool _givenDebuffThisTurn;

    /// <summary>
    /// 每回合重置 —— <c>DemonTongue.cs:29-37</c> 的写法。
    ///
    /// <para>
    /// 用 <c>BeforeSideTurnStart</c>（回合<b>开始前</b>），不是 <c>AfterSideTurnStart</c>：
    /// 后者会让"回合开始瞬间自动结算的负面"（如某些遗物/能力在回合开始的施放）
    /// 落在重置<b>之后</b>，把它们错算进新回合。
    /// </para>
    /// </summary>
    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature))
        {
            _givenDebuffThisTurn = false;
        }

        return Task.CompletedTask;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Unpowered),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // ① 每回合只触发一次。
        if (_givenDebuffThisTurn)
        {
            return;
        }

        // ② 只认"我们自己施加的"。
        if (applier != Owner.Creature)
        {
            return;
        }

        // ③ 只认"落在敌人身上"的（没有 target 参数，用 power.Owner）。
        if (!power.Owner.IsEnemy)
        {
            return;
        }

        // ④ 跳过隐藏的内部能力（如 TemporaryXxx 的内层）。
        if (!power.IsVisible)
        {
            return;
        }

        // ⑤ 必须是负面 —— 用 GetTypeForAmount(amount) 按**带符号的量**判定，
        //    不能用 power.Type（后者对"负力量"这类会判错）。
        if (power.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return;
        }

        // ⑥ 排除临时属性能力：它们会在内部再施加一次真实能力，导致重复触发。
        if (power is ITemporaryPower)
        {
            return;
        }

        // ⑦ 神器会挡掉这次施加，不算"给上了负面"。
        if (power.Owner.HasPower<ArtifactPower>())
        {
            return;
        }

        await GrantBlock();
    }

    // ══════════════════════════════════════════════════════════════════
    // 燃烧：必须额外订阅 WizardEvents.BurnApplied
    // ══════════════════════════════════════════════════════════════════
    //
    // ⚠️⚠️ <b>2026-10-13 用户报告</b>：目标已有高层数燃烧时，再给它<b>低层数</b>燃烧
    // （例如身上 4 层、再施加 1 层）本遗物<b>不触发</b>。
    //
    // <b>原因</b>：燃烧是"取高值"的（见 <see cref="BurnPower"/>），
    // 低层数施加不改变层数 ⇒ 引擎的
    // <c>if ((int)modifiedOffset != 0) await Hook.AfterPowerAmountChanged(...)</c>
    // （<c>PowerCmd.cs:247</c>）<b>根本不发</b>，上面那个钩子收不到通知。
    //
    // <b>修法与「全知形态」「纵火狂」对齐</b>：订阅
    // <see cref="WizardEvents.BurnApplied"/> —— 它正是为"施加动作本身算数"
    // 而存在的（在 <see cref="SpellCmd.ApplyBurn"/> 里无条件触发）。
    //
    // <para>
    // ⚠️ <b>必须用事件带来的 <c>landed</c>，不要自己查 artifact</b>：
    // 被抵消时引擎已经把那一层 artifact 减掉了（减层在
    // <c>PowerCmd.Apply:156</c> 的 <c>AfterModifyingPowerAmountReceived</c>），
    // 事件回调里查 <c>HasPower&lt;ArtifactPower&gt;()</c> 必为 false ⇒ 会误判成"没被抵消"。
    // 这正是「纵火狂」当初踩过的坑。
    // </para>
    //
    // <para>
    // <b>不会重复触发</b>：燃烧层数<b>上升</b>时两条路都会到
    // （引擎钩子 + 本事件），但都走 <see cref="_givenDebuffThisTurn"/> 去重，
    // 第二次进入直接返回。
    // </para>

    /// <summary>是否已订阅燃烧事件（退订时的幂等保护）。</summary>
    private bool _subscribed;

    /// <summary>
    /// 拾起时订阅燃烧事件。
    ///
    /// <para>
    /// ⚠️ 与「纵火狂」<c>PyromaniacPower</c> 的写法<b>有一处关键差别</b>：
    /// 能力每场战斗都会新建、战斗结束被清空，所以它在 <c>AfterApplied</c> 订阅、
    /// 在 <c>AfterCombatEnd</c> 退订。
    /// 而<b>遗物是持久的</b>（一直在 <c>Owner.Relics</c> 里，跨战斗不重建），
    /// 所以订阅必须<b>跟遗物的生命周期一致</b>：拾起时订阅、<b>被移除时</b>才退订。
    /// 若照抄能力那样在战斗结束退订，第二场战斗就再也收不到事件了。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 钩子签名也不同：<c>RelicModel</c> 是
    /// <c>AfterObtained()</c> / <c>AfterRemoved()</c>（<b>无参</b>），
    /// 不是能力那套 <c>AfterApplied(Creature?, CardModel?)</c> /
    /// <c>AfterRemoved(Creature)</c>。
    /// </para>
    ///
    /// <para>
    /// 常驻订阅是安全的：非战斗期间事件不会被触发
    /// （<c>SpellCmd.ApplyBurn</c> 只在战斗流程里被调用），
    /// 即便触发也只会命中 <see cref="_givenDebuffThisTurn"/> 的早退分支。
    /// </para>
    /// </summary>
    public override Task AfterObtained()
    {
        // 先退订再订阅，保证不会重复挂同一个委托。
        WizardEvents.BurnApplied -= OnBurnApplied;
        WizardEvents.BurnApplied += OnBurnApplied;
        _subscribed = true;
        return Task.CompletedTask;
    }

    /// <summary>遗物被移除时退订（这是唯一该退订的时机，见 <see cref="AfterObtained"/>）。</summary>
    public override Task AfterRemoved()
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 战斗结束只清"本回合"标志，<b>不退订</b>（订阅跟遗物生命周期，见 <see cref="AfterObtained"/>）。
    /// </summary>
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        _givenDebuffThisTurn = false;
        return Task.CompletedTask;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        WizardEvents.BurnApplied -= OnBurnApplied;
        _subscribed = false;
    }

    /// <summary>
    /// 燃烧被施加时触发 —— 覆盖"取高值导致层数没变、引擎钩子不发"的情况。
    /// </summary>
    private async Task OnBurnApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        // ⚠️ 订阅是<b>常驻</b>的（跟遗物生命周期，见 AfterObtained），
        // 所以要防御"遗物还在、但这一局的 Owner 已经不成立"的情况：
        // 例如旧局结束后遗物实例尚未回收，而新局又开始了。
        if (Owner?.Creature == null)
        {
            return;
        }

        // 只认"我们自己施加给敌人的"。
        if (applier != Owner.Creature || !target.IsEnemy)
        {
            return;
        }

        // ★ 被「人工制品」抵消 ⇒ 没真上到，不算"给上了负面"。
        if (!landed)
        {
            return;
        }

        await GrantBlock();
    }

    /// <summary>
    /// 记录"本回合已经给过负面"并给格挡。
    ///
    /// <para>
    /// 两条路径（引擎钩子 + 燃烧事件）<b>共用这里</b>，
    /// 靠 <see cref="_givenDebuffThisTurn"/> 去重，所以同一次施加不会给两份格挡。
    /// </para>
    /// </summary>
    private async Task GrantBlock()
    {
        // 每回合只触发一次。
        if (_givenDebuffThisTurn)
        {
            return;
        }

        _givenDebuffThisTurn = true;

        Flash();

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            null);
    }
}
