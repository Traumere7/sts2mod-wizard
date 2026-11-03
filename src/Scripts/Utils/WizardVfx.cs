using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Wizard.Scripts.Utils;

/// <summary>
/// 从<b>原版游戏</b>借用的演出片段（不新增任何美术资源）。
///
/// <para>
/// 这里每个方法都是"原版某张卡/某个球的演出"的忠实复刻，
/// 并在注释里写明<b>出处文件</b>，方便以后对照原版改动。
/// </para>
/// </summary>
public static class WizardVfx
{
    /// <summary>
    /// 原版「末日降临」<c>EndOfDays</c> 的配色（深绯）。
    /// 出处：<c>MegaCrit.Sts2.Core.Models.Cards/EndOfDays.cs</c> 里的 <c>new Color("8c2447")</c>。
    /// </summary>
    public static readonly Color EndOfDaysTint = new("8c2447");

    /// <summary>
    /// 原版故障机器人「闪电球」激发时打在敌人身上的打击特效。
    ///
    /// <para>
    /// 出处：<c>MegaCrit.Sts2.Core.Models.Orbs/LightningOrb.cs</c> 的
    /// <c>ApplyLightningDamage</c>：
    /// <code>
    /// foreach (Creature item in targets)
    ///     VfxCmd.PlayOnCreature(item, "vfx/vfx_attack_lightning");
    /// </code>
    /// 被动与激发（<c>isEvoke</c>）走的是同一个特效，所以这个常量两处都适用。
    /// </para>
    /// </summary>
    public const string LightningOrbImpact = "vfx/vfx_attack_lightning";

    /// <summary>
    /// 原版故障机器人「闪电球」<b>激发</b>时的音效。
    ///
    /// <para>
    /// 出处：<c>LightningOrb.cs</c> 的 <c>protected override string EvokeSfx</c>：
    /// <code>
    /// protected override string EvokeSfx => "event:/sfx/characters/defect/defect_lightning_evoke";
    /// </code>
    /// （同一个类里还有 <c>PassiveSfx</c> = <c>..._passive</c>、<c>ChannelSfx</c> = <c>..._channel</c>，
    /// 我们只要激发那一个。）
    /// </para>
    ///
    /// <para>
    /// 传给 <c>AttackCommand.WithHitFx(vfx, sfx)</c> 的第二个参数，内部走 <c>SfxCmd.Play(HitSfx)</c>。
    /// 原版怪物攻击也这么用（如 <c>ByrdSwoop</c>：
    /// <c>.WithHitFx("vfx/vfx_attack_slash", "event:/sfx/byrdpip/byrdpip_attack")</c>）。
    /// </para>
    /// </summary>
    public const string LightningOrbEvokeSfx = "event:/sfx/characters/defect/defect_lightning_evoke";

    /// <summary>
    /// 原版「末日降临」<c>EndOfDays</c> 的演出：
    /// 在<b>敌方半场中心的地面</b>砸下一枚大型魔法飞弹，等它演完再继续结算。
    ///
    /// <para>
    /// 出处：<c>EndOfDays.OnPlay</c>
    /// <code>
    /// Vector2? sideCenterFloor = VfxCmd.GetSideCenterFloor(CombatSide.Enemy, base.CombatState);
    /// if (sideCenterFloor.HasValue) {
    ///     NLargeMagicMissileVfx vfx = NLargeMagicMissileVfx.Create(sideCenterFloor.Value, new Color("8c2447"));
    ///     if (vfx != null) {
    ///         NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(vfx);
    ///         await Cmd.Wait(vfx.WaitTime);
    ///     }
    /// }
    /// </code>
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>任何异常都不能让卡牌结算崩掉</b>：演出只是表现层，
    /// 这里整段 <c>try/catch</c> 掉，出错只写日志、照常继续结算。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>没有战斗房间时（例如卡牌库预览）直接返回</b>：
    /// <c>NCombatRoom.Instance</c> 为 null，硬挂会留下孤儿节点。
    /// </para>
    /// </summary>
    /// <param name="combatState">当前战斗状态；为 null 时不做任何事。</param>
    /// <param name="tint">飞弹配色；默认用末日降临那一档深绯。</param>
    public static async Task PlayLargeMagicMissile(ICombatState? combatState, Color? tint = null)
    {
        if (combatState == null)
        {
            return;
        }

        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            if (room == null)
            {
                return;
            }

            Vector2? floor = VfxCmd.GetSideCenterFloor(CombatSide.Enemy, combatState);
            if (floor is not { } position)
            {
                return;
            }

            NLargeMagicMissileVfx? vfx = NLargeMagicMissileVfx.Create(position, tint ?? EndOfDaysTint);
            if (vfx == null)
            {
                return;
            }

            room.CombatVfxContainer.AddChildSafely(vfx);
            await Cmd.Wait(vfx.WaitTime);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：大型魔法飞弹播放失败（不影响结算）：{e}");
        }
    }
}
