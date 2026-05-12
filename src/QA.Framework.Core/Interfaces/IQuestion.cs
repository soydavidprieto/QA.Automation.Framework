namespace QA.Framework.Core.Interfaces;

/// <summary>
/// Represents a read-only inquiry an <see cref="IActor"/> can make about the state
/// of the system under test (e.g., the text of an element, the current URL,
/// the response body of an API call).
///
/// Questions are the Screenplay equivalent of "getters" — they observe state
/// without changing it. The returned value is then passed to assertion libraries
/// (FluentAssertions, Shouldly, xUnit's Assert) inside the test body, keeping
/// the assertion concern out of the framework itself.
/// </summary>
/// <typeparam name="TAnswer">
/// The type of value returned. Common types are <see cref="string"/> for text,
/// <see cref="bool"/> for visibility/presence, <see cref="int"/> for counts,
/// or domain models for structured API responses.
/// </typeparam>
public interface IQuestion<TAnswer>
{
    /// <summary>
    /// Asks the question on behalf of the given actor and returns the observed value.
    /// Implementations must not mutate system state — Questions are strictly read-only.
    /// </summary>
    /// <param name="actor">The actor asking this question.</param>
    /// <returns>The observed value from the system under test.</returns>
    /// <exception cref="MissingAbilityException">
    /// Thrown when the actor lacks an ability required to answer this question.
    /// </exception>
    Task<TAnswer> AnsweredBy(IActor actor);

    /// <summary>
    /// Human-readable description of what is being asked, used in logs and reports
    /// (e.g., "the text of the welcome banner", "the HTTP status of the orders endpoint").
    /// </summary>
    string ToString() => GetType().Name;
}