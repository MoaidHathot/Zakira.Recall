using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.DependencyInjection;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.DependencyInjection;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>
/// Reproduces the production incident: a long-lived MCP host had fetched successfully, then another process ran a
/// "Stop-Process every node.exe whose command line contains playwright" sweep which killed the Playwright driver.
/// Every later fetch failed instantly with fetch_failed "Process exited" until the host was restarted.
/// Runs only where a real browser is available (Windows with Microsoft Edge).
/// </summary>
public sealed class DriverRecoveryTests
{
    private const string PageHtml = """
        <html><head><title>Local recovery page</title></head><body>
        <main><h1>Local recovery page</h1>
        <p>This page is served from the test process so the browser fetch does not depend on the network.
        It contains enough words for the fetcher to treat it as real content rather than a weak page.</p>
        </main></body></html>
        """;

    [Fact]
    public async Task Fetch_Recovers_After_The_Playwright_Driver_Process_Is_Killed()
    {
        if (!OperatingSystem.IsWindows() || !IsEdgeInstalled())
        {
            return;
        }

        using var server = LocalHtmlServer.Start(PageHtml);
        await using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory, NullLoggerFactory>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddRecallCore()
            .AddRecallPlaywright()
            .BuildServiceProvider();
        var factory = Assert.IsType<PlaywrightBrowserSessionFactory>(services.GetRequiredService<IBrowserSessionFactory>());
        var fetchService = services.GetRequiredService<IFetchService>();

        var first = await fetchService.FetchAsync(new FetchRequest { Url = server.Url, TimeoutSeconds = 30 });
        Assert.True(first.Success, first.Error?.Message);
        Assert.Contains("Local recovery page", first.Text);
        Assert.Equal(1, factory.DriverCreationCount);

        var killed = KillPlaywrightDriverChildren();
        Assert.True(killed > 0, "Expected to find and kill the Playwright driver (node.exe run-driver) spawned by this process.");
        await Task.Delay(500); // let the transport observe the exit

        var second = await fetchService.FetchAsync(new FetchRequest { Url = server.Url, TimeoutSeconds = 30 });
        Assert.True(second.Success, $"{second.Error?.Code}: {second.Error?.Message}");
        Assert.Contains("Local recovery page", second.Text);
        Assert.Equal(2, factory.DriverCreationCount);

        // The replacement driver is reused, not re-created per fetch.
        var third = await fetchService.FetchAsync(new FetchRequest { Url = server.Url, TimeoutSeconds = 30 });
        Assert.True(third.Success, third.Error?.Message);
        Assert.Equal(2, factory.DriverCreationCount);
    }

    private static bool IsEdgeInstalled()
        => new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            }
            .Where(root => !string.IsNullOrEmpty(root))
            .Any(root => File.Exists(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe")));

    /// <summary>Kills only node.exe children of this test process running the Playwright driver; nothing else on the machine.</summary>
    private static int KillPlaywrightDriverChildren()
    {
        var script =
            $"Get-CimInstance Win32_Process -Filter \"Name='node.exe'\" | " +
            $"Where-Object {{ $_.ParentProcessId -eq {Environment.ProcessId} -and $_.CommandLine -like '*run-driver*' }} | " +
            "ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Output $_.ProcessId }";
        using var process = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private sealed class LocalHtmlServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _stop = new();

        private LocalHtmlServer(HttpListener listener, string url, string html)
        {
            _listener = listener;
            Url = url;
            _ = ServeAsync(html);
        }

        public string Url { get; }

        public static LocalHtmlServer Start(string html)
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            return new LocalHtmlServer(listener, prefix + "page", html);
        }

        private async Task ServeAsync(string html)
        {
            var payload = Encoding.UTF8.GetBytes(html);
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload);
                context.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _listener.Close();
        }
    }
}
