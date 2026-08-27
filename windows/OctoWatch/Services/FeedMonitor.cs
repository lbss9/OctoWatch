using Microsoft.UI.Dispatching;

namespace OctoWatch;

internal sealed class FeedMonitor
{
    public static FeedMonitor Instance { get; } = new();

    public List<FeedItem> Items { get; private set; } = [];
    public FeedFilter Filter { get; } = new();
    public string? LastError { get; private set; }
    public string ManualOwner { get; set; } = "cli";
    public string ManualName { get; set; } = "cli";
    public event EventHandler? Updated;

    private readonly HashSet<string> _seen = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DispatcherQueue? _queue;
    private DispatcherQueueTimer? _timer;
    private bool _primed;
    private string _repoKey = "";

    public void Start(DispatcherQueue queue)
    {
        _queue = queue;
        _timer?.Stop();
        _timer = queue.CreateTimer();
        ApplyInterval();
        _timer.Tick += async (_, _) => await RefreshAsync(notify: true);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void ApplyInterval()
    {
        if (_timer is null)
            return;
        var seconds = Math.Max(30, SettingsStore.Load().PollingSeconds);
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    public void ReplaceItems(List<FeedItem> items)
    {
        Items = items;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAsync(bool notify)
    {
        var repos = FeedService.ResolveRepos(ManualOwner, ManualName);
        if (repos.Count == 0)
            return;

        await _gate.WaitAsync();
        try
        {
            var repoKey = string.Join(",", repos.Select(repo => $"{repo.owner}/{repo.name}"));
            var snapshot = await Task.Run(() => FeedService.FetchAll(repos));
            await RunOnUi(() => ApplyResult(snapshot, notify, repoKey, null));
        }
        catch (Exception ex)
        {
            await RunOnUi(() => ApplyResult(null, notify, _repoKey, ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ApplyResult(FeedSnapshot? snapshot, bool notify, string repoKey, string? failure)
    {
        if (repoKey != _repoKey)
        {
            _repoKey = repoKey;
            _primed = false;
            _seen.Clear();
        }

        if (snapshot is null)
        {
            LastError = failure;
            Items = [];
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        LastError = snapshot.Error;
        if (notify && _primed)
            UpdateToast.ShowNewItems(FeedMapper.NewlyArrived(snapshot.Items, _seen, Filter));
        Items = snapshot.Items;
        _seen.Clear();
        foreach (var item in snapshot.Items)
            _seen.Add(FeedMapper.Identity(item));
        _primed = true;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private Task RunOnUi(Action action)
    {
        if (_queue is null || _queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var finished = new TaskCompletionSource();
        if (
            !_queue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    finished.SetResult();
                }
                catch (Exception ex)
                {
                    finished.SetException(ex);
                }
            })
        )
        {
            action();
            return Task.CompletedTask;
        }

        return finished.Task;
    }
}
