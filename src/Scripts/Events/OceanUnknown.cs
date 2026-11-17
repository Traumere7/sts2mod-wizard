using System.Linq;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using Wizard.Scripts.Cards;
using Wizard.Scripts.Enchantments;
using Wizard.Scripts.Relics;

namespace Wizard.Scripts.Events;

/// <summary>
/// 未知海洋 / OceanUnknown —— 第三阶段事件，单人专属。
///
/// <para>
/// design.md 的口径：
/// <code>
/// 出现阶段：第三阶段      ⇒ IsAllowed 判 runState.CurrentActIndex == 2（Act1=0/2=1/3=2）
/// 多人合作：否            ⇒ 不重写 IsShared（默认 false）
/// 触发条件：卡组里至少 1 张可附魔「不朽」的合法目标
///                           ⇒ 不满足就整个事件不出现（与「坟冢」GraveOfTheForgotten 同做法）
/// </code>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 两个选项
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>岸边休息</b>：获得遗物「水蓝之心」<see cref="AquaHeart"/>；</item>
///   <item><b>纵身一跃</b>：把一张「梦魇」<see cref="Nightmare"/> 加入牌组，
///         并给牌组里的一张牌附魔「不朽」<see cref="Immortal"/>。</item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "选一张牌附魔"照抄原版事件「坟冢」<c>GraveOfTheForgotten</c>
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// await CardPileCmd.AddCurseToDeck&lt;Decay&gt;(Owner);
/// CardModel card = (await CardSelectCmd.FromDeckForEnchantment(
///     Owner, ModelDb.Enchantment&lt;SoulsPower&gt;(), 1,
///     new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1))).FirstOrDefault();
/// CardCmd.Enchant&lt;SoulsPower&gt;(card, 1m);
/// </code>
/// <c>FromDeckForEnchantment</c> 内部已经用 <c>CanEnchant</c> 过滤过合法目标，
/// 所以不需要我们再筛一遍。
/// </para>
///
/// <para>
/// ⚠️ <b>选项文案必须走 <c>Option(onChosen, LocString title, LocString description, ...)</c> 这个重载</b>
/// （四个重载的坑见 <see cref="RulesHorror"/> 的注释）：
/// 传 <c>string</c> 会被当成 <b>pageKey</b>，键会拼错，运行期只打一条 Missing localization 警告。
/// </para>
/// </summary>
public sealed class OceanUnknown : CustomEventModel
{
    private const string Key = "WIZARD-OCEAN_UNKNOWN";

    /// <summary>事件立绘（用户提供的原图，文件名保持大写）。</summary>
    public override string? CustomInitialPortraitPath => "res://Wizard/images/events/OceanUnknown.png";

    // ══════════════════════════════════════════════════════════════════
    // 文案里引用的名字（遗物 / 诅咒 / 附魔）
    // ══════════════════════════════════════════════════════════════════
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("Relic", ModelDb.Relic<AquaHeart>().Title.GetFormattedText()),
        new StringVar("Curse", ModelDb.Card<Nightmare>().Title),
        new StringVar("Enchantment", ModelDb.Enchantment<Immortal>().Title.GetFormattedText()),
    ];

    // ══════════════════════════════════════════════════════════════════
    // 出现条件：第三阶段 + 每个玩家都有可附魔「不朽」的牌
    // ══════════════════════════════════════════════════════════════════
    public override bool IsAllowed(IRunState runState)
    {
        if (runState.CurrentActIndex != 2)
        {
            return false;
        }

        // 单人局等价于判自己；多人时保守取交集（本事件本就设计为单人）。
        return runState.Players.All(HasImmortalTarget);
    }

    /// <summary>牌组里是否有可附魔「不朽」的牌。</summary>
    private static bool HasImmortalTarget(Player player)
    {
        EnchantmentModel immortal = ModelDb.Enchantment<Immortal>();
        return PileType.Deck.GetPile(player).Cards.Any(c => immortal.CanEnchant(c));
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项
    // ══════════════════════════════════════════════════════════════════
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Option(ChooseRest, RestTitle, RestDescription, HoverTipFactory.FromRelic<AquaHeart>().ToArray()),
        Option(
            ChooseDive,
            DiveTitle,
            DiveDescription,
            HoverTipFactory.FromCardWithCardHoverTips<Nightmare>()
                .Concat(HoverTipFactory.FromEnchantment<Immortal>())
                .ToArray()),
    ];

    private static LocString RestTitle => Opt("REST", "title");

    private static LocString RestDescription => Opt("REST", "description");

    private static LocString DiveTitle => Opt("DIVE", "title");

    private static LocString DiveDescription => Opt("DIVE", "description");

    /// <summary>拼出 <c>{Entry}.pages.INITIAL.options.{name}.{field}</c> 的 LocString。</summary>
    private static LocString Opt(string name, string field) =>
        new("events", $"{Key}.pages.INITIAL.options.{name}.{field}");

    // ══════════════════════════════════════════════════════════════════
    // 选项 1：岸边休息 —— 获得水蓝之心
    // ══════════════════════════════════════════════════════════════════
    private async Task ChooseRest()
    {
        if (Owner == null)
        {
            return;
        }

        await RelicCmd.Obtain<AquaHeart>(Owner);
        SetEventFinished(L10NLookup($"{Key}.pages.REST.description"));
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项 2：纵身一跃 —— 加一张梦魇，再给一张牌附魔「不朽」
    // ══════════════════════════════════════════════════════════════════
    private async Task ChooseDive()
    {
        if (Owner == null)
        {
            return;
        }

        // ① 先塞诅咒（与原版「坟冢」same order：先给代价，再给收益）。
        await CardPileCmd.AddCurseToDeck<Nightmare>(Owner);

        // ② 从牌组里选 1 张合法目标附魔。
        CardModel? picked = (await CardSelectCmd.FromDeckForEnchantment(
                Owner,
                ModelDb.Enchantment<Immortal>(),
                1,
                new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1)))
            .FirstOrDefault();

        if (picked != null)
        {
            CardCmd.Enchant<Immortal>(picked, 1m);

            // ③ 附魔演出（原版「坟冢」同款）。
            NCardEnchantVfx? vfx = NCardEnchantVfx.Create(picked);
            if (vfx != null)
            {
                NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
            }
        }

        SetEventFinished(L10NLookup($"{Key}.pages.DIVE.description"));
    }
}
