using System.Globalization;
using System.Net.Http.Json;
using Serilog.Core;
using Serilog.Events;

namespace Wayfarer.App;

/// <summary>A Serilog sink that posts events in batches to the URL in
/// <see cref="AppConfig.RemoteLogUrl"/>, when there is one. Off by default; meant for a tester on
/// the developer's own network who cannot send logs any other way. Events are dropped, never
/// retried, if the server is unreachable, so a dead server costs nothing but the lines.</summary>
internal sealed class RemoteLogSink : ILogEventSink, IAsyncDisposable
{
    /// <summary>The developer's receiver on the home network. Any copy of the plugin elsewhere
    /// asks this address to name itself, gets no answer or the wrong one, and sends nothing.</summary>
    public const string DefaultUrl = "http://192.168.178.25:7792/log";

    private const string PingPath = "/ping";
    private const string LogPath = "/log";
    private const string ReceiverName = "wayfarer-log-server";
    private const int MostQueued = 500;
    private const string TimeFormat = "HH:mm:ss.fff";
    private static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(2);

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Queue<Entry> queue = [];
    private readonly CancellationTokenSource stopping = new();
    private readonly string version = typeof(RemoteLogSink).Assembly.GetName().Version?.ToString(3) ?? "?";
    private Task? pump;
    private string? url;

    /// <summary>Starts sending to <paramref name="remoteUrl"/> once it has answered to its name, or
    /// does nothing when it is null, unreachable, or something other than a Wayfarer receiver.</summary>
    public void Start(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) || pump is not null)
        {
            return;
        }

        pump = Task.Run(() => PumpAsync(remoteUrl));
    }

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (url is null)
        {
            return;
        }

        var message = logEvent.Exception is null
            ? logEvent.RenderMessage(CultureInfo.InvariantCulture)
            : $"{logEvent.RenderMessage(CultureInfo.InvariantCulture)} | {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";

        lock (queue)
        {
            if (queue.Count < MostQueued)
            {
                queue.Enqueue(new Entry(logEvent.Timestamp.ToString(TimeFormat, CultureInfo.InvariantCulture), logEvent.Level.ToString(), message, version));
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

    private async Task PumpAsync(string remoteUrl)
    {
        if (!await IsWayfarerReceiverAsync(remoteUrl).ConfigureAwait(false))
        {
            return;
        }

        url = remoteUrl;
        while (!stopping.IsCancellationRequested)
        {
            await Task.Delay(FlushEvery, stopping.Token).ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> IsWayfarerReceiverAsync(string remoteUrl)
    {
        try
        {
            var ping = remoteUrl.EndsWith(LogPath, StringComparison.Ordinal) ? remoteUrl[..^LogPath.Length] + PingPath : remoteUrl + PingPath;
            var answer = await http.GetStringAsync(ping, stopping.Token).ConfigureAwait(false);
            return string.Equals(answer.Trim(), ReceiverName, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return false;
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
