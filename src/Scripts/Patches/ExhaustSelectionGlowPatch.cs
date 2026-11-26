using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Wizard.Scripts.Cards;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 让「被消耗有额外收益」的牌在**「选择消耗的卡牌」**选牌界面里金边亮起 ——
/// 对齐原版「奇巧」<c>Sly</c> 在"选择丢弃的卡牌"时边缘亮起的体验。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 原版机制 &amp; 缺的那一环
/// ══════════════════════════════════════════════════════════════════
/// 手牌金边由 <c>NHandCardHolder.ShouldGlowGold</c> 决定
/// （<c>NHandCardHolder.cs:289-312</c>）：
/// <list type="number">
///   <item>若 <c>NPlayerHand.SelectModeGoldGlowOverride</c> 非空 ⇒ 直接用这个谓词；</item>
///   <item>否则退回 <c>CardModel.ShouldGlowGold</c>（卡牌自己的金边逻辑）。</item>
/// </list>
/// 而 <c>SelectModeGoldGlowOverride</c> 就是 <c>NPlayerHand._prefs.ShouldGlowGold</c>
/// （<c>NPlayerHand.cs:518</c>）。
/// 原版只在"丢弃"等少数选择里显式设置该谓词，
/// <b>没有任何地方为 <c>TO_EXHAUST</c> 设置它</b> ⇒ 消耗选择时本来不会亮。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 本补丁的做法
/// ══════════════════════════════════════════════════════════════════
/// 给 <c>SelectModeGoldGlowOverride</c> 的 getter 加 Postfix：
/// <list type="number">
///   <item>原有谓词非空 ⇒ 完全不动（保留引擎/其它 mod 的行为）；</item>
///   <item>否则读取 <c>NPlayerHand._prefs.Prompt</c>，若正是
///         <c>card_selection / TO_EXHAUST</c>，就返回我们的谓词：
///         <c>card is WizardCardModel w &amp;&amp; w.GlowsOnExhaustSelection</c>。</item>
/// </list>
/// 因此**所有**来源的消耗选择（本 mod 的「占卜」「顿悟」「沉思」「真理派对」，
/// 以及原版「烙印」「燃契」等）都会让「尾注」「心流」亮起。
/// </para>
///
/// <para>
/// ⚠️ 为什么改 patch 点：<c>CardSelectCmd.FromHand</c> 是 async 方法，
/// 用 <c>ref</c> 前缀修改 <c>CardSelectorPrefs</c>（struct）在 Harmony 下
/// 依赖状态机字段回写，行为不够确定；而这个是**同步属性 getter**，
/// 且读的正是选牌时真正生效的那份 <c>_prefs</c>，最稳。
/// </para>
///
/// <para>
/// ⚠️ <c>_prefs</c> 是私有 struct 字段，用 <see cref="AccessTools.FieldRefAccess{T,F}(string)"/>
/// 读取；全程 try/catch，异常只记日志、不影响选牌。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.SelectModeGoldGlowOverride), MethodType.Getter)]
internal static class ExhaustSelectionGlowPatch
{
    private const string CardSelectionTable = "card_selection";

    private const string ExhaustPromptKey = "TO_EXHAUST";

    /// <summary>读 <c>NPlayerHand._prefs</c>（私有字段）。</summary>
    private static readonly AccessTools.FieldRef<NPlayerHand, CardSelectorPrefs> PrefsRef =
        AccessTools.FieldRefAccess<NPlayerHand, CardSelectorPrefs>("_prefs");

    /// <summary>只在"消耗选择"且没有其它谓词时，补上「被消耗有收益」的高亮。</summary>
    [HarmonyPostfix]
    private static void Postfix(NPlayerHand __instance, ref Func<CardModel, bool>? __result)
    {
        try
        {
            if (__result != null)
            {
                return;
            }

            LocString? prompt = PrefsRef(__instance).Prompt;
            if (prompt == null
                || prompt.LocTable != CardSelectionTable
                || prompt.LocEntryKey != ExhaustPromptKey)
            {
                return;
            }

            __result = static card => card is WizardCardModel wizard && wizard.GlowsOnExhaustSelection;
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard/ExhaustSelectionGlow] 读取选牌 prompt 失败（不影响选牌）：{e.Message}");
        }
    }
}
