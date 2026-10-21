using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 超导 - 1 费 - 能力 - 罕见
/// **你对**寒冷状态的敌人造成的雷电伤害提升 50%（升级 75%）。
/// 多张**加算**：2 张 = +100%。
///
/// <para>
/// 这张牌同时承担一个工程作用：**让巫师卡池里存在至少一张能力牌**。
/// 商店固定生成 5 个卡位，其中 1 个必须是能力牌
/// （<c>MerchantInventory._coloredCardTypes = [Attack, Attack, Skill, Skill, Power]</c>），
/// 池子里没有能力牌会导致进商店时抛
/// "Can't generate valid rarity for merchant card type Power" → **商店黑屏**。
/// </para>
///
/// <para>
/// ⚠️ 卡面文案的变量名必须是 <c>SuperconductPower</c>
/// （<c>PowerVar&lt;T&gt;</c> 用 <c>typeof(T).Name</c>），
/// 且用 <c>:diff()</c> 直接显示**百分点数**（50 → 50）。
/// <list type="bullet">
///   <item><b>不能</b>写成 <c>{LightningIncrease}</c> —— 那是**能力自己**的变量，
///         卡牌作用域里没有它，会原样输出变量名。</item>
///   <item><b>不要</b>用 <c>:percentMore()</c> —— 它算的是 <c>(v-1)×100</c>，
///         传百分点数 50 会得到 <c>4900</c>。</item>
/// </list>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Superconduct : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 存**百分点数**（50 = +50%），照抄原版「残酷」Cruelty 的
        // PowerVar<CrueltyPower>(25m) 写法。
        //
        // 卡面文案用 {SuperconductPower:diff()}%（不是 :percentMore()）——
        // 后者算的是 (v-1)×100，传百分点数会得到负数。
        new PowerVar<SuperconductPower>(SuperconductPower.BaseIncreasePercent),
    ];

    /// <summary>
    /// 悬停提示只给**机制关键词**（寒冷），不给「超导」自己的能力提示。
    ///
    /// 原因：<c>HoverTipFactory.FromPower&lt;T&gt;()</c> 走的是 <c>GetDumbHoverTip</c>，
    /// 它按设计**不注入自定义变量**，能力文案里的 <c>{Amount}%</c>
    /// 会原样显示成占位符。
    /// 原版同类型的效果（如「火焰屏障」<c>FlameBarrierPower</c>）也是**不给自己的效果做卡面提示**。
    ///
    /// 注意：卡面**文案本身**仍然会正确显示「提升50%」——
    /// 那是走 <c>CardModel.GetDescriptionForPile</c> 注入卡牌自己的变量，不受此限制。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    public Superconduct()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加的数量就是**本张卡贡献的百分点数**（50，升级后 75）。
        // 引擎对已存在实例走 ModifyAmount —— 于是多张自动累加，
        // 混合升级也各贡献各的（50+50+75 = 175）。
        await PowerCmd.Apply<SuperconductPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(SuperconductPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 本张卡贡献 50 → 75 个百分点
        DynamicVars[nameof(SuperconductPower)].UpgradeValueBy(SuperconductPower.UpgradeStep);
    }
}
