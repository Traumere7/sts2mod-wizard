using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Utils;

/// <summary>
/// 「随机负面效果」的候选集合与施加入口（「孔雀之尾」用）。
///
/// <para>
/// 设计（design.md 卡池 165 行）：
/// <b>候选效果为燃烧、麻痹、电流外溢、寒冷、冻结、易伤、虚弱，优先给予敌人未拥有的负面效果。</b>
/// </para>
///
/// <para>
/// ⚠️ 燃烧 / 寒冷走 <see cref="SpellCmd"/> 的统一收口（本 mod 里所有"施加燃烧 / 施加寒冷"
/// 都从那里过，这样"每当施加燃烧/寒冷时"的效果才会响应）；
/// 其余（麻痹、电流外溢、冻结、易伤、虚弱）直接 <c>PowerCmd.Apply</c>。
/// </para>
/// </summary>
public enum RandomDebuffKind
{
    /// <summary>燃烧（本 mod，取最高层不叠加）。</summary>
    Burn,

    /// <summary>麻痹（本 mod）。</summary>
    Paralysis,

    /// <summary>电流外溢（本 mod）。</summary>
    CurrentOverflow,

    /// <summary>寒冷（本 mod）。</summary>
    Chill,

    /// <summary>冻结（本 mod）。</summary>
    Freeze,

    /// <summary>易伤（原版）。</summary>
    Vulnerable,

    /// <summary>虚弱（原版）。</summary>
    Weak,
}

/// <summary>「随机负面效果」的工具集：候选表、是否已拥有、随机挑选、施加。</summary>
public static class RandomDebuff
{
    /// <summary>全部候选（顺序即设计文档里的列举顺序）。</summary>
    public static readonly IReadOnlyList<RandomDebuffKind> All =
    [
        RandomDebuffKind.Burn,
        RandomDebuffKind.Paralysis,
        RandomDebuffKind.CurrentOverflow,
        RandomDebuffKind.Chill,
        RandomDebuffKind.Freeze,
        RandomDebuffKind.Vulnerable,
        RandomDebuffKind.Weak,
    ];

    /// <summary>该目标身上是否已经有这种负面效果。</summary>
    public static bool Has(Creature target, RandomDebuffKind kind) => kind switch
    {
        RandomDebuffKind.Burn => target.HasPower<BurnPower>(),
        RandomDebuffKind.Paralysis => target.HasPower<ParalysisPower>(),
        RandomDebuffKind.CurrentOverflow => target.HasPower<CurrentOverflowPower>(),
        RandomDebuffKind.Chill => target.HasPower<ChillPower>(),
        RandomDebuffKind.Freeze => target.HasPower<FreezePower>(),
        RandomDebuffKind.Vulnerable => target.HasPower<VulnerablePower>(),
        RandomDebuffKind.Weak => target.HasPower<WeakPower>(),
        _ => false,
    };

    /// <summary>
    /// 随机挑一种**目标还没有的**负面效果；如果七种全都有了，就退回"七种里随机"。
    /// </summary>
    public static RandomDebuffKind Pick(Creature target, Rng rng)
    {
        List<RandomDebuffKind> missing = All.Where(k => !Has(target, k)).ToList();
        IReadOnlyList<RandomDebuffKind> pool = missing.Count > 0 ? missing : All;
        return rng.NextItem(pool);
    }

    /// <summary>把 <paramref name="kind"/> 以 <paramref name="stacks"/> 层施加到 <paramref name="target"/> 身上。</summary>
    public static async Task Apply(
        PlayerChoiceContext choiceContext,
        Creature target,
        RandomDebuffKind kind,
        decimal stacks,
        Creature applier,
        CardModel? source)
    {
        switch (kind)
        {
            case RandomDebuffKind.Burn:
                // 走统一收口（"每当施加燃烧"的效果可响应）。
                await SpellCmd.ApplyBurn(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.Chill:
                await SpellCmd.ApplyChill(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.Paralysis:
                await PowerCmd.Apply<ParalysisPower>(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.CurrentOverflow:
                await PowerCmd.Apply<CurrentOverflowPower>(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.Freeze:
                await PowerCmd.Apply<FreezePower>(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.Vulnerable:
                await PowerCmd.Apply<VulnerablePower>(choiceContext, target, stacks, applier, source);
                break;

            case RandomDebuffKind.Weak:
                await PowerCmd.Apply<WeakPower>(choiceContext, target, stacks, applier, source);
                break;
        }
    }
}
