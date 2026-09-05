using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using uniffi.octowatch_core;
using Windows.System;

namespace OctoWatch.Pages;

public sealed partial class HomePage : Page
{
    private bool _ready;

    // Stable list source: updated in place (diff), without re-rendering everything.
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

        // Keep the "x ago" labels fresh without touching the collection.
        _timeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timeTimer.Tick += (_, _) => RefreshTimes();
        _timeTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FeedMonitor.Instance.Updated -= OnMonitorUpdated;
        _timeTimer?.Stop();
        _timeTimer = null;
    }

    private DispatcherTimer? _timeTimer;

    private void RefreshTimes()
    {
        foreach (var item in _feed)
        {
            if (
                FeedList.ContainerFromItem(item) is ListViewItem container
                && container.ContentTemplateRoot is FrameworkElement root
                && root.FindName("AgoText") is TextBlock label
            )
            {
                label.Text = TimeText.Ago(item.UpdatedAt);
            }
        }
    }

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetMoreVisible(sender, true);

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e) =>
        SetMoreVisible(sender, false);

    private static void SetMoreVisible(object sender, bool visible)
    {
        if (sender is FrameworkElement card && card.FindName("MoreButton") is Button button)
            button.Opacity = visible ? 1 : 0;
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
            ShowMessage(InfoBarSeverity.Error, CoreError.Describe(ex));
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
        // Pull-request cards expand instead of opening; the detail has an open link.
        if (e.ClickedItem is FeedItem card && card.Kind != FeedMapper.KindPull)
            await SafeUrl.OpenAsync(card.Url);
    }

    // Reset any reused expander to collapsed so a recycled container never shows
    // another item's detail.
    private void OnContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Expander expander)
            expander.IsExpanded = false;
    }

    private async void OnPullExpanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is not FeedItem item || sender.Content is not FrameworkElement root)
            return;

        var spinner = (ProgressRing)root.FindName("DetailSpinner");
        var body = (StackPanel)root.FindName("DetailBody");
        var error = (TextBlock)root.FindName("DetailError");

        if (
            !FeedMapper.TryParseFullName(item.RepoFullName, out var owner, out var name)
            || item.PullNumber == 0
        )
        {
            error.Text = Loc.Get("Home_Error");
            error.Visibility = Visibility.Visible;
            return;
        }

        spinner.IsActive = true;
        body.Visibility = Visibility.Collapsed;
        error.Visibility = Visibility.Collapsed;
        try
        {
            var detail = await PullDetailStore.GetAsync(owner, name, item.PullNumber);

            ((TextBlock)root.FindName("DetailStats")).Text =
                $"+{detail.additions}  −{detail.deletions}  ·  "
                + string.Format(Loc.Get("Detail_Files"), detail.changedFiles);
            ((TextBlock)root.FindName("DetailMeta")).Text = BuildMeta(detail);
            MarkdownLite.Render(
                (RichTextBlock)root.FindName("DetailDescription"),
                string.IsNullOrWhiteSpace(detail.body) ? Loc.Get("Detail_NoDescription") : detail.body
            );
            var open = (HyperlinkButton)root.FindName("DetailOpen");
            open.Content = Loc.Get("Card_Open");
            open.Tag = detail.htmlUrl;

            body.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            error.Text = CoreError.Describe(ex);
            error.Visibility = Visibility.Visible;
        }
        finally
        {
            spinner.IsActive = false;
        }
    }

    private async void OnDetailOpen(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton button && button.Tag is string url)
            await SafeUrl.OpenAsync(url);
    }

    private static string BuildMeta(PullDetail d)
    {
        var parts = new List<string>();
        if (d.merged)
            parts.Add(Loc.Get("Detail_Merged"));
        else if (d.draft)
            parts.Add(Loc.Get("Detail_Draft"));
        else
            parts.Add(d.state);

        if (!d.merged && d.mergeable == true)
            parts.Add(Loc.Get("Detail_Mergeable"));
        else if (!d.merged && d.mergeable == false)
            parts.Add(Loc.Get("Detail_Conflicts"));

        if (d.requestedReviewers.Count > 0)
            parts.Add(string.Format(Loc.Get("Detail_Reviewers"), string.Join(", ", d.requestedReviewers)));
        if (d.labels.Count > 0)
            parts.Add(string.Join(", ", d.labels));

        return string.Join("  ·  ", parts);
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
            ShowMessage(InfoBarSeverity.Error, CoreError.Describe(ex));
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

    private static async Task OpenUrl(string url) => await SafeUrl.OpenAsync(url);

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
