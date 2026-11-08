namespace Wizard.Scripts.Spells;

/// <summary>
/// 法术流派。决定伤害的视觉表现，以及受哪些增益/减益影响。
/// </summary>
public enum SpellSchool
{
    /// <summary>非法术伤害。</summary>
    None = 0,

    /// <summary>火焰：受寒冷加成。</summary>
    Fire = 1,

    /// <summary>雷电：受寒冷与超导加成，触发电流外溢。</summary>
    Lightning = 2,

    /// <summary>寒冰：不受寒冷加成（寒冷只提升火焰与雷电）。</summary>
    Ice = 3,

    /// <summary>
    /// 以太：**同时**被视作火焰 / 雷电 / 寒冰伤害（见 design.md「特殊伤害类型」）。
    ///
    /// <para>
    /// ⚠️ 它只是"卡面/接口层面的流派标签"。真正的伤害判定不靠这个枚举，
    /// 而是靠 <c>SpellProps.Aether</c>（= Fire | Lightning | Ice 三位的并集）——
    /// 所以任何按"单系标记位"判断的逻辑都会自动把以太算作三系。
    /// </para>
    /// </summary>
    Aether = 4,
}
