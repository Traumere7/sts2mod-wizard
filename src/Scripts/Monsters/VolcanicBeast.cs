using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.NodeFactories;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

// ⚠️ 原版状态牌「灼伤」的类名就叫 Burn，与本 mod 的 BurnPower（燃烧，减益）不是一回事。
using BurnCard = MegaCrit.Sts2.Core.Models.Cards.Burn;

namespace Wizard.Scripts.Monsters;

/// <summary>
/// 火山巨兽 / VolcanicBeast —— 「火山贤者」事件限定的精英怪。
///
/// <para>
/// <b>规格</b>（<c>docs/design.md</c> 特殊怪物一节）：
/// <list type="bullet">
///   <item>生命 <b>144 / 154</b>（进阶 8+ 为 154）</item>
///   <item>初始能力：<b>免疫燃烧</b> <see cref="BurnImmunityPower"/>、<b>喷火</b> <see cref="FireSpitPower"/>（初始 3）</item>
///   <item>状态机 <c>1 → 2 → 3 → 1</c> 循环：
///     <list type="number">
///       <item>打 3×4，给予玩家 喷火层数 的燃烧 + 1 层易伤</item>
///       <item>打 24</item>
///       <item>给玩家弃牌堆塞 3 张「灼伤」，力量 +4，喷火 +2</item>
///     </list>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>只有生命按进阶缩放</b>。design.md 明确写了「144/154（进阶8+）」，
/// 但**没有**给伤害标注进阶值 ⇒ 3、3×4 与 24 **保持恒定**，
/// 不套用原版 <c>DeadlyEnemies</c> 的 +伤害惯例。
/// 「不要随意修改设计」优先于「与原版惯例一致」。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 实现骨架来自 BaseLib 教程「11 - Adding Monsters」+ 原版 <c>KinPriest</c>
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item>生命：<c>AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 154, 144)</c></item>
///   <item>视觉：<c>CreateCustomVisuals()</c> + <c>NodeFactory&lt;NCreatureVisuals&gt;.CreateFromScene(...)</c>
///         —— 占位美术是**一张静态贴图**，所以场景里用 <c>Sprite2D</c>（详见 tscn 注释）</item>
///   <item>初始能力：<c>AfterAddedToRoom()</c> 里 <c>MonsterActions.ApplySelf&lt;T&gt;</c></item>
///   <item>状态机：<c>MoveState(id, exec, params AbstractIntent[])</c> + <c>FollowUpState</c> 串成环，
///         最后 <c>new MonsterMoveStateMachine([...], 初始状态)</c></item>
/// </list>
/// </para>
///
/// <para>
/// <b>「喷火层数的燃烧」是动态值</b>：出招时读
/// <c>Creature.GetPowerAmount&lt;FireSpitPower&gt;()</c>
/// （<c>Creature.cs:596-599</c>），而不是固定的 3 ——
/// 所以第 3 招之后它会从 3 涨到 5，下一轮的燃烧也随之变多。
/// </para>
///
/// <para>
/// <b>燃烧走 <c>SpellCmd.ApplyBurn</c></b>：那是本 mod 所有燃烧施加的唯一收口，
/// 会正确处理"取高值不叠加"与 <c>WizardEvents.BurnApplied</c>（「纵火狂」等订阅）。
/// 注意怪物自己带 <see cref="BurnImmunityPower"/>，所以**它免疫的是"被上燃烧"**，
/// 不影响它给别人上燃烧。
/// </para>
///
/// <para>
/// <b>「灼伤」进弃牌堆</b>照抄原版「超频」<c>Overclock</c>：
/// <c>CombatState.CreateCard&lt;Burn&gt;(player)</c> +
/// <c>CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player)</c>。
/// </para>
/// </summary>
public class VolcanicBeast : CustomMonsterModel
{
    // ── 生命（design.md：144 / 154 进阶8+）──
    private const int HpNormal = 144;
    private const int HpHard = 154;

    // ── 招式 1：打 3×4 + 喷火层数的燃烧 + 1 层易伤 ──
    private const int ClawDamage = 3;
    private const int ClawHits = 4;
    private const int ClawVulnerable = 1;

    // ── 招式 2：打 24 ──
    private const int SmashDamage = 24;

    // ── 招式 3：塞 3 张灼伤 + 力量 +4 + 喷火 +2 ──
    private const int BurnCardsAdded = 3;
    private const int StrengthGain = 4;
    private const int FireSpitGain = 2;

    // ── 初始喷火层数 ──
    private const int FireSpitInitial = 3;

    // ── 招式 ID（= 本地化键 monsters.json 里的 moves.{ID}.title）──
    private const string MoveFlameClaw = "FLAME_CLAW";
    private const string MoveMagmaSmash = "MAGMA_SMASH";
    private const string MoveEruption = "ERUPTION";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HpHard, HpNormal);

    public override int MaxInitialHp => MinInitialHp;

    /// <summary>
    /// 占位视觉：一个 <c>NCreatureVisuals</c> 场景，内部是**单张 Sprite2D 贴图**。
    /// 场景路径见 <c>src/Wizard/scenes/monsters/volcanic_beast.tscn</c>。
    /// </summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        NodeFactory<NCreatureVisuals>.CreateFromScene("res://Wizard/scenes/monsters/volcanic_beast.tscn");

    /// <summary>
    /// 进战斗时挂上初始能力：免疫燃烧 + 喷火 3。
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await MonsterActions.ApplySelf<BurnImmunityPower>(this, 1m);
        await MonsterActions.ApplySelf<FireSpitPower>(this, FireSpitInitial);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 1. 打 3×4，给玩家喷火层数的燃烧 + 1 层易伤
        MoveState flameClaw = new(
            MoveFlameClaw,
            FlameClawMove,
            new MultiAttackIntent(ClawDamage, ClawHits),
            new DebuffIntent());

        // 2. 打 24
        MoveState magmaSmash = new(
            MoveMagmaSmash,
            MagmaSmashMove,
            new SingleAttackIntent(SmashDamage));

        // 3. 塞灼伤 + 力量 +4 + 喷火 +2
        MoveState eruption = new(
            MoveEruption,
            EruptionMove,
            new StatusIntent(BurnCardsAdded),
            new BuffIntent());

        // 循环：1 → 2 → 3 → 1
        flameClaw.FollowUpState = magmaSmash;
        magmaSmash.FollowUpState = eruption;
        eruption.FollowUpState = flameClaw;

        return new MonsterMoveStateMachine([flameClaw, magmaSmash, eruption], flameClaw);
    }

    // ══════════════════════════════════════════════════════════════════
    // 招式 1：火焰爪击
    // ══════════════════════════════════════════════════════════════════
    private async Task FlameClawMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ClawDamage)
            .WithHitCount(ClawHits)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        // 喷火的层数就是这次给多少燃烧（动态读取，第 3 招后会变多）。
        int burn = Creature.GetPowerAmount<FireSpitPower>();

        foreach (Creature target in targets)
        {
            if (!target.IsAlive)
            {
                continue;
            }

            if (burn > 0)
            {
                await SpellCmd.ApplyBurn(
                    new ThrowingPlayerChoiceContext(), target, burn, Creature, null);
            }

            await PowerCmd.Apply<VulnerablePower>(
                new ThrowingPlayerChoiceContext(), target, ClawVulnerable, Creature, null);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // 招式 2：熔岩重击
    // ══════════════════════════════════════════════════════════════════
    private async Task MagmaSmashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SmashDamage)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    // ══════════════════════════════════════════════════════════════════
    // 招式 3：喷发
    // ══════════════════════════════════════════════════════════════════
    private async Task EruptionMove(IReadOnlyList<Creature> targets)
    {
        // ① 给每个玩家的弃牌堆塞 3 张「灼伤」，并播放"塞牌 + 展示"的演出。
        //
        // ⚠️ 2026-10-22 修：第一版用了裸的 AddGeneratedCardToCombat，牌是塞进去了，
        //    但**玩家什么都看不到**。参考原版「史莱姆狂战士」SlimedBerserker.cs:70：
        //        await CardPileCmd.AddToCombatAndPreview<Slimed>(targets, PileType.Discard, 10, null);
        //    它会逐张塞入后调 CardCmd.PreviewCardPileAdd + Cmd.Wait(1f)，
        //    即"牌飞进弃牌堆 + 停留展示"的完整演出（原版「无厌沙虫」
        //    TheInsatiable.cs:133-146 是同一套做法的多目标手写版）。
        // 音效：占位阶段没有自己的音源，先不播（借用别怪物的音效会显得很怪）。
        // 等正式素材到位，可在这里 SfxCmd.Play(...) 或重写 CastSfx。
        await CardPileCmd.AddToCombatAndPreview<BurnCard>(
            targets, PileType.Discard, BurnCardsAdded, null, CardPilePosition.Bottom);

        // ② 自身 力量 +4、喷火 +2。
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), Creature, StrengthGain, Creature, null);

        await PowerCmd.Apply<FireSpitPower>(
            new ThrowingPlayerChoiceContext(), Creature, FireSpitGain, Creature, null);
    }
}
