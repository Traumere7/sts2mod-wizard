using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;
using MegaCrit.Sts2.Core.HoverTips;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 降雷 - 1 费 - 攻击 - 稀有
/// 对全体敌人造成 4(7) 点雷电伤害。
/// 所有敌人具有的负面效果每有 1 种类，就再结算 1 次。
///
/// <para>
/// <b>段数口径是"全体并集"，对每个敌人都一样</b>：
/// 取<b>所有敌人</b>负面效果种类的并集数量 N，则每个敌人都承受
/// <c>1 + N</c> 段，每段都是完整的 6 点。
/// 完整的取值过程见 <see cref="CountDebuffKindsAcross"/>。
/// </para>
///
/// <para>
/// <b>⚠️ 旧口径已废弃</b>：最初实现是"各敌人分别数自己的种类数"，
/// 那在两个敌人负面数量不同时会让人不知道该按谁算。设计文档已明确改为并集。
/// </para>
///
/// <para>
/// <b>"1 种"的口径与「魔力汲取」一致</b>：过滤 <c>PowerType.Debuff</c>
/// 后按 <c>PowerModel.Id</c> 去重 —— 同一个 power 叠多少层、
/// 出现在几个敌人身上，都只算 1 种。
/// （原版「撕裂」<c>Rend</c> 是直接数数量、不去重，这里沿用本 mod 已定的口径。）
/// </para>
///
/// <para>
/// <b>卡面实时显示额外次数</b>：<c>CalculatedHits</c> + 文案里的
/// <c>{InCombat:\n（额外结算{CalculatedHits:diff()}次）|}</c>，
/// 照抄原版「终结技」<c>Finisher</c>。详见 <c>CONTEXT.md</c> §6.20。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class LightningFall : WizardSpellCard
{
    private const int Cost = 1;

    /// <summary>卡面动态次数的变量名（照抄原版「终结技」<c>Finisher</c>）。</summary>
    private const string CalculatedHitsKey = "CalculatedHits";

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move | SpellProps.For(School)),

        // ⚠️ 这两个是 CalculatedVar 的**硬前置**，缺了会在 ToMutable() 时抛
        //    KeyNotFoundException: 'CalculationBase' was not present in the dictionary
        //    （堆栈：CalculatedVar.SetOwner → UpdateValues → GetBaseVar → DynamicVarSet.get_CalculationBase）
        //    原版「终结技」Finisher 同样声明了这两个，即使它并不显示它们。
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),

        // 卡面实时显示"额外结算 N 次"，照抄原版「终结技」Finisher：
        //   new CalculatedVar("CalculatedHits").WithMultiplier(...)
        // 配上文案里的 {InCombat:\n（额外结算{CalculatedHits:diff()}次）|}
        new CalculatedVar(CalculatedHitsKey)
            .WithMultiplier((CardModel card, Creature? _) => CountDebuffKindsAcross(card)),
    ];

    // 本牌不提供额外悬停提示。
    //
    // 曾经这里挂了 HoverTipFactory.FromPower<CurrentOverflowPower>()，
    // 但降雷**完全不涉及电流外溢** —— 那是「雷光」「交流电」的机制。
    // 悬停时弹出无关词条只会让人以为这张牌会给电流外溢。
    //
    // 2026-10-17：曾短暂加过「虚无」词条（CanonicalKeywords + 这里的 FromKeyword），
    // 后经考虑**回调移除**。数值维持 4(7) 不变。

    public LightningFall()
        : base(Cost, CardType.Attack, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        bool hasEnemy = combatState
            .GetOpponentsOf(Owner.Creature)
            .Any(c => c.IsAlive);

        if (!hasEnemy)
        {
            return;
        }

        // 口径：**所有敌人**具有的负面效果每有 1 种类，就再结算 1 次。
        // 即"全体负面效果种类的并集"，对每个敌人都用**同一个**段数 ——
        // 不再是"各敌人分别算自己的种类数"（旧口径，已按设计文档修改）。
        int extraHits = CountDebuffKindsAcross(this);
        int hitCount = 1 + extraHits;

        // **每段都是对所有怪物结算**（不是逐个怪物各打一次）：
        //   TargetingAllOpponents 让每一段同时命中全体敌人，
        //   所以 n 段 = 每名敌人都挨 n 下，段与段之间交错进行。
        //
        // ⚠️ 这里能安全用 TargetingAllOpponents，正是因为段数是"全体并集"、
        //    对每个敌人都相同 —— 该 API 只接受**一个共享的** HitCount。
        //    （旧口径按各敌人自己的种类数算，段数不同，那时只能用循环，
        //     见 CONTEXT.md §6.7 关于 TargetingAllOpponents 的限制说明。）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitCount(hitCount)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 全体敌人身上不同种类负面效果的**并集**数量（按 <c>PowerModel.Id</c> 去重）。
    ///
    /// <para>
    /// 同一个 id 无论出现在几个敌人身上、叠多少层，都只算 1 种 ——
    /// 与「魔力汲取」「远古残章」的口径一致。
    /// </para>
    ///
    /// <para>
    /// 非战斗（卡牌库预览）返回 0，卡面就显示基础段数。
    /// </para>
    /// </summary>
    private static int CountDebuffKindsAcross(CardModel card)
    {
        // 先取 creature 再判空：card.Owner 是可空的，直接链式访问会 CS8602。
        if (card.Owner?.Creature is not { } self)
        {
            return 0;
        }

        ICombatState? combatState = card.CombatState ?? self.CombatState;
        if (combatState == null)
        {
            return 0;
        }

        return combatState
            .GetOpponentsOf(self)
            .Where(c => c.IsAlive)
            .SelectMany(c => c.Powers)
            .Where(p => p.Type == PowerType.Debuff)
            .Select(p => p.Id)
            .Distinct()
            .Count();
    }

    protected override void OnUpgrade()
    {
        // 5 → 8
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
