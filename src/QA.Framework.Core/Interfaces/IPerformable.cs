namespace QA.Framework.Core.Interfaces;

/// <summary>
/// Represents anything an <see cref="IActor"/> can perform — either a low-level
/// Action (e.g., Click, Enter) or a high-level Task composed of other Performables
/// (e.g., Login, CompletePurchase).
///
/// This is the unifying contract that allows Tasks and Actions to be composed
/// interchangeably via <see cref="IActor.AttemptsTo"/>. Concrete implementations
/// must be stateless after construction so they can be safely reused across
/// parallel tests.
/// </summary>
public interface IPerformable
{
    /// <summary>
    /// Executes this Performable in the context of the given actor.
    /// Implementations should pull the abilities they need from the actor
    /// (e.g., <c>actor.AbilityTo&lt;BrowseTheWeb&gt;()</c>) and must not store
    /// actor state between invocations.
    /// </summary>
    /// <param name="actor">The actor performing this Action or Task.</param>
    /// <returns>A task that completes when the Performable has finished executing.</returns>
    /// <exception cref="MissingAbilityException">
    /// Thrown when the actor lacks an ability required to execute this Performable.
    /// </exception>
    Task PerformAs(IActor actor);

    /// <summary>
    /// Returns a human-readable description used in logs, Sentry breadcrumbs,
    /// and failure reports. Concrete classes should override <see cref="object.ToString"/>
    /// to provide a description in business language (e.g., "log in as alice@acme.com").
    /// </summary>
    string ToString() => GetType().Name;
}