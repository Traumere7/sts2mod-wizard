using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 心流 / MindFlow - X 费 - 技能 - 罕见
/// 获得 6(7)X 点格挡。这张牌被消耗时，获得 X（X+1）费。
///
/// <para>
/// <b>X 费照抄原版「串刺」<c>Skewer</c>（X 费攻击）与「挽歌」<c>Dirge</c>（X 费技能）</b>，
/// 与本 mod 的「焚尽」<see cref="BurnToAshes"/> 同一套写法：
/// <list type="bullet">
///   <item><c>protected override bool HasEnergyCostX =&gt; true;</c></item>
///   <item>构造函数里的 baseCost 传 <b>0</b>（X 费卡的"基数"，不是实际费用）</item>
///   <item><c>OnPlay</c> 里用 <c>ResolveEnergyXValue()</c> 取实际 X</item>
/// </list>
/// <c>ResolveEnergyXValue()</c> 已经过 <c>Hook.ModifyXValue</c>，
/// 所以「化学X」<c>ChemicalX</c> 这类"X 值 +N"的遗物会自动生效
/// （见 <c>CardModel.cs:1113-1124</c>）—— <b>不要自己读能量，那样会漏掉 X 修正</b>。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>本牌【不】自带「消耗」词条（2026-10-08 修正）。</b>
/// 初版我把 <c>CardKeyword.Exhaust</c> 放进了 <c>CanonicalKeywords</c>，那是错的：
/// 设计里的"这张牌被消耗时"指的是<b>被【其它】牌消耗</b>
/// （「思索」「顿悟」的选择消耗、「高温消毒」等），
/// 而不是"打出后自动消耗"。这与本 mod「尾注」<see cref="Endnote"/> 的定位一致：
/// <b>一张需要主动喂给消耗体系的牌</b>。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>后半句的 X 不是"打出时支付的费用"（2026-10-08 修正）。</b>
/// 既然本牌不带消耗、要靠别的牌消耗它，"被消耗"完全可能发生在
/// <b>打出之后的另一个时刻</b>。所以这里的 X 指的是
/// <b>被消耗那一刻、当前的能量值</b>：
/// <code>
/// 获得的能量 = 当前能量（未升级）／ 当前能量 + 1（升级后）
/// </code>
/// 这正是设计原文"获得X（X+1）费"里括号表示升级值的含义 ——
/// 换句话说，未升级时它<b>把当前能量原样补满一遍</b>，
/// 升级后额外多给 1 点。
/// </para>
///
/// <para>
/// ⚠️ <b>因此不能像初版那样在 <c>OnPlay</c> 里存 <c>_paidX</c></b> ——
/// 那个值反映的是"打出时支付了多少"，而本牌真正需要的是
/// "被消耗时手上还有多少能量"，两者毫无关系。
/// 现在直接在 <c>AfterCardExhausted</c> 里读当前能量。
/// </para>
///
/// <para>
/// <b>「被消耗时触发」照抄原版「战鼓」<c>DrumOfBattle</c> 与「尾注」<see cref="Endnote"/>：</b>
/// 重写 <c>AbstractModel.AfterCardExhausted</c>（<c>AbstractModel.cs:447</c>），
/// 并<b>判断被消耗的是不是自己</b>（<c>card == this</c>）——
/// 这个钩子对<b>任何</b>卡被消耗都会触发，不判就会"别人被消耗、我也给费"。
/// </para>
///
/// <para>
/// <b>卡面文案刻意不使用动态变量</b>（用户明确要求）：
/// 后半句直接写死成 <c>X（X+1）</c>，因为那个 X 是运行时才知道的当前能量，
/// 用 DynamicVar 既表达不了、也会误导成"打出时支付的值"。
/// 前半句的系数仍用 <c>{Block:diff()}</c>，让升级后的 7 能显示出来。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class MindFlow : WizardCardModel
{
    /// <summary>X 费卡：baseCost 传 0，真实费用在打出时由 X 决定。</summary>
    private const int Cost = 0;

    /// <summary>格挡系数基数（升级 +1 → 7），格挡 = 系数 × X。</summary>
    private const int BlockPerX = 6;

    /// <summary>
    /// 本牌是 X 费（照抄原版「串刺」「挽歌」「多重施法」与本 mod「焚尽」）。
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-08 修：这个属性曾经【丢失】，导致两个症状同时出现。</b>
    /// 重新整理文件时我把属性本体删掉了、只留下上面类注释里描述它的那一行，
    /// 于是：
    /// <list type="number">
    ///   <item><b>左上角费用显示 0 而不是 X</b> ——
    ///         引擎在 <c>CardModel.EnergyCost</c> 里用
    ///         <c>new CardEnergyCost(this, CanonicalEnergyCost, HasEnergyCostX)</c>
    ///         构造费用；<c>HasEnergyCostX</c> 为 <c>false</c> 时
    ///         <c>CardEnergyCost.CostsX</c> 就是假的，UI 便按普通 0 费渲染。</item>
    ///   <item><b>打出后无结算、动画卡在半空</b> ——
    ///         <c>OnPlay</c> 里的 <c>ResolveEnergyXValue()</c> 开头就检查
    ///         <c>EnergyCost.CostsX</c>，为假则直接
    ///         <c>throw new InvalidOperationException("This card does not have an X-cost.")</c>
    ///         （<c>CardModel.cs:1117-1124</c>）。异常抛在 <c>OnPlay</c> 的 await 链里，
    ///         整个 <c>PlayCardAction</c> 中断 ⇒ 卡牌停在半空、没有任何结算。</item>
    /// </list>
    /// 一个缺失属性同时造成"显示错"与"功能崩"，日志里那条
    /// <c>does not have an X-cost</c> 就是它的指纹。
    /// </para>
    ///
    /// <para>
    /// <b>排查提示</b>：X 费卡出问题时，先确认这个属性真的存在 ——
    /// 光有类注释里的说明不算。用
    /// <c>Select-String -Path &lt;文件&gt; -Pattern 'HasEnergyCostX'</c>
    /// 应该看到<b>两处</b>：注释里的说明 + 属性本体。
    /// </para>
    /// </summary>
    protected override bool HasEnergyCostX => true;

    /// <summary>
    /// ⚠️ <b>刻意【不】带「消耗」</b> —— 本牌要靠别的牌来消耗它。
    /// 详见类注释。别"顺手"把 Exhaust 加回来。
    /// </summary>
    // 注意：这里没有 CanonicalKeywords 覆盖 —— 即不带任何词条。

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // ⚠️ Block 承载的是**系数**（6 → 升级 7），不是最终格挡值。
        //    最终值 = 系数 × X，随 X 变化，无法用静态变量表达。
        new BlockVar(BlockPerX, ValueProp.Move),

        // ⚠️ 后半句刻意【不】声明能量变量（2026-10-08 最终定案）。
        //
        // 卡面是**静态**的「X🔵」，那个图标只是"这里说的是能量"的符号，
        // 数量不随 X 变化 —— 所以用 `{energyPrefix:energyIcons(1)}` 渲染
        // 固定 1 个图标即可（与本 mod「魔力汲取」「全知形态」同一写法），
        // 不需要 EnergyVar、也不需要实时同步。
        //
        // 中途曾试过"实时图标数量"，那是绕远路：既引入两个变量、
        // 又要处理 `{IfUpgraded}` 与 `energyIcons()` 无法嵌套的问题。
    ];

    public MindFlow()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ResolveEnergyXValue 已含 Hook.ModifyXValue（「化学X」等 X 修正遗物会生效）。
        int x = ResolveEnergyXValue();
        if (x < 0)
        {
            x = 0;
        }

        // 格挡 = 系数 × X（系数未升级 6 / 升级后 7）。
        decimal block = DynamicVars.Block.BaseValue * x;
        if (block <= 0m)
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay,
            false);
    }

    /// <summary>
    /// 这张牌**被（其它牌）消耗时**获得费用
    /// （照抄「战鼓」<c>DrumOfBattle</c> / 「尾注」<see cref="Endnote"/>）。
    ///
    /// <para>
    /// ⚠️ 必须判 <c>card == this</c>：这个钩子对**任何**卡被消耗都会触发。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>X = 被消耗这一刻的当前能量</b>（不是打出时支付的费用，见类注释）。
    /// 所以此处直接读能量，绝不使用 <c>OnPlay</c> 里的任何值。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>读值必须在 <c>GainEnergy</c> 之前</b>：<c>PlayerCmd.GainEnergy</c>
    /// 会先触发 <c>Hook.ModifyEnergyGain</c>、再真正加能量
    /// （<c>PlayerCmd.cs:29-39</c>），先读后加才拿得到"加之前"的值。
    /// 若先加再读，就会变成"加完之后的值"，凭空多出一份。
    /// </para>
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-08 修：必须走 <c>Hook.ModifyXValue</c>，否则吃不到「化学物X」。</b>
    /// 实测反馈：有「化学物X」<c>ChemicalX</c> 时，前半句（格挡）能正确 +2，
    /// 但后半句（被消耗时给费）不生效。
    /// </para>
    ///
    /// <para>
    /// <b>根因</b>：<c>ResolveEnergyXValue()</c> 做两件事
    /// （<c>CardModel.cs:1117-1124</c>）：
    /// <list type="number">
    ///   <item><b>守卫</b>：<c>if (!EnergyCost.CostsX) throw ...</c> ——
    ///         本牌不带 X 费用，这一步会抛异常，所以<b>用不了</b>；</item>
    ///   <item><b>取值</b>：<c>Hook.ModifyXValue(CombatState, this, EnergyCost.CapturedXValue)</c>
    ///         —— <b>这才是「化学物X」生效的那一步</b>。</item>
    /// </list>
    /// 而后半句的 X 语义不同（= 被消耗时的当前能量，不是"打出时支付的费用"），
    /// 所以只能<b>绕开守卫、单独调第 2 步</b> —— 也就是直接调
    /// <c>Hook.ModifyXValue</c>，把"当前能量"当作原始值传进去。
    /// </para>
    ///
    /// <para>
    /// <b>为什么这是正确的语义</b>：「化学物X」的实现是
    /// <c>ModifyXValue(card, original) =&gt; original + 2</c>（仅校验 <c>Owner == card.Owner</c>，
    /// <b>并不检查 <c>CostsX</c></b>；<c>CostsX</c> 只在它的 <c>BeforeCardPlayed</c> 里
    /// 决定要不要闪光）。所以调这个钩子 ≠ 假装本牌是 X 费卡，
    /// 只是让"所有 X 修正"作用到本牌自己的 X 定义上 ——
    /// 与前半句经由 <c>ResolveEnergyXValue()</c> 走的是<b>同一个</b> <c>Hook.ModifyXValue</c>，
    /// 两半因此行为一致。
    /// </para>
    ///
    /// <para>
    /// <b>不要自己写 <c>if (HasChemicalX) +2</c></b> —— 那样只会认这一件遗物，
    /// 将来任何别的 X 修正（新遗物/能力）都得再改一次。
    /// 走钩子则自动继承全部修正。
    /// </para>
    /// </summary>
    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card != this || CombatState == null)
        {
            return;
        }

        // ★ X = 当前能量（先读，再给）。
        decimal currentEnergy = Owner.PlayerCombatState?.Energy ?? 0m;

        // ★★ 让所有"X 修正"作用到这个值上（「化学物X」+2 等）。
        //    与前半句经由 ResolveEnergyXValue() 走的是同一个 Hook.ModifyXValue。
        //    注意这里刻意**不用** ResolveEnergyXValue()：它开头有
        //    `if (!EnergyCost.CostsX) throw` 的守卫，而本牌不带 X 费用。
        int x = Hook.ModifyXValue(CombatState, this, (int)currentEnergy);

        // 升级后多给 1 点（设计里"X+1"的含义）。
        decimal energy = IsUpgraded ? x + 1m : x;

        if (energy <= 0m)
        {
            return;
        }

        await PlayerCmd.GainEnergy(energy, Owner);
    }

    /// <summary>
    /// 升级只提升<b>系数</b>（6 → 7），所以升级格挡变量。
    ///
    /// <para>
    /// 卡面上 <c>{Block:diff()}</c> 显示的也是这个系数
    /// （未升级 6、升级后 7），与实际结算一致。
    /// </para>
    ///
    /// <para>
    /// 后半句的 <c>X（X+1）</c> 不进升级 —— 它是写死的文案，
    /// 括号里的 +1 由这里的 <c>IsUpgraded</c> 在结算时体现，不靠变量。
    /// </para>
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}
