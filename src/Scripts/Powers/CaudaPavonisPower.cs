using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 孔雀之尾：**你每生成 1 张卡牌，就给予随机敌人 <c>Layers</c> 层随机负面效果 `Amount` 次**。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ① 生成检测：照抄本 mod「催化」<see cref="CatalyzationPower"/>
/// ══════════════════════════════════════════════════════════════════
/// 订阅 <c>AfterCardGeneratedForCombat(card, creator)</c>，且<b>只认自己生成的牌</b>
/// （<c>creator.Creature == Owner</c>）。走普通 <c>CardPileCmd.Add</c> 的加牌不算生成。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ② 叠层口径（用户 2026-11-09 明确）
/// ══════════════════════════════════════════════════════════════════
/// buff 描述是"<b>你每生成 1 张卡牌，就给予随机敌人 2 层随机负面效果 n 次</b>"，
/// <b>n = 层数（<c>Amount</c>）</b> —— 打两张就是 n=2，每次触发结算两次。
/// 所以 <see cref="StackType"/> 用 <c>Counter</c>（原版「噪声」<c>CacophonyPower</c> 也是 Counter）。
/// </para>
///
/// <para>
/// ⚠️ <b>"每次给几层"（2 / 升级后 3）不能放 <c>Amount</c></b>（那是 n），
/// 所以单独存一个 <see cref="LayersKey"/> 变量，由卡牌在 <c>PowerCmd.Apply</c> 之后
/// 调 <see cref="SetLayers"/> 写入（与「催化」<c>SetThreshold</c> 同一条"Apply 后再塞参数"模式）。
/// 混合叠加（普通 + 升级过的两张）时取<b>较大者</b>，保证升级永远不亏。
/// </para>
///
/// <para>
/// ③ 随机：敌人用 <c>Rng.CombatTargets</c>（原版随机打怪的标准流），
///    负面效果种类用 <c>Rng.Niche</c>；"优先给未拥有的"由
///    <see cref="RandomDebuff.Pick"/> 实现。
/// </para>
///
/// <para>
/// ④ 钩子没有 <c>PlayerChoiceContext</c>，按原版「炮火」<c>ArsenalPower</c> 的先例
///    用 <c>new ThrowingPlayerChoiceContext()</c>。
/// </para>
/// </summary>
public class CaudaPavonisPower : CustomPowerModel
{
    /// <summary>"每次给几层负面效果"的变量名（buff 文案写 <c>{Layers}</c>）。</summary>
    public const string LayersKey = "Layers";

    /// <summary>未升级时的默认层数（卡牌没来得及 SetLayers 时的兜底）。</summary>
    public const decimal DefaultLayers = 2m;

    public override PowerType Type => PowerType.Buff;

    /// <summary>层数 = n（每次触发结算几次）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/cauda_pavonis_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/cauda_pavonis_power_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(LayersKey, DefaultLayers),
    ];

    /// <summary>由卡牌在 Apply 之后写入"每次给几层"（升级过的牌给 3）。</summary>
    public void SetLayers(int layers)
    {
        decimal value = Math.Max(1, layers);
        DynamicVar layersVar = DynamicVars[LayersKey];

        // 取较大者：先打升级过的、再打普通的，不该把 3 降回 2。
        if (value > layersVar.BaseValue)
        {
            layersVar.BaseValue = value;
        }

        InvokeDisplayAmountChanged();
    }

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只认"自己生成的牌"（与「催化」同一条口径）。
        if (creator == null || creator.Creature != Owner)
        {
            return;
        }

        Player? player = Owner.Player;
        ICombatState? combatState = Owner.CombatState;
        if (player == null || combatState == null)
        {
            return;
        }

        int times = (int)Amount;
        if (times <= 0)
        {
            return;
        }

        decimal layers = DynamicVars[LayersKey].BaseValue;
        Flash();

        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();

        for (int i = 0; i < times; i++)
        {
            // 每次都重新取一遍存活敌人：中途可能被打死。
            List<Creature> enemies = combatState.HittableEnemies.Where(c => c.IsAlive).ToList();
            if (enemies.Count == 0)
            {
                return;
            }

            Creature? enemy = player.RunState.Rng.CombatTargets.NextItem(enemies);
            if (enemy == null)
            {
                return;
            }

            RandomDebuffKind kind = RandomDebuff.Pick(enemy, player.RunState.Rng.Niche);

            // source 传 null：这是能力触发，不属于任何一张牌的伤害/施加。
            await RandomDebuff.Apply(choiceContext, enemy, kind, layers, Owner, null);
        }
    }
}
