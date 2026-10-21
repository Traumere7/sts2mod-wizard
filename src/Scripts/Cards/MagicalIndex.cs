using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 禁书目录 - 0 费 - 技能 - 稀有
/// 给予 2(3) 层易伤、2(3) 层燃烧、2(3) 层麻痹、2(3) 层寒冷。消耗。
///
/// <para>
/// 四种负面效果一次性叠满，是「魔力汲取」「降雷」「全知形态」的主要弹药。
/// </para>
///
/// <para>
/// <b>燃烧走 <see cref="SpellCmd.ApplyBurn"/>，其余三种走 <c>PowerCmd.Apply</c>。</b>
/// 区别在于燃烧有"不可叠加、取高值"的特殊规则，
/// 且需要无条件触发"给予燃烧时"的效果（如「纵火狂」），
/// 这些逻辑都封在 <see cref="SpellCmd.ApplyBurn"/> 里，不能绕过。
/// </para>
///
/// <para>
/// <b>⚠️ 「消耗」始终存在，升级不移除它</b>（2026-09-28 设计改动）。
/// <c>CanonicalKeywords</c> 声明 <c>CardKeyword.Exhaust</c>，
/// 而 <c>OnUpgrade</c> <b>只加数值、不碰词条</b> ——
/// 升级后四种效果各 +1 层（2 → 3），代价仍是消耗。
/// </para>
///
/// <para>
/// ⚠️ <b>类名曾是 <c>IndexOfForbiddenBooks</c>，已改名为 <c>MagicalIndex</c>。</b>
/// 改名会连带三处，缺一不可（详见 <c>CONTEXT.md</c> §9）：
/// <list type="bullet">
///   <item>类名 → 决定 <c>ModelId.Entry</c> = <c>WIZARD-MAGICAL_INDEX</c></item>
///   <item>卡图文件名 → <c>MagicalIndex.png</c>（<c>CustomPortraitPath</c> 按类名拼）</item>
///   <item>本地化键 → <c>WIZARD-MAGICAL_INDEX.*</c></item>
/// </list>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class MagicalIndex : WizardCardModel
{
    /// <summary>
    /// 费用。
    ///
    /// <para>
    /// ⚠️ <b>2026-10-10：0 → 1（平衡调整）。</b>design.md 第 96 行已改为「禁书目录 - 1 - 稀有」。
    /// 本牌升级是"各效果 +1 层"（不再移除消耗），所以升级**不降费**。
    /// </para>
    /// </summary>
    private const int Cost = 1;

    private const string VulnerableKey = nameof(VulnerablePower);
    private const string BurnKey = nameof(BurnPower);
    private const string ParalysisKey = nameof(ParalysisPower);
    private const string ChillKey = nameof(ChillPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromPower<ParalysisPower>(),
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(2m),
        new PowerVar<BurnPower>(2m),
        new PowerVar<ParalysisPower>(2m),
        new PowerVar<ChillPower>(2m),
    ];

    /// <summary>初始就有「消耗」，升级由 <c>RemoveKeyword</c> 移除。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public MagicalIndex()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        var target = cardPlay.Target;

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext, target, DynamicVars[VulnerableKey].BaseValue, Owner.Creature, this);

        await SpellCmd.ApplyBurn(
            choiceContext, target, DynamicVars[BurnKey].BaseValue, Owner.Creature, this);

        await PowerCmd.Apply<ParalysisPower>(
            choiceContext, target, DynamicVars[ParalysisKey].BaseValue, Owner.Creature, this);

        // 寒冷走统一收口，让"每当施加寒冷时"的效果（如「弹射」）能响应到。
        await SpellCmd.ApplyChill(
            choiceContext, target, DynamicVars[ChillKey].BaseValue, Owner.Creature, this);
    }

    /// <summary>升级增量的基数：四种效果都各 +1 层。</summary>
    private const decimal UpgradeStep = 1m;

    /// <summary>
    /// 升级：**四种效果各 +1 层**（2 → 3），<b>不再移除「消耗」</b>。
    ///
    /// <para>
    /// ⚠️ 2026-09-28 设计改动。此前是照抄原版「蜃景」<c>Mirage</c> 的
    /// "<c>RemoveKeyword(CardKeyword.Exhaust)</c>"，现在改成纯数值提升，
    /// 所以「消耗」**始终存在**（它在 <see cref="CanonicalKeywords"/> 里，
    /// 升级不去动它）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>不要以为 <c>PowerVar</c> 会自己涨。</b>
    /// <c>PowerVar</c> 继承 <c>DynamicVar</c>，而 <c>DynamicVar</c>
    /// <b>没有</b>任何"升级步长"机制 —— <c>UpgradeValueBy</c> 就是个纯加法
    /// （<c>DynamicVar.cs:143-147</c>：<c>BaseValue += addend</c>）。
    /// 原版也**没有**"一张牌多个 PowerVar 一起升级"的范本可抄，
    /// 所以四个变量必须逐个显式 +1。
    /// </para>
    /// </summary>
    protected override void OnUpgrade()
    {
        // 2 → 3，四种效果各 +1。
        DynamicVars[VulnerableKey].UpgradeValueBy(UpgradeStep);
        DynamicVars[BurnKey].UpgradeValueBy(UpgradeStep);
        DynamicVars[ParalysisKey].UpgradeValueBy(UpgradeStep);
        DynamicVars[ChillKey].UpgradeValueBy(UpgradeStep);
    }
}
