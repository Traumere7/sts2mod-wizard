using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using Wizard.Scripts.Character;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 修正宝箱房「拿取遗物」演出时的手/遗物遮挡图层（A 版与 B 版同时生效）。
///
/// <para>
/// <b>根因</b>：原版 <c>NHandImage.GrabRelic</c> 在手掌合拢成拳后调用
/// <c>holder.Reparent(this)</c>，把遗物节点追加为 <c>NHandImage</c> 的最后一个子节点
/// （排在 <c>TextureRect</c> 之后），导致同 <c>ZIndex</c> 下遗物贴图绘制在手背之上。
/// </para>
///
/// <para>
/// <b>修复</b>：在巫师的 <c>NHandImage._Ready</c> 订阅 <c>ChildEnteredTree</c>，
/// 每当 <c>NTreasureRoomRelicHolder</c> 挂入巫师手部节点时，延迟同帧调用
/// <c>MoveChild(child, 0)</c> 将其移至 <c>TextureRect</c> 之前。
/// <list type="bullet">
///   <item>同帧渲染前即完成重排，无单帧闪烁，且符合 Godot 4 的节点树锁定规范（零警告）；</item>
///   <item>保持 <c>ZIndex = 0</c>，不影响 <c>FightBackstop</c>、队友手部及金币特效的既有层级；</item>
///   <item>仅对巫师玩家生效，不改动原版或其它 Mod 角色的表现。</item>
/// </list>
/// </para>
/// </summary>
[HarmonyPatch(typeof(NHandImage), "_Ready")]
internal static class WizardTreasureHandLayerPatch
{
    private const string HookedMeta = "wizard_hand_layer_hooked";

    [HarmonyPostfix]
    private static void Postfix(NHandImage __instance)
    {
        try
        {
            if (!WizardCharacter.IsWizardPlayer(__instance.Player))
            {
                return;
            }

            if (__instance.HasMeta(HookedMeta))
            {
                return;
            }

            __instance.SetMeta(HookedMeta, true);
            __instance.ChildEnteredTree += (Node child) =>
            {
                if (child is NTreasureRoomRelicHolder
                    && GodotObject.IsInstanceValid(__instance)
                    && GodotObject.IsInstanceValid(child))
                {
                    __instance.CallDeferred(Node.MethodName.MoveChild, child, 0);
                }
            };
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 宝箱房手臂图层修正挂接失败：{e.Message}");
        }
    }
}
