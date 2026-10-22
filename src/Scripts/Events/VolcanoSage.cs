using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;          // EventOption 等
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Character;
using Wizard.Scripts.Encounters;
using Wizard.Scripts.Relics;

namespace Wizard.Scripts.Events;

/// <summary>
/// 火山贤者 / VolcanoSage —— 第二阶段事件，**多人合作：是**。
///
/// <para>
/// <b>规格</b>（<c>docs/design.md</c> 事件稿一节）：
/// <list type="bullet">
///   <item>出现阶段：第二阶段（<c>CurrentActIndex == 1</c>），触发条件：无</item>
///   <item>三个选项：
///     <list type="number">
///       <item>虚心求教：失去 8 点生命，升级 1 张牌</item>
///       <item>与之辩论：获得 256 金币，将 1 张「疑虑」加入卡组</item>
///       <item>我已领会：进入战斗（火山巨兽），固定奖励见 <see cref="Fight"/></item>
///     </list>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「事件里打一场」的实现照抄原版 <c>DenseVegetation</c> / <c>TheLanternKey</c>
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item><c>IsShared => true</c> —— <b>必须</b>：<c>EnterCombatWithoutExitingEvent</c>
///         在非共享事件里会直接抛 <c>InvalidOperationException</c>
///         （<c>EventModel.cs:573-576</c>）。design.md 也写了「多人合作：是」。</item>
///   <item>选项 3 先 <c>SetEventState(后续文案, [战斗按钮])</c>，
///         点「战斗」再 <c>EnterCombatWithoutExitingEvent(...)</c>
///         —— 与 design.md 的「后续：… / 战斗」两步结构一致。</item>
///   <item><c>shouldResumeAfterCombat: false</c>：打完直接走人，不回事件界面。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>不能把 <c>LayoutType</c> 设成 <c>EventLayoutType.Combat</c></b>：
/// 那样事件就没有可显示的选项页（<c>EventModel.cs:577-580</c> 明确禁止
/// "Combat 布局 + 战后返回"），而本事件**前面还有 3 个选项要显示**。
/// 保持默认布局即可。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 固定奖励（design.md 逐项写死）
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// 100 金币
/// + 一件随机商店遗物（若角色为巫师且尚未拥有「三原素」⇒ 固定给「三原素」）
/// + 一件随机稀有遗物
/// + 一次普通卡牌奖励
/// </code>
/// 通过 <c>EnterCombatWithoutExitingEvent</c> 的 <c>extraRewards</c> 一次性给出，
/// 同时把遭遇战的默认奖励关掉（<c>VolcanicBeastEncounter.ShouldGiveRewards => false</c>），
/// 否则会多出一份精英奖励。
/// </para>
///
/// <para>
/// <b>「普通卡牌奖励」</b>用 <c>CardCreationOptions.ForRoom(Owner, RoomType.Monster)</c>
/// —— 即按普通战斗的稀有度权重抽 3 张选 1，与原版战斗奖励口径一致。
/// </para>
/// </summary>
public sealed class VolcanoSage : CustomEventModel
{
    /// <summary>本事件的本地化键前缀（= <c>ModelId.Entry</c>）。</summary>
    private const string Key = "WIZARD-VOLCANO_SAGE";

    // ── 选项 1：失去 8 点生命 + 升级 1 张牌 ──
    private const int ConsultHpLoss = 8;
    private const int ConsultUpgradeCount = 1;

    // ── 选项 2：256 金币 + 1 张疑虑 ──
    private const int DebateGold = 256;
    private const int DebateCurseCount = 1;

    // ── 选项 3：战斗奖励 ──
    private const int FightGold = 100;
    /// <summary>普通卡牌奖励给几张候选（原版战斗奖励是 3 选 1）。</summary>
    private const int FightCardChoices = 3;

    public override string? CustomInitialPortraitPath => "res://Wizard/images/events/volcano_sage.png";

    /// <summary>design.md：多人合作 ⇒ 是。也是事件内进战斗的硬性前提。</summary>
    public override bool IsShared => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(ConsultHpLoss),
        new GoldVar(DebateGold),
    ];

    /// <summary>只在第二阶段出现（<c>Act 1 = 0，Act 2 = 1</c>）。</summary>
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1;

    // ══════════════════════════════════════════════════════════════════
    // 选项
    // ══════════════════════════════════════════════════════════════════
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            Option(Consult, ConsultTitle, ConsultDescription)
                .ThatDoesDamage(ConsultHpLoss),
            Option(Debate, DebateTitle, DebateDescription),
            Option(Defy, DefyTitle, DefyDescription),
        ];
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项 1：虚心求教 —— 失去 8 点生命，升级 1 张牌
    // ══════════════════════════════════════════════════════════════════
    /// <summary>
    /// <b>「失去 8 点生命」用 <c>ValueProp.Unblockable | Unpowered</c></b>
    /// （照抄原版 <c>DenseVegetation.TrudgeOn</c>）：<c>Unblockable</c> 的语义注释
    /// 就是"像中毒一样的生命损失"，格挡挡不住。
    /// </summary>
    private async Task Consult()
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        // ⚠️ 生命可能致死 —— 死了就别再开选牌界面（会盖在 GameOver 上）。
        if (player.Creature.IsDead)
        {
            return;
        }

        IEnumerable<CardModel> picked = await CardSelectCmd.FromDeckForUpgrade(
            prefs: new CardSelectorPrefs(
                CardSelectorPrefs.UpgradeSelectionPrompt, ConsultUpgradeCount),
            player: player);

        foreach (CardModel card in picked)
        {
            CardCmd.Upgrade(card);
        }

        SetEventFinished(L10NLookup($"{Key}.pages.CONSULT.description"));
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项 2：与之辩论 —— 256 金币 + 1 张疑虑进卡组
    // ══════════════════════════════════════════════════════════════════
    private async Task Debate()
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, player);

        for (int i = 0; i < DebateCurseCount; i++)
        {
            await CardPileCmd.AddCurseToDeck<MegaCrit.Sts2.Core.Models.Cards.Doubt>(player);
        }

        SetEventFinished(L10NLookup($"{Key}.pages.DEBATE.description"));
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项 3：我已领会 —— 显示后续文案 + 「战斗」按钮
    // ══════════════════════════════════════════════════════════════════
    private Task Defy()
    {
        SetEventState(
            L10NLookup($"{Key}.pages.DEFY.description"),
            [Option(Fight, FightButtonTitle, FightButtonDescription)]);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 进入战斗：先把 design.md 写死的四项奖励备好，再交给事件进战斗流程。
    /// </summary>
    private Task Fight()
    {
        Player? player = Owner;
        if (player == null)
        {
            return Task.CompletedTask;
        }

        List<Reward> rewards = [new GoldReward(FightGold, player)];

        // ── 随机商店遗物；巫师且没有「三原素」时固定给「三原素」──
        if (player.Character is WizardCharacter
            && !player.Relics.Any(r => r is TriaPrima))
        {
            // ⚠️ 必须 ToMutable()：RelicReward(RelicModel, Player) 内部会 AssertMutable()，
            //    直接传 canonical 模型会抛 CanonicalModelException（2026-10-22 实测踩到）。
            //    与「规则怪谈」里 RelicCmd.Obtain(....ToMutable(), player) 同理。
            rewards.Add(new RelicReward(ModelDb.Relic<TriaPrima>().ToMutable(), player));
        }
        else
        {
            rewards.Add(new RelicReward(RelicRarity.Shop, player));
        }

        // ── 随机稀有遗物 ──
        rewards.Add(new RelicReward(RelicRarity.Rare, player));

        // ── 一次普通卡牌奖励（3 选 1）──
        rewards.Add(new CardReward(
            CardCreationOptions.ForRoom(player, RoomType.Monster),
            FightCardChoices,
            player));

        EnterCombatWithoutExitingEvent<VolcanicBeastEncounter>(
            rewards, shouldResumeAfterCombat: false);

        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════
    // 本地化键
    // ══════════════════════════════════════════════════════════════════
    //
    // ⚠️ 必须走 `Option(onChosen, LocString title, LocString description)` 重载。
    //    只传一个字符串的重载会把参数当 **pageKey**（
    //    `CustomEventModel.cs:35-107`），导致运行时 "Missing localization key"。
    //    —— 这是本项目踩过的坑，见 CONTEXT.md。

    private static LocString Opt(string name, string field) =>
        new("events", $"{Key}.pages.INITIAL.options.{name}.{field}");

    private static LocString ConsultTitle => Opt("CONSULT", "title");
    private static LocString ConsultDescription => Opt("CONSULT", "description");
    private static LocString DebateTitle => Opt("DEBATE", "title");
    private static LocString DebateDescription => Opt("DEBATE", "description");
    private static LocString DefyTitle => Opt("DEFY", "title");
    private static LocString DefyDescription => Opt("DEFY", "description");

    /// <summary>战后那一页的「战斗」按钮文案。</summary>
    private static LocString FightButtonTitle =>
        new("events", $"{Key}.pages.DEFY.options.FIGHT.title");

    private static LocString FightButtonDescription =>
        new("events", $"{Key}.pages.DEFY.options.FIGHT.description");
}
