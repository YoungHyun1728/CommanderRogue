using UnityEngine;

public abstract class SkillEffectDefinition : ScriptableObject
{
    public abstract void Execute(SkillContext ctx);
}