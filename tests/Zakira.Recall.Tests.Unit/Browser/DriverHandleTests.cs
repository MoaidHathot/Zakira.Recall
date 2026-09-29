using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class DriverHandleTests
{
    private sealed class FakeDriver
    {
        public bool Disposed { get; set; }
    }

    private sealed class DeadDriverException : Exception;

    [Fact]
    public async Task Concurrent_First_Use_Creates_A_Single_Driver()
    {
        var started = new TaskCompletionSource();
        var created = 0;
        var handle = new DriverHandle<FakeDriver>(
            async _ =>
            {
                Interlocked.Increment(ref created);
                await started.Task;
                return new FakeDriver();
            },
            static driver => driver.Disposed = true);

        var callers = Enumerable.Range(0, 8).Select(_ => handle.GetAsync()).ToArray();
        started.SetResult();
        var drivers = await Task.WhenAll(callers);

        Assert.Equal(1, created);
        Assert.Equal(1, handle.CreationCount);
        Assert.All(drivers, driver => Assert.Same(drivers[0], driver));
    }

    [Fact]
    public async Task Replaces_A_Dead_Driver_And_Retries_The_Operation_Once()
    {
        var handle = new DriverHandle<FakeDriver>(static _ => Task.FromResult(new FakeDriver()), static driver => driver.Disposed = true);
        var first = await handle.GetAsync();
        var attempts = 0;

        var result = await handle.RunAsync(
            driver =>
            {
                attempts++;
                if (ReferenceEquals(driver, first))
                {
                    throw new DeadDriverException();
                }

                return Task.FromResult("ok");
            },
            static ex => ex is DeadDriverException);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
        Assert.Equal(2, handle.CreationCount);
        Assert.True(first.Disposed);
        Assert.NotSame(first, await handle.GetAsync());
    }

    [Fact]
    public async Task Gives_Up_After_The_Replacement_Also_Fails()
    {
        var handle = new DriverHandle<FakeDriver>(static _ => Task.FromResult(new FakeDriver()), static driver => driver.Disposed = true);

        await Assert.ThrowsAsync<DeadDriverException>(() => handle.RunAsync<string>(
            static _ => throw new DeadDriverException(),
            static ex => ex is DeadDriverException));

        Assert.Equal(2, handle.CreationCount);
    }

    [Fact]
    public async Task Does_Not_Replace_The_Driver_For_Unrelated_Failures()
    {
        var handle = new DriverHandle<FakeDriver>(static _ => Task.FromResult(new FakeDriver()), static driver => driver.Disposed = true);
        var driver = await handle.GetAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.RunAsync<string>(
            static _ => throw new InvalidOperationException("page closed"),
            static ex => ex is DeadDriverException));

        Assert.Equal(1, handle.CreationCount);
        Assert.False(driver.Disposed);
        Assert.Same(driver, await handle.GetAsync());
    }

    [Fact]
    public async Task Reset_With_A_Stale_Reference_Leaves_The_Current_Driver_Alone()
    {
        var handle = new DriverHandle<FakeDriver>(static _ => Task.FromResult(new FakeDriver()), static driver => driver.Disposed = true);
        var first = await handle.GetAsync();
        await handle.ResetAsync(first);
        var second = await handle.GetAsync();

        await handle.ResetAsync(first);

        Assert.Same(second, await handle.GetAsync());
        Assert.False(second.Disposed);
        Assert.Equal(2, handle.CreationCount);
    }

    [Fact]
    public async Task Dispose_Releases_The_Driver_And_Swallows_Disposal_Errors()
    {
        var handle = new DriverHandle<FakeDriver>(static _ => Task.FromResult(new FakeDriver()), static _ => throw new ObjectDisposedException("already closed"));
        await handle.GetAsync();

        await handle.DisposeAsync();

        Assert.Equal(1, handle.CreationCount);
    }
}
