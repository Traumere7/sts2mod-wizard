using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Character;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 远古残章 - 2(1) 费 - 能力 - 先古
/// 固有。敌人每具有 1 种负面效果，你对其造成的伤害就提升 25%（**可叠加**：两张 = 50%）。
///
/// <para>
/// <b>触发来源</b>：先古之民达弗 <c>Darv</c> 选项三「尘封魔典」<c>DustyTome</c>
/// 给予本牌，且按先古惯例给的是<b>已升级</b>版本（即「远古残章+」，1 费）。
/// </para>
///
/// <para>
/// 与「落日」同属先古卡，稀有度 <c>CardRarity.Ancient</c> ——
/// 该稀有度<b>不会被随机奖励 roll 到</b>
/// （<c>CardFactory.RollForRarity</c> 只走 Common/Uncommon/Rare/Shop），
/// 所以放进本角色卡池是安全的，不会污染常规掉落。
/// </para>
///
/// <para>
/// 效果由 <see cref="AncientFragmentPower"/> 承载。
/// </para>
///
/// <para>
/// <b>「固有」是初始就有的</b>：直接列在 <c>CanonicalKeywords</c>。
/// 升级改的是<b>费用 2 → 1</b>。
/// </para>
///
/// <para>
/// ⚠️ <b>本条历史（别再走回头路）</b>：曾经是"升级加固有、费用恒为 2"，
/// 更早还试过"升级降费但不加固有"。设计最终定为
/// <b>固有常驻 + 升级降费</b>。
/// </para>
///
/// <para>
/// <b>本地化文案里不要写「固有」</b> —— 关键字由引擎渲染成卡面词条。
/// </para>
///
/// <para>
/// <b>达弗「尘封魔典」<c>DustyTome</c> 给本牌</b> ——
/// 靠类声明上的 <see cref="ITomeCard"/>，见
/// <see cref="TomeCharacter"/> 的说明。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class AncientFragment : WizardCardModel, ITomeCard
{
    private const int Cost = 2;

    /// <summary>
    /// 告诉「尘封魔典」<c>DustyTome</c>：它是给**巫师**的。
    ///
    /// <para>
    /// <b>不需要写 Harmony。</b> BaseLib 已有 <c>DustyTomePatch</c> ——
    /// 它对原版 <c>DustyTome.SetupForPlayer</c> 加了 <c>[HarmonyPrefix]</c>：
    /// 扫描所有 <see cref="ITomeCard"/> 实现、按 <c>TomeCharacter</c> 归组，
    /// <b>只要该角色有一组就直接接管</b>（Prefix 返回 false，原版逻辑整段跳过），
    /// 从这组里随机取一张。
    /// </para>
    ///
    /// <para>
    /// <b>这正是"魔典只给远古残章"的实现方式：</b>巫师这组里只有本牌，
    /// 于是随机池退化成唯一解。而「落日」虽然同为 <c>Ancient</c> 稀有度，
    /// 但因为它是「古老牙齿」的替换目标（<see cref="Fireball"/> 实现了
    /// <c>ITranscendenceCard</c>），会被原版筛选条件
    /// <c>!ArchaicTooth.TranscendenceCards.Contains(c)</c> 排除掉 ——
    /// 两条路径互不干扰。
    /// </para>
    ///
    /// <para>
    /// 不重写这个属性也行：<see cref="ITomeCard"/> 的默认实现会反查
    /// "哪个人物的卡池含有本牌"。这里显式写出来是为了让它**不依赖卡池巧合**，
    /// 语义也更直白。
    /// </para>
    /// </summary>
    public CharacterModel TomeCharacter => ModelDb.Character<WizardCharacter>();

    public AncientFragment()
        : base(Cost, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
    }

    /// <summary>「固有」是初始就有的词条，直接声明（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Innate,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加数量固定 1；叠层由引擎累加，实际加成 = 每层 25% × 层数。
        await PowerCmd.Apply<AncientFragmentPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>升级：费用 2 → 1（「固有」本来就有，不需要在这里加）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
