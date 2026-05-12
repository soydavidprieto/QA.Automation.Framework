using global::Microsoft.Playwright;
using QA.Framework.Core.Interfaces;

namespace QA.Framework.Core.Abilities;

/// <summary>
/// Grants an <see cref="IActor"/> the ability to interact with a web browser
/// via Playwright. Wraps a single <see cref="IBrowserContext"/> and a primary
/// <see cref="IPage"/> so that Actions (Click, Enter, Navigate) and Questions
/// (Text, Visibility, CurrentUrl) operate against a consistent session.
///
/// <para>
/// <b>Ownership boundary:</b> this ability does <i>not</i> own the
/// <see cref="IBrowser"/>. The browser is launched once per test fixture
/// and shared across many tests; each test creates its own
/// <see cref="IBrowserContext"/> via <see cref="WithAsync(IBrowser, BrowserNewContextOptions?, CancellationToken)"/>.
/// Disposing this ability closes the context (cookies, storage, in-flight
/// requests) but leaves the browser process running for the next test.
/// This separation is what makes parallel execution safe.
/// </para>
///
/// <para>
/// <b>Lifetime:</b> one <see cref="BrowseTheWeb"/> instance per test, granted
/// to a single actor, disposed when the actor is disposed. Sharing across
/// actors or tests will cause undefined behavior because the underlying
/// <see cref="IPage"/> is single-threaded.
/// </para>
/// </summary>
public sealed class BrowseTheWeb : IAbility
{
    private readonly bool _ownsContext;
    private bool _disposed;
    private bool _isTracing;

    /// <summary>
    /// The Playwright browser context owned (or borrowed) by this ability.
    /// Exposed for advanced scenarios — cookie inspection, multi-page flows,
    /// network interception. Most tests should not need to touch it directly.
    /// </summary>
    public IBrowserContext Context { get; }

    /// <summary>
    /// The primary page used by Actions and Questions. Setter is internal
    /// because popups and new-tab flows must go through
    /// <see cref="SwitchToPage(IPage)"/>, which performs validation.
    /// </summary>
    public IPage Page { get; private set; }

    /// <summary>
    /// Optional path to a Playwright <c>storageState.json</c> the context
    /// was hydrated from. Set when the ability was created via
    /// <see cref="WithStorageStateAsync"/>; <c>null</c> otherwise.
    /// </summary>
    public string? StorageStatePath { get; }

    /// <summary>
    /// Whether a Playwright trace is currently being recorded on the context.
    /// Used by the failure-capture pipeline to decide whether to flush a trace
    /// artifact on test teardown.
    /// </summary>
    public bool IsTracing => _isTracing;

    private BrowseTheWeb(
        IBrowserContext context,
        IPage page,
        bool ownsContext,
        string? storageStatePath = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Page = page ?? throw new ArgumentNullException(nameof(page));
        _ownsContext = ownsContext;
        StorageStatePath = storageStatePath;
    }

    // ---------------------------------------------------------------------
    // Factories
    // ---------------------------------------------------------------------

    /// <summary>
    /// Creates a fresh <see cref="BrowseTheWeb"/> ability with a brand-new
    /// browser context and page. This is the most common entry point —
    /// every test that needs an isolated session calls this in setup.
    /// </summary>
    /// <param name="browser">The shared browser launched by the test fixture.</param>
    /// <param name="options">
    /// Optional context options (viewport, locale, timezone, user agent,
    /// HTTPS-error policy, etc.). When <c>null</c>, sensible defaults are used.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public static async Task<BrowseTheWeb> WithAsync(
        IBrowser browser,
        BrowserNewContextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(browser);
        cancellationToken.ThrowIfCancellationRequested();

        var context = await browser.NewContextAsync(options ?? DefaultContextOptions())
            .ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        return new BrowseTheWeb(context, page, ownsContext: true);
    }

    /// <summary>
    /// Creates a <see cref="BrowseTheWeb"/> ability hydrated from a previously
    /// saved Playwright storage state file (cookies + localStorage). Use this
    /// to skip UI login in tests by loading an authenticated session captured
    /// once in a setup hook.
    /// </summary>
    /// <param name="browser">The shared browser launched by the test fixture.</param>
    /// <param name="storageStatePath">Absolute path to a <c>storageState.json</c> file.</param>
    /// <param name="options">
    /// Optional context options merged with the storage state. The
    /// <see cref="BrowserNewContextOptions.StorageStatePath"/> field is set
    /// internally and overrides any value in <paramref name="options"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public static async Task<BrowseTheWeb> WithStorageStateAsync(
        IBrowser browser,
        string storageStatePath,
        BrowserNewContextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageStatePath);
        if (!File.Exists(storageStatePath))
        {
            throw new FileNotFoundException(
                $"Storage state file was not found: {storageStatePath}",
                storageStatePath);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var contextOptions = options ?? DefaultContextOptions();
        contextOptions.StorageStatePath = storageStatePath;

        var context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        return new BrowseTheWeb(context, page, ownsContext: true, storageStatePath);
    }

    /// <summary>
    /// Wraps an already-created <see cref="IBrowserContext"/>. Use this when
    /// the context is owned by something else (e.g., a higher-level fixture
    /// that pre-warms a context with a shared session). The ability will
    /// <i>not</i> dispose the context — that is the caller's responsibility.
    /// </summary>
    /// <param name="context">An existing browser context owned by the caller.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    public static async Task<BrowseTheWeb> WithExistingContextAsync(
        IBrowserContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // Reuse the first existing page if any; otherwise create one.
        var page = context.Pages.Count > 0
            ? context.Pages[0]
            : await context.NewPageAsync().ConfigureAwait(false);

        return new BrowseTheWeb(context, page, ownsContext: false);
    }

    // ---------------------------------------------------------------------
    // Convenience accessors
    // ---------------------------------------------------------------------

    /// <summary>
    /// Convenience helper used by Actions and Questions to retrieve this
    /// ability from an actor in one line, e.g.
    /// <c>var browser = BrowseTheWeb.As(actor);</c>.
    /// </summary>
    /// <param name="actor">The actor performing the Action or Question.</param>
    /// <exception cref="MissingAbilityException">
    /// Thrown via the actor when <c>BrowseTheWeb</c> has not been granted.
    /// </exception>
    public static BrowseTheWeb As(IActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.AbilityTo<BrowseTheWeb>();
    }

    /// <summary>
    /// Switches the primary <see cref="Page"/> to a different one within the
    /// same context — typically after a popup, a window-open, or an OAuth
    /// redirect spawns a new tab.
    /// </summary>
    /// <param name="page">The new page to make primary. Must belong to <see cref="Context"/>.</param>
    public void SwitchToPage(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Context != Context)
        {
            throw new InvalidOperationException(
                "Cannot switch to a page that belongs to a different browser context.");
        }
        Page = page;
    }

    // ---------------------------------------------------------------------
    // Tracing & failure capture
    // ---------------------------------------------------------------------

    /// <summary>
    /// Starts Playwright tracing on the context. Traces include screenshots,
    /// DOM snapshots, network activity, and source frames — invaluable for
    /// post-mortem analysis when a test fails. Pair with
    /// <see cref="StopTracingAsync(string)"/> in a teardown hook.
    /// </summary>
    /// <param name="title">Human-readable title shown in Playwright Trace Viewer.</param>
    public async Task StartTracingAsync(string? title = null)
    {
        if (_isTracing) return;
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
            Title = title
        }).ConfigureAwait(false);
        _isTracing = true;
    }

    /// <summary>
    /// Stops tracing and writes the resulting trace to the given path.
    /// Returns the path on success, or <c>null</c> if tracing was not active.
    /// </summary>
    /// <param name="outputPath">Where to write the <c>.zip</c> trace artifact.</param>
    public async Task<string?> StopTracingAsync(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!_isTracing) return null;

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await Context.Tracing.StopAsync(new TracingStopOptions { Path = outputPath })
            .ConfigureAwait(false);
        _isTracing = false;
        return outputPath;
    }

    /// <summary>
    /// Captures a screenshot of the current page as a byte array. Used by
    /// the failure-capture pipeline to attach a screenshot to Sentry events
    /// and xUnit TRX results.
    /// </summary>
    /// <param name="fullPage">Whether to capture beyond the viewport.</param>
    public Task<byte[]> CaptureScreenshotAsync(bool fullPage = true) =>
        Page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = fullPage,
            Type = ScreenshotType.Png
        });

    /// <summary>
    /// Persists the current authenticated session (cookies + localStorage)
    /// to disk so a later test can boot via <see cref="WithStorageStateAsync"/>.
    /// </summary>
    /// <param name="path">Destination path for the <c>storageState.json</c> file.</param>
    public async Task<string> SaveStorageStateAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = path })
            .ConfigureAwait(false);
        return path;
    }

    // ---------------------------------------------------------------------
    // Disposal
    // ---------------------------------------------------------------------

    /// <summary>
    /// Closes the browser context (if owned) and releases any in-flight
    /// tracing. The browser itself is never closed here — its lifetime is
    /// owned by the test fixture.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Best-effort tracing flush so we don't lose the trace on a crash.
        if (_isTracing)
        {
            try
            {
                await Context.Tracing.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Swallow — disposal must not throw, and a failed trace flush
                // is strictly diagnostic. The Sentry pipeline records a warning.
            }
            _isTracing = false;
        }

        if (_ownsContext)
        {
            try
            {
                await Context.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Same rationale: dispose paths must be exception-safe.
            }
        }
    }

    // ---------------------------------------------------------------------
    // Defaults
    // ---------------------------------------------------------------------

    private static BrowserNewContextOptions DefaultContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
        IgnoreHTTPSErrors = false,
        AcceptDownloads = true,
        Locale = "en-US",
        TimezoneId = "UTC"
    };
}