using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>Minimal loopback HTTP server so browser-based tests do not depend on external sites.</summary>
internal sealed class LocalHtmlServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<string, (int Status, string Html)> _handler;
    private readonly CancellationTokenSource _stop = new();

    private LocalHtmlServer(HttpListener listener, string baseUrl, Func<string, (int Status, string Html)> handler)
    {
        _listener = listener;
        _handler = handler;
        BaseUrl = baseUrl;
        _ = ServeAsync();
    }

    /// <summary>Base URL with a trailing slash, e.g. http://127.0.0.1:54321/.</summary>
    public string BaseUrl { get; }

    public string UrlFor(string path) => BaseUrl + path.TrimStart('/');

    /// <summary>Serves the same page with HTTP 200 for every path.</summary>
    public static LocalHtmlServer Start(string html)
        => Start(_ => (200, html));

    /// <summary>Serves whatever the handler returns for the request path (without leading slash).</summary>
    public static LocalHtmlServer Start(Func<string, (int Status, string Html)> handler)
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        return new LocalHtmlServer(listener, prefix, handler);
    }

    private async Task ServeAsync()
    {
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

            var (status, html) = _handler(context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty);
            var payload = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = status;
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
