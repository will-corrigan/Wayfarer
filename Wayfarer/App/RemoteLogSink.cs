using System.Globalization;
using System.Net.Http.Json;

namespace Wayfarer.App;

/// <summary>Posts log lines in batches to the URL in <see cref="AppConfig.RemoteLogUrl"/>, when
/// there is one. Off by default; meant for a tester on the developer's own network who cannot
/// send logs any other way. Lines are dropped, never retried, if the server is unreachable, so a
/// dead server costs nothing but the lines.</summary>
internal sealed class RemoteLogSink : IAsyncDisposable
{
    private const int MostQueued = 500;
    private const string TimeFormat = "HH:mm:ss.fff";
    private static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(2);

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Queue<Entry> queue = [];
    private readonly CancellationTokenSource stopping = new();
    private readonly string version;
    private Task? pump;
    private string? url;

    public RemoteLogSink()
    {
        version = typeof(RemoteLogSink).Assembly.GetName().Version?.ToString(3) ?? "?";
    }

    /// <summary>Starts sending to <paramref name="remoteUrl"/>, or does nothing when it is null.</summary>
    public void Start(string? remoteUrl)
    {
        url = string.IsNullOrWhiteSpace(remoteUrl) ? null : remoteUrl;
        if (url is not null && pump is null)
        {
            pump = Task.Run(PumpAsync);
        }
    }

    public void Offer(string level, string message)
    {
        if (url is null)
        {
            return;
        }

        lock (queue)
        {
            if (queue.Count < MostQueued)
            {
                queue.Enqueue(new Entry(DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture), level, message, version));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        if (pump is not null)
        {
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await FlushAsync().ConfigureAwait(false);
        http.Dispose();
        stopping.Dispose();
    }

    private async Task PumpAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            await Task.Delay(FlushEvery, stopping.Token).ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);
        }
    }

    private async Task FlushAsync()
    {
        List<Entry> batch;
        lock (queue)
        {
            if (queue.Count == 0 || url is null)
            {
                return;
            }

            batch = [.. queue];
            queue.Clear();
        }

        try
        {
            using var response = await http.PostAsJsonAsync(url, batch).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The server is away; the lines are gone and the game is not troubled.
        }
    }

    private sealed record Entry(string Time, string Level, string Message, string Version);
}
