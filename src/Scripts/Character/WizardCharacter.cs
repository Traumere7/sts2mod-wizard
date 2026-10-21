using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using BaseLib.Abstracts;
using Wizard.Scripts.Cards;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Relics;

namespace Wizard.Scripts.Character;

/// <summary>
/// 巫师：一位钻研禁忌法术的施法者。
///
/// 目前继承 <see cref="PlaceholderCharacterModel"/>，即全部美术/音效资源都暂时借用
/// 原版铁甲战士（PlaceholderID 默认 "ironclad"）。等巫师自己的资源就绪后，
/// 逐个重写下面的 Custom* 属性即可平滑替换，不需要改动其它任何代码。
/// </summary>
public class WizardCharacter : PlaceholderCharacterModel
{
    /// <summary>角色的稳定 ID。</summary>
    public const string CharacterId = "Wizard";

    /// <summary>
    /// 本角色在 <c>ModelDb</c> 里的 <c>Id.Entry</c>。
    ///
    /// <para>
    /// 形如 <c>{命名空间根}-{类名的 UPPER_SNAKE}</c>，所以是
    /// <c>WIZARD-WIZARD_CHARACTER</c>（不是 <see cref="CharacterId"/>）。
    /// 与本地化键的前缀一致（<c>characters.json</c> 里就是
    /// <c>WIZARD-WIZARD_CHARACTER.title</c>）。
    /// </para>
    /// </summary>
    public const string CharacterEntry = "WIZARD-WIZARD_CHARACTER";

    /// <summary>
    /// 判断某个玩家是不是<b>巫师</b>。
    ///
    /// <para>
    /// ⚠️ <b>多人游戏必须用它，不要用 <c>creature.Entity.Player != null</c> 就动手</b> ——
    /// 那个只说明"这是某个玩家"，队友也是玩家。凡是"只改巫师自己外观/行为"的补丁
    /// 都要再过这一层。
    /// </para>
    ///
    /// <para>
    /// 比 <c>player.Character is WizardCharacter</c> 更稳：<c>Id.Entry</c> 是
    /// 引擎的稳定标识，不受将来换基类 / 换占位实现影响。
    /// </para>
    /// </summary>
    public static bool IsWizardPlayer(Player? player)
        => player?.Character?.Id.Entry == CharacterEntry;

    /// <summary>巫师的奥术紫主题色。</summary>
    public static readonly Color ArcanePurple = new(0.55f, 0.35f, 0.95f);

    // ── 文案颜色 ──
    /// <summary>角色名称颜色。</summary>
    public override Color NameColor => ArcanePurple;

    /// <summary>能量图标轮廓颜色。</summary>
    public override Color EnergyLabelOutlineColor => new Color("3A1E7AFF");

    /// <summary>地图上角色标记的绘制颜色。</summary>
    public override Color MapDrawingColor => ArcanePurple;

    // ── 基础属性 ──
    /// <summary>性别：中立。</summary>
    public override CharacterGender Gender => CharacterGender.Neutral;

    /// <summary>初始最大生命。</summary>
    public override int StartingHp => 75;

    /// <summary>初始金币。</summary>
    public override int StartingGold => 99;

    // ── 场景资源 ──
    // 下面这些覆盖掉铁甲战士的占位资源，指向巫师自己的场景。
    // 场景内部目前仍使用 icon.svg 占位图，替换场景里的贴图即可完成美术替换。

    /// <summary>战斗人物场景。</summary>
    public override string CustomVisualPath => "res://Wizard/scenes/wizard_character.tscn";

    /// <summary>能量表盘场景。</summary>
    public override string CustomEnergyCounterPath => "res://Wizard/scenes/wizard_energy_counter.tscn";

    /// <summary>商店人物场景。</summary>
    public override string CustomMerchantAnimPath => "res://Wizard/scenes/wizard_character_merchant.tscn";

    /// <summary>篝火休息场景。</summary>
    public override string CustomRestSiteAnimPath => "res://Wizard/scenes/wizard_character_rest_site.tscn";

    /// <summary>角色选择背景。</summary>
    public override string CustomCharacterSelectBg => "res://Wizard/scenes/wizard_bg.tscn";

    /// <summary>游戏左上角头像 / 角色统计页头像 / 每日挑战头像。</summary>
    public override string CustomIconPath => "res://Wizard/scenes/wizard_icon.tscn";

    /// <summary>人物头像（自适应大小）。</summary>
    public override string? CustomIconTexturePath => "res://Wizard/images/ui/char_icon_wizard.png";

    /// <summary>角色选择界面图标。</summary>
    public override string? CustomCharacterSelectIconPath => "res://Wizard/images/ui/char_select_wizard.png";

    /// <summary>角色选择界面图标（未解锁状态）。</summary>
    public override string? CustomCharacterSelectLockedIconPath => "res://Wizard/images/ui/char_select_wizard_locked.png";

    /// <summary>地图上的角色标记图标、表情轮盘头像。</summary>
    public override string? CustomMapMarkerPath => "res://Wizard/images/ui/map_marker_wizard.png";

    // ── 三卡池 ──
    public override CardPoolModel CardPool => ModelDb.CardPool<WizardCardPool>();

    public override RelicPoolModel RelicPool => ModelDb.RelicPool<WizardRelicPool>();

    public override PotionPoolModel PotionPool => ModelDb.PotionPool<WizardPotionPool>();

    // ── 初始卡组：4 打击 + 4 防御 + 1 火球术 + 1 镇静 ──
    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<StrikeWizard>(),
        ModelDb.Card<StrikeWizard>(),
        ModelDb.Card<StrikeWizard>(),
        ModelDb.Card<StrikeWizard>(),
        ModelDb.Card<DefendWizard>(),
        ModelDb.Card<DefendWizard>(),
        ModelDb.Card<DefendWizard>(),
        ModelDb.Card<DefendWizard>(),
        ModelDb.Card<Fireball>(),
        ModelDb.Card<CalmMind>(),
    ];

    // ── 初始遗物：魔法卷轴（战斗开始时把 1 张「思索」加入手牌）──
    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<SpellScroll>(),
    ];

    // ── 动画对齐延迟（沿用铁甲战士占位值）──
    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    // ── 攻击建筑师时的特效列表 ──
    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shards",
    ];
}
