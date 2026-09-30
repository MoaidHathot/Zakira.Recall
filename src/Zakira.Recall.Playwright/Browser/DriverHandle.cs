namespace Zakira.Recall.Playwright.Browser;

/// <summary>
/// Owns one lazily created, process-wide driver instance and replaces it when it dies.
/// Creation is serialized so concurrent first-time callers share a single instance instead of each spawning
/// their own, and a dead instance is disposed and re-created at most once per operation.
/// </summary>
internal sealed class DriverHandle<TDriver>(Func<CancellationToken, Task<TDriver>> create, Action<TDriver> dispose) : IAsyncDisposable
    where TDriver : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TDriver? _driver;
    private int _creationCount;

    /// <summary>Number of driver instances created so far (diagnostics).</summary>
    public int CreationCount => Volatile.Read(ref _creationCount);

    /// <summary>Raised after a dead driver has been dropped; everything it owned (browsers, sessions) is gone with it.</summary>
    public event Action? DriverLost;

    public async Task<TDriver> GetAsync(CancellationToken cancellationToken = default)
    {
        var current = Volatile.Read(ref _driver);
        if (current is not null)
        {
            return current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_driver is null)
            {
                _driver = await create(cancellationToken);
                Interlocked.Increment(ref _creationCount);
            }

            return _driver;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Drops <paramref name="dead"/> if it is still the current instance; a newer replacement is left alone.</summary>
    public async Task ResetAsync(TDriver dead, CancellationToken cancellationToken = default)
    {
        var dropped = false;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (ReferenceEquals(_driver, dead))
            {
                _driver = null;
                SafeDispose(dead);
                dropped = true;
            }
        }
        finally
        {
            _gate.Release();
        }

        if (dropped)
        {
            DriverLost?.Invoke();
        }
    }

    /// <summary>
    /// Runs <paramref name="operation"/> against the current driver. When it fails with an exception that
    /// <paramref name="isDriverDead"/> recognizes, the driver is replaced and the operation retried once.
    /// </summary>
    public async Task<TResult> RunAsync<TResult>(
        Func<TDriver, Task<TResult>> operation,
        Func<Exception, bool> isDriverDead,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var driver = await GetAsync(cancellationToken);
            try
            {
                return await operation(driver);
            }
            catch (Exception ex) when (attempt == 0 && isDriverDead(ex))
            {
                await ResetAsync(driver, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_driver is not null)
            {
                SafeDispose(_driver);
                _driver = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void SafeDispose(TDriver driver)
    {
        try
        {
            dispose(driver);
        }
        catch
        {
            // A dead driver's connection is already closed; disposal failures carry no information.
        }
    }
}
