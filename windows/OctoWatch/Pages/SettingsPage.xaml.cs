using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using uniffi.octowatch_core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace OctoWatch.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;
    private readonly List<RepoChoice> _allRepos = [];
    private DispatcherTimer? _pollTimer;
    private string? _deviceCode;
    private string? _verificationUri;
    private int _intervalSeconds = 5;
    private DateTimeOffset _expiresAt;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        StopPolling();
        PersistRepoSelection();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStore.Load();
        FillComboBoxes(settings);
        BuildEventChecks(settings, null);
        StartupSwitch.IsOn = settings.StartWithWindows;
        AcrylicSwitch.IsOn = settings.AcrylicEnabled;
        OpacitySlider.Value = settings.BackgroundOpacity;
        SetOpacityPanelEnabled(settings.AcrylicEnabled);
        OpacityValue.Text = $"{settings.BackgroundOpacity}%";
        _loading = false;
        await RefreshAccountUi();
        if (GitHubSession.IsSignedIn)
            await LoadRepositories();
    }

    private void FillComboBoxes(AppSettings settings)
    {
        FillPolling(settings.PollingSeconds);
        FillLanguage(settings.Language);
        FillTheme(settings.Theme);
        FillEventScope(settings);
    }

    private static void AddItem(ComboBox box, string label, object tag)
    {
        box.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
    }

    private static void SelectTag(ComboBox box, object tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag))
            {
                box.SelectedItem = item;
                return;
            }
        }
        if (box.Items.Count > 0)
            box.SelectedIndex = 0;
    }

    private void FillPolling(int seconds)
    {
        PollingBox.Items.Clear();
        AddItem(PollingBox, Loc.Get("Polling_30s"), 30);
        AddItem(PollingBox, Loc.Get("Polling_1m"), 60);
        AddItem(PollingBox, Loc.Get("Polling_5m"), 300);
        AddItem(PollingBox, Loc.Get("Polling_15m"), 900);
        SelectTag(PollingBox, seconds);
    }

    private void FillLanguage(string language)
    {
        LanguageBox.Items.Clear();
        AddItem(LanguageBox, Loc.Get("Lang_PtBr"), "pt-BR");
        AddItem(LanguageBox, Loc.Get("Lang_En"), "en");
        SelectTag(LanguageBox, language);
    }

    private void FillTheme(string theme)
    {
        ThemeBox.Items.Clear();
        AddItem(ThemeBox, Loc.Get("Theme_System"), "System");
        AddItem(ThemeBox, Loc.Get("Theme_Light"), "Light");
        AddItem(ThemeBox, Loc.Get("Theme_Dark"), "Dark");
        SelectTag(ThemeBox, theme);
    }

    private void FillEventScope(AppSettings settings)
    {
        var previous = CurrentEventScope() ?? "";
        EventScopeBox.Items.Clear();
        AddItem(EventScopeBox, Loc.Get("Settings_EventsGlobal"), "");
        foreach (var repo in settings.SelectedRepos)
            AddItem(EventScopeBox, repo, repo);
        SelectTag(EventScopeBox, previous);
    }

    private string? CurrentEventScope()
    {
        return EventScopeBox.SelectedItem is ComboBoxItem item ? item.Tag as string : null;
    }

    private void BuildEventChecks(AppSettings settings, string? scope)
    {
        EventsPanel.Children.Clear();
        var selected = EventsFor(settings, scope);
        foreach (var (id, key) in MonitorEvents.Catalog)
        {
            var box = new CheckBox
            {
                Content = Loc.Get(key),
                Tag = id,
                IsChecked = selected.Contains(id),
            };
            box.Checked += OnEventToggled;
            box.Unchecked += OnEventToggled;
            EventsPanel.Children.Add(box);
        }
    }

    private static List<string> EventsFor(AppSettings settings, string? scope)
    {
        if (
            !string.IsNullOrEmpty(scope)
            && settings.EventsByRepo.TryGetValue(scope, out var list)
        )
            return list;
        return settings.GlobalEvents;
    }

    private void OnEventScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        BuildEventChecks(SettingsStore.Load(), CurrentEventScope());
    }

    private void OnEventToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var settings = SettingsStore.Load();
        var selected = EventsPanel
            .Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag!)
            .ToList();
        var scope = CurrentEventScope();
        if (string.IsNullOrEmpty(scope))
            settings.GlobalEvents = selected;
        else
            settings.EventsByRepo[scope] = selected;
        SettingsStore.Save(settings);
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        SignInButton.IsEnabled = false;
        try
        {
            var code = await Task.Run(
                () => OctowatchCoreMethods.StartDeviceLogin(GitHubSession.DefaultScopes)
            );
            ShowDeviceCode(code);
            await Launcher.LaunchUriAsync(new Uri(code.verificationUri));
            StartPollTimer();
        }
        catch (Exception ex)
        {
            ShowAuth(InfoBarSeverity.Error, ex.Message);
            SignInButton.IsEnabled = true;
        }
    }

    private void ShowDeviceCode(DeviceCode code)
    {
        _deviceCode = code.deviceCode;
        _verificationUri = code.verificationUri;
        _intervalSeconds = (int)Math.Max(code.interval, 1u);
        _expiresAt = DateTimeOffset.Now.AddSeconds(code.expiresIn);
        UserCodeText.Text = code.userCode;
        DevicePanel.Visibility = Visibility.Visible;
        AuthSpinner.IsActive = true;
        AuthBar.IsOpen = false;
    }

    private void StartPollTimer()
    {
        _pollTimer?.Stop();
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_intervalSeconds) };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private async void OnPollTick(object? sender, object e)
    {
        if (_deviceCode is null)
            return;
        if (DateTimeOffset.Now >= _expiresAt)
        {
            FinishAuthFailure(Loc.Get("Settings_AuthExpired"));
            return;
        }
        try
        {
            var status = await Task.Run(() => OctowatchCoreMethods.PollDeviceLogin(_deviceCode));
            await ApplyLoginStatus(status);
        }
        catch (Exception ex)
        {
            ShowAuth(InfoBarSeverity.Error, ex.Message);
        }
    }

    private async Task ApplyLoginStatus(DeviceLoginStatus status)
    {
        switch (status)
        {
            case DeviceLoginStatus.Pending:
                break;
            case DeviceLoginStatus.SlowDown:
                _intervalSeconds += 5;
                StartPollTimer();
                break;
            case DeviceLoginStatus.Expired:
                FinishAuthFailure(Loc.Get("Settings_AuthExpired"));
                break;
            case DeviceLoginStatus.Denied:
                FinishAuthFailure(Loc.Get("Settings_AuthDenied"));
                break;
            case DeviceLoginStatus.Authorized authorized:
                StopPolling();
                CredentialStore.SaveToken(authorized.token);
                await RefreshAccountUi();
                await LoadRepositories();
                break;
        }
    }

    private void FinishAuthFailure(string message)
    {
        StopPolling();
        ShowAuth(InfoBarSeverity.Error, message);
        SignInButton.IsEnabled = true;
    }

    private void StopPolling()
    {
        if (_pollTimer is not null)
        {
            _pollTimer.Tick -= OnPollTick;
            _pollTimer.Stop();
            _pollTimer = null;
        }
        AuthSpinner.IsActive = false;
        _deviceCode = null;
    }

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(UserCodeText.Text ?? "");
        Clipboard.SetContent(data);
    }

    private async void OnOpenGithub(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_verificationUri))
            await Launcher.LaunchUriAsync(new Uri(_verificationUri));
    }

    private async void OnSignOut(object sender, RoutedEventArgs e)
    {
        StopPolling();
        CredentialStore.Clear();
        await RefreshAccountUi();
    }

    private async Task RefreshAccountUi()
    {
        var signedIn = GitHubSession.IsSignedIn;
        SignOutButton.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        SignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        DevicePanel.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = true;
        if (!signedIn)
        {
            AccountStatusText.Text = Loc.Get("Settings_NotSignedIn/Text");
            return;
        }
        try
        {
            var login = await Task.Run(() =>
            {
                using var client = GitHubSession.CreateClient();
                return client.Whoami();
            });
            AccountStatusText.Text = string.Format(Loc.Get("Settings_SignedInAs"), login);
        }
        catch (Exception ex)
        {
            ShowAuth(InfoBarSeverity.Error, ex.Message);
        }
    }

    private async void OnLoadRepos(object sender, RoutedEventArgs e) => await LoadRepositories();

    private async Task LoadRepositories()
    {
        if (!GitHubSession.IsSignedIn)
        {
            ShowAuth(InfoBarSeverity.Warning, Loc.Get("Settings_NeedLoginForRepos"));
            return;
        }
        LoadReposButton.IsEnabled = false;
        try
        {
            var repos = await Task.Run(() =>
            {
                using var client = GitHubSession.CreateClient();
                return client.ListRepositories();
            });
            BindRepos(repos);
        }
        catch (Exception ex)
        {
            ShowAuth(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            LoadReposButton.IsEnabled = true;
        }
    }

    private void BindRepos(List<Repo> repos)
    {
        var selected = new HashSet<string>(
            SettingsStore.Load().SelectedRepos,
            StringComparer.OrdinalIgnoreCase
        );
        _allRepos.Clear();
        foreach (var repo in repos)
        {
            var choice = new RepoChoice
            {
                Owner = repo.owner,
                Name = repo.name,
                IsSelected = selected.Contains(FeedMapper.FullName(repo.owner, repo.name)),
            };
            choice.PropertyChanged += (_, _) => PersistRepoSelection();
            _allRepos.Add(choice);
        }
        ApplyRepoFilter();
        FillEventScope(SettingsStore.Load());
    }

    private void OnRepoSearch(object sender, TextChangedEventArgs e) => ApplyRepoFilter();

    private void ApplyRepoFilter()
    {
        var query = RepoSearchBox.Text.Trim();
        IEnumerable<RepoChoice> view = _allRepos;
        if (query.Length > 0)
            view = _allRepos.Where(r =>
                r.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
            );
        RepoList.ItemsSource = view.ToList();
    }

    private void PersistRepoSelection()
    {
        if (_loading || _allRepos.Count == 0)
            return;
        var settings = SettingsStore.Load();
        settings.SelectedRepos = _allRepos.Where(r => r.IsSelected).Select(r => r.FullName).ToList();
        SettingsStore.Save(settings);
        var keep = CurrentEventScope();
        _loading = true;
        FillEventScope(settings);
        if (!string.IsNullOrEmpty(keep))
            SelectTag(EventScopeBox, keep);
        _loading = false;
    }

    private void OnPollingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PollingBox.SelectedItem is not ComboBoxItem item || item.Tag is not int seconds)
            return;
        var settings = SettingsStore.Load();
        settings.PollingSeconds = seconds;
        SettingsStore.Save(settings);
        FeedMonitor.Instance.ApplyInterval();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem item || item.Tag is not string lang)
            return;
        var settings = SettingsStore.Load();
        settings.Language = lang;
        SettingsStore.Save(settings);
        App.Main?.ReloadForLanguage(lang);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ComboBoxItem item || item.Tag is not string theme)
            return;
        var settings = SettingsStore.Load();
        settings.Theme = theme;
        SettingsStore.Save(settings);
        App.Main?.ApplyTheme(theme);
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var settings = SettingsStore.Load();
        settings.StartWithWindows = StartupSwitch.IsOn;
        SettingsStore.Save(settings);
        StartupRegistry.SetEnabled(StartupSwitch.IsOn);
    }

    // Toggle/slider only update the form; the backdrop changes when Apply is clicked.
    private void OnAcrylicToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        SetOpacityPanelEnabled(AcrylicSwitch.IsOn);
    }

    private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
            return;
        OpacityValue.Text = $"{(int)e.NewValue}%";
    }

    private void SetOpacityPanelEnabled(bool enabled)
    {
        OpacitySlider.IsEnabled = enabled;
        OpacityPanel.Opacity = enabled ? 1.0 : 0.5;
    }

    private void OnApplyTransparency(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStore.Load();
        settings.AcrylicEnabled = AcrylicSwitch.IsOn;
        settings.BackgroundOpacity = (int)OpacitySlider.Value;
        SettingsStore.Save(settings);
        App.Main?.ApplyBackdropSettings();
    }

    private void OnResetTransparency(object sender, RoutedEventArgs e)
    {
        _loading = true;
        AcrylicSwitch.IsOn = true;
        OpacitySlider.Value = 30;
        _loading = false;
        SetOpacityPanelEnabled(true);
        OpacityValue.Text = "30%";
    }

    private void OnQuit(object sender, RoutedEventArgs e) => App.Main?.ExitApplication();

    private void ShowAuth(InfoBarSeverity severity, string message)
    {
        AuthBar.Severity = severity;
        AuthBar.Message = message;
        AuthBar.IsOpen = true;
    }
}
