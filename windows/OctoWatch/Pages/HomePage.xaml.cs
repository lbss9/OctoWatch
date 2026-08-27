using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using uniffi.octowatch_core;

namespace OctoWatch.Pages;

public sealed partial class HomePage : Page
{
    private bool _ready;

    // Stable list source: patched in-place so polling does not rebuild every card.
    private readonly ObservableCollection<FeedItem> _feed = [];

    public HomePage()
    {
        InitializeComponent();
        FeedList.ItemsSource = _feed;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private FeedFilter Filter => FeedMonitor.Instance.Filter;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OwnerBox.Text = FeedMonitor.Instance.ManualOwner;
        RepoBox.Text = FeedMonitor.Instance.ManualName;
        FilterActionsCheck.Content = Loc.Get("Home_FilterActions/Text");
        FilterPullsCheck.Content = Loc.Get("Home_FilterPrs/Text");
        FilterBranchesCheck.Content = Loc.Get("Home_FilterBranches/Text");
        ToolTipService.SetToolTip(FilterButton, Loc.Get("Home_Filter"));
        FilterActionsCheck.IsChecked = Filter.Actions;
        FilterPullsCheck.IsChecked = Filter.Pulls;
        FilterBranchesCheck.IsChecked = Filter.Branches;
        FeedMonitor.Instance.Updated -= OnMonitorUpdated;
        FeedMonitor.Instance.Updated += OnMonitorUpdated;
        ApplyRepoSource();
        _ready = true;
        OnRefresh(this, new RoutedEventArgs());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FeedMonitor.Instance.Updated -= OnMonitorUpdated;
    }

    private void OnMonitorUpdated(object? sender, EventArgs e) => ShowFiltered();

    private void OnFilterToggle(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        Filter.Actions = FilterActionsCheck.IsChecked == true;
        Filter.Pulls = FilterPullsCheck.IsChecked == true;
        Filter.Branches = FilterBranchesCheck.IsChecked == true;
        ShowFiltered();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        FeedMonitor.Instance.ManualOwner = OwnerBox.Text;
        FeedMonitor.Instance.ManualName = RepoBox.Text;
        var repos = FeedService.ResolveRepos(OwnerBox.Text, RepoBox.Text);
        if (repos.Count == 0)
        {
            ShowMessage(InfoBarSeverity.Warning, Loc.Get("Home_NeedOwnerRepo"));
            StatusText.Text = Loc.Get("Home_StatusHint");
            return;
        }

        SetBusy(true, string.Format(Loc.Get("Home_Loading"), repos.Count));
        try
        {
            await FeedMonitor.Instance.RefreshAsync(notify: false);
        }
        catch (Exception ex)
        {
            ShowMessage(InfoBarSeverity.Error, ex.Message);
            StatusText.Text = Loc.Get("Home_Error");
        }
        finally
        {
            SetBusy(false, null);
            ShowFiltered();
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        FeedMonitor.Instance.ReplaceItems(FeedMapper.Cleared(FeedMonitor.Instance.Items, Filter));
    }

    private void ShowFiltered()
    {
        var visible = FeedMapper.Visible(FeedMonitor.Instance.Items, Filter);
        FeedDiff.Apply(_feed, visible);
        StatusText.Text = string.Format(Loc.Get("Home_Count"), visible.Count);
        UpdateClearLabel();
        var error = FeedMonitor.Instance.LastError;
        if (error is not null)
            ShowMessage(InfoBarSeverity.Error, error);
        else if (visible.Count == 0)
            ShowMessage(InfoBarSeverity.Informational, Loc.Get("Home_Empty"));
        else
            MessageBar.IsOpen = false;
    }

    private void UpdateClearLabel()
    {
        ClearButton.Content = Loc.Get(Filter.ClearLabelKey());
        ClearButton.IsEnabled =
            !Spinner.IsActive && FeedMapper.Visible(FeedMonitor.Instance.Items, Filter).Count > 0;
    }

    private async void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FeedItem card)
            await SafeUrl.OpenAsync(card.Url);
    }

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetOverflowOpacity(sender, 1);

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e) =>
        SetOverflowOpacity(sender, 0);

    private static void SetOverflowOpacity(object sender, double opacity)
    {
        if (sender is FrameworkElement root && root.FindName("OverflowButton") is UIElement button)
            button.Opacity = opacity;
    }

    private void OnCardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not FeedItem item)
            return;
        var menu = new MenuFlyout();
        foreach (var action in CardActionCatalog.For(item.Kind, item.State))
        {
            var flyoutItem = new MenuFlyoutItem
            {
                Text = Loc.Get(action.LabelKey),
                Tag = new ActionTag(item, action.Id),
            };
            flyoutItem.Click += OnCardActionChosen;
            menu.Items.Add(flyoutItem);
        }
        menu.ShowAt(button);
    }

    private async void OnCardActionChosen(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem flyoutItem || flyoutItem.Tag is not ActionTag tagged)
            return;
        await RunCardAction(tagged.Item, tagged.ActionId);
    }

    private async Task RunCardAction(FeedItem item, string actionId)
    {
        try
        {
            if (actionId == CardActionCatalog.Open)
            {
                await OpenUrl(item.Url);
                return;
            }
            if (actionId == CardActionCatalog.OpenFiles)
            {
                await OpenUrl(item.Url.TrimEnd('/') + "/files");
                return;
            }
            if (actionId == CardActionCatalog.OpenChecks)
            {
                await OpenUrl(item.Url.TrimEnd('/') + "/checks");
                return;
            }
            if (actionId == CardActionCatalog.OpenCommits)
            {
                var commits = string.IsNullOrEmpty(item.BranchName)
                    ? item.Url
                    : $"https://github.com/{item.RepoFullName}/commits/{Uri.EscapeDataString(item.BranchName)}";
                await OpenUrl(commits);
                return;
            }
            await RunWorkflowAction(item, actionId);
        }
        catch (Exception ex)
        {
            ShowMessage(InfoBarSeverity.Error, ex.Message);
        }
    }

    private async Task RunWorkflowAction(FeedItem item, string actionId)
    {
        if (!GitHubSession.IsSignedIn)
        {
            ShowMessage(InfoBarSeverity.Warning, Loc.Get("Card_NeedLogin"));
            return;
        }
        if (
            !FeedMapper.TryParseFullName(item.RepoFullName, out var owner, out var name)
            || item.RunId == 0
        )
        {
            ShowMessage(InfoBarSeverity.Error, Loc.Get("Home_Error"));
            return;
        }

        var repo = new Repo(owner, name);
        await Task.Run(() =>
        {
            using var client = GitHubSession.CreateClient();
            if (actionId == CardActionCatalog.Rerun)
                client.RerunWorkflow(repo, item.RunId);
            else if (actionId == CardActionCatalog.RerunFailed)
                client.RerunFailedJobs(repo, item.RunId);
            else if (actionId == CardActionCatalog.Cancel)
                client.CancelWorkflow(repo, item.RunId);
        });
        OnRefresh(this, new RoutedEventArgs());
    }

    private static Task OpenUrl(string url) => SafeUrl.OpenAsync(url);

    private void ApplyRepoSource()
    {
        var selected = SettingsStore.Load().SelectedRepos;
        var configured = selected.Count > 0;
        ManualRepoBar.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
        if (configured)
            StatusText.Text = string.Format(Loc.Get("Home_UsingRepos"), selected.Count);
        else
            StatusText.Text = Loc.Get("Home_StatusHint");
    }

    private void SetBusy(bool busy, string? status)
    {
        Spinner.IsActive = busy;
        RefreshButton.IsEnabled = !busy;
        FilterButton.IsEnabled = !busy;
        ClearButton.IsEnabled =
            !busy && FeedMapper.Visible(FeedMonitor.Instance.Items, Filter).Count > 0;
        if (status is not null)
            StatusText.Text = status;
    }

    private void ShowMessage(InfoBarSeverity severity, string message)
    {
        MessageBar.Severity = severity;
        MessageBar.Message = message;
        MessageBar.IsOpen = true;
    }

    private sealed record ActionTag(FeedItem Item, string ActionId);
}
