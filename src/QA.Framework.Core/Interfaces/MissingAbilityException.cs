namespace QA.Framework.Core.Interfaces;

/// <summary>
/// Thrown when an <see cref="IActor"/> is asked to use an <see cref="IAbility"/>
/// it has not been granted. The exception message includes both the actor's name
/// and the missing ability type to make the failure obvious in test output.
/// </summary>
public sealed class MissingAbilityException : InvalidOperationException
{
    public MissingAbilityException(string actorName, Type abilityType)
        : base($"Actor '{actorName}' does not have the ability '{abilityType.Name}'. " +
               $"Grant it via actor.Can(...) before performing tasks that require it.")
    {
        ActorName = actorName;
        AbilityType = abilityType;
    }

    public string ActorName { get; }
    public Type AbilityType { get; }
}