using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 置换 - 1 费 - 技能 - 罕见
/// 移除你当前的所有[格挡]。每移除 4(3) 点格挡，获得 1 层[法术增益]。
///
/// <para>
/// <b>为什么"读完格挡再一次性 LoseBlock"</b>：
/// <c>CreatureCmd.LoseBlock</c> 之后 <c>self.Block</c> 就变成 0 了，
/// 所以必须先把数值读到局部变量里再扣（照抄「击穿」<see cref="Puncture"/> 的写法）。
/// </para>
///
/// <para>
/// <b>除不尽的部分直接舍去</b>：7 点格挡 / 每 4 点 ⇒ 1 层（不是 1.75 层）。
/// 用整数除法即可，与设计文档「每移除 4 点获得 1 层」的直读一致。
/// </para>
///
/// <para>
/// <b>升级</b>：每 4 点 → 每 3 点（<see cref="PerBlockKey"/> 数值 -1）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Displacement : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>"每多少点格挡换 1 层"的变量名（卡面文案写 <c>{PerBlock:diff()}</c>）。</summary>
    private const string PerBlockKey = "PerBlock";

    /// <summary>未升级：每 4 点格挡换 1 层。</summary>
    private const decimal PerBlock = 4m;

    /// <summary>升级后减少的量（4 → 3）。</summary>
    private const decimal PerBlockUpgrade = -1m;

    /// <summary>每满一份就给 1 层法术增益。</summary>
    private const decimal StacksPerThreshold = 1m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PerBlockKey, PerBlock),
        new PowerVar<SpellPowerPower>(StacksPerThreshold),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    public Displacement()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;

        // ① 先读格挡（LoseBlock 之后就读不到了）。
        int block = self.Block;   // Creature.Block 本身就是 int，不需要取整

        // ② 移除全部格挡。
        if (block > 0)
        {
            await CreatureCmd.LoseBlock(choiceContext, self, block, self);
        }

        // ③ 每满 per 点换 1 层法术增益（向下取整）。
        int per = (int)DynamicVars[PerBlockKey].BaseValue;
        if (per <= 0 || block <= 0)
        {
            return;
        }

        int stacks = block / per;
        if (stacks <= 0)
        {
            return;
        }

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            self,
            stacks,
            self,
            this);
    }

    protected override void OnUpgrade()
    {
        // 每 4 点 → 每 3 点
        DynamicVars[PerBlockKey].UpgradeValueBy(PerBlockUpgrade);
    }
}
