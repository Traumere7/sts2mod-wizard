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
}
