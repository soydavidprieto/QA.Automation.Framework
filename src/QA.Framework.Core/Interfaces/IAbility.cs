namespace QA.Framework.Core.Interfaces;

/// <summary>
/// Marker interface representing a capability granted to an <see cref="IActor"/>.
/// Abilities encapsulate the underlying tool or resource needed to interact with
/// a system (e.g., a Playwright <c>IPage</c>, an <c>HttpClient</c>, a database
/// connection), keeping that tool out of Tasks and Actions so business workflows
/// remain tool-agnostic.
///
/// Concrete examples include:
/// <list type="bullet">
///   <item><c>BrowseTheWeb</c> — wraps a Playwright <c>IBrowserContext</c> and <c>IPage</c>.</item>
///   <item><c>CallAnApi</c> — wraps an <c>HttpClient</c> with base URL and auth headers.</item>
///   <item><c>QueryADatabase</c> — wraps a database connection or repository.</item>
///   <item><c>ManageTestData</c> — wraps a faker, generator, and cleanup registry.</item>
/// </list>
///
/// Abilities are owned by a single actor and disposed when the actor is disposed.
/// They typically hold expensive or stateful resources (browser contexts, sockets,
/// auth tokens), which is why they implement <see cref="IAsyncDisposable"/>.
/// </summary>
public interface IAbility : IAsyncDisposable
{
    /// <summary>
    /// Optional human-readable name used in diagnostics. Defaults to the type name.
    /// Useful when an actor has multiple instances of the same ability type
    /// (e.g., two API clients pointing at different services).
    /// </summary>
    string Name => GetType().Name;
}