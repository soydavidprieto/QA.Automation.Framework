using Microsoft.Extensions.Logging;

namespace QA.Framework.Core.Interfaces;

/// <summary>
/// Represents a user (or system) participating in a test scenario. The Actor is
/// the central protagonist of the Screenplay pattern: it holds <see cref="IAbility"/>
/// instances, performs <see cref="IPerformable"/> Tasks and Actions, and answers
/// <see cref="IQuestion{TAnswer}"/> inquiries on behalf of the test.
///
/// Actors are typically created via a factory (e.g., <c>Cast.Actor("Alice")</c>),
/// granted abilities fluently via <see cref="Can{TAbility}"/>, and disposed at
/// the end of a test to release ability-owned resources (browser contexts,
/// connections, etc.).
///
/// Actors are NOT thread-safe. Each test should create its own actor (or actors).
/// Sharing an actor across parallel tests will cause undefined behavior because
/// abilities like <c>BrowseTheWeb</c> hold per-test state.
/// </summary>
public interface IActor : IAsyncDisposable
{
    /// <summary>
    /// The actor's display name, used in logs, Sentry tags, and failure reports.
    /// Conventionally a person's first name (e.g., "Alice", "Bob") to keep
    /// test narratives readable.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The logger scoped to this actor. Every Action, Task, and Question should
    /// log through this logger so that activity is correlated to the actor in
    /// structured log output.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Grants an ability to this actor and returns the actor for fluent chaining.
    /// If the actor already has an ability of the same type, it is replaced and
    /// the previous instance is disposed asynchronously in the background.
    /// </summary>
    /// <typeparam name="TAbility">The concrete ability type.</typeparam>
    /// <param name="ability">The ability instance to grant.</param>
    /// <returns>This actor, for fluent chaining.</returns>
    IActor Can<TAbility>(TAbility ability) where TAbility : IAbility;

    /// <summary>
    /// Returns true if this actor has been granted an ability of the given type.
    /// Useful for Performables that gracefully degrade when an optional ability
    /// (e.g., AI-assisted self-healing) is not present.
    /// </summary>
    bool HasAbilityTo<TAbility>() where TAbility : IAbility;

    /// <summary>
    /// Retrieves the ability of the given type, throwing a <see cref="MissingAbilityException"/>
    /// with a descriptive message if the actor does not have it. Tasks and Actions
    /// should call this without a null check — the explicit exception is more
    /// debuggable than a NullReferenceException three frames deep.
    /// </summary>
    /// <typeparam name="TAbility">The concrete ability type to retrieve.</typeparam>
    TAbility AbilityTo<TAbility>() where TAbility : IAbility;

    /// <summary>
    /// Sequentially executes the given Performables, awaiting each to completion
    /// before starting the next. Order is part of the contract — never run these
    /// in parallel, as Tasks frequently depend on side effects of earlier Actions.
    ///
    /// On failure, the exception is captured (with actor name, last performable
    /// description, and observability context) and rethrown so the test framework
    /// can fail the test in the usual way.
    /// </summary>
    /// <param name="performables">The Tasks and/or Actions to execute, in order.</param>
    Task AttemptsTo(params IPerformable[] performables);

    /// <summary>
    /// Asks a question and returns its answer. Equivalent in spirit to a getter,
    /// but routed through the actor so that observability and ability resolution
    /// stay consistent with how Tasks are executed.
    /// </summary>
    /// <typeparam name="TAnswer">The type of value the question returns.</typeparam>
    /// <param name="question">The question to ask.</param>
    Task<TAnswer> AsksFor<TAnswer>(IQuestion<TAnswer> question);
}