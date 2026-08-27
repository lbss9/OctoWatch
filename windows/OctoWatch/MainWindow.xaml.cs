using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using uniffi.octowatch_core;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using WinRT;

namespace OctoWatch;

/// <summary>Card exibido na lista (workflow run = GitHub Action).</summary>
public sealed record RunCard(string Title, string Subtitle, string State, string Url);

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Barra de título nativa (botões min/max/close do sistema) com título próprio.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // Maximizar desativado; mantém minimizar e fechar.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        // Efeito vidro: Acrylic translúcido customizado (blur nativo).
        TrySetGlassBackdrop();

        // Fechar e minimizar recolhem para a bandeja (o app continua rodando).
        AppWindow.Closing += OnWindowClosing;
        AppWindow.Changed += OnWindowChanged;
        SetupTray();

        // Janela compacta ancorada no canto inferior direito, estilo flyout do OneDrive.
        PositionBottomRight(460, 640);

        // Conveniência para testar rápido.
        OwnerBox.Text = "cli";
        RepoBox.Text = "cli";

        // Carrega assim que a janela abre.
        Activated += OnFirstActivated;
    }

    /// <summary>Ancora a janela no canto inferior direito da área de trabalho (acima da barra de tarefas).</summary>
    private void PositionBottomRight(int width, int height)
    {
        AppWindow.Resize(new SizeInt32(width, height));
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        const int margin = 12;
        int x = work.X + work.Width - width - margin;
        int y = work.Y + work.Height - height - margin;
        AppWindow.Move(new PointInt32(x, y));
    }

    private bool _loaded;

    private void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_loaded)
            return;
        _loaded = true;
        OnRefresh(this, new RoutedEventArgs());
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        var owner = OwnerBox.Text.Trim();
        var name = RepoBox.Text.Trim();
        var token = TokenBox.Password;

        if (owner.Length == 0 || name.Length == 0)
        {
            StatusText.Text = "Informe owner e repositório.";
            return;
        }

        SetBusy(true, $"Carregando GitHub Actions de {owner}/{name}…");
        try
        {
            var cards = await Task.Run(() =>
            {
                var client = new Client(token);
                var result = new List<RunCard>();
                foreach (var run in client.ListWorkflowRuns(new Repo(owner, name)))
                {
                    var state = MapState(run.status, run.conclusion);
                    var detail = run.conclusion ?? run.status;
                    result.Add(
                        new RunCard(
                            Title: string.IsNullOrEmpty(run.name) ? run.commitMessage : run.name,
                            Subtitle: $"{run.branch} · {detail} · {run.commitMessage}",
                            State: state,
                            Url: run.htmlUrl
                        )
                    );
                }
                return result;
            });

            RunsList.ItemsSource = cards;
            StatusText.Text = $"{cards.Count} runs · {owner}/{name}";
        }
        catch (Exception ex)
        {
            RunsList.ItemsSource = null;
            StatusText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RunCard card && !string.IsNullOrEmpty(card.Url))
            await Launcher.LaunchUriAsync(new Uri(card.Url));
    }

    private void SetBusy(bool busy, string? status)
    {
        Spinner.IsActive = busy;
        RefreshButton.IsEnabled = !busy;
        if (status is not null)
            StatusText.Text = status;
    }

    /// <summary>Traduz status/conclusion do GitHub para o estado da bolinha.</summary>
    private static string MapState(string status, string? conclusion)
    {
        if (status != "completed")
            return "running"; // queued | in_progress
        return conclusion switch
        {
            "success" => "success",
            "failure" or "timed_out" or "startup_failure" or "action_required" => "failure",
            _ => "other", // cancelled | skipped | neutral | null
        };
    }

    // --- Efeito vidro (Acrylic customizado) -----------------------------

    private DesktopAcrylicController? _glass;
    private SystemBackdropConfiguration? _backdropConfig;
    private DispatcherQueueHelper? _dispatcherHelper;

    /// <summary>
    /// Aplica um Acrylic translúcido (vidro com blur) via controller próprio,
    /// com opacidades baixas para ficar bem transparente — estilo TranslucentTB.
    /// </summary>
    private void TrySetGlassBackdrop()
    {
        if (!DesktopAcrylicController.IsSupported())
            return; // sem suporte (ex.: RDP antigo) — janela fica com cor sólida de fallback

        _dispatcherHelper = new DispatcherQueueHelper();
        _dispatcherHelper.EnsureDispatcherQueueController();

        _backdropConfig = new SystemBackdropConfiguration { IsInputActive = true };
        Activated += (_, e) =>
            _backdropConfig.IsInputActive =
                e.WindowActivationState != WindowActivationState.Deactivated;
        Closed += (_, _) =>
        {
            _glass?.Dispose();
            _glass = null;
        };
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => UpdateGlassTheme(root);
            UpdateGlassTheme(root);
        }

        _glass = new DesktopAcrylicController
        {
            // Opacidades baixas = mais "vidro" (vê-se o desktop através, com blur).
            TintColor = Color.FromArgb(255, 24, 24, 28),
            TintOpacity = 0.15f,
            LuminosityOpacity = 0.25f,
            FallbackColor = Color.FromArgb(255, 42, 42, 46),
        };
        _glass.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _glass.SetSystemBackdropConfiguration(_backdropConfig);
    }

    private void UpdateGlassTheme(FrameworkElement root)
    {
        if (_backdropConfig is null)
            return;
        _backdropConfig.Theme = root.ActualTheme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            ElementTheme.Light => SystemBackdropTheme.Light,
            _ => SystemBackdropTheme.Default,
        };
    }

    // --- Bandeja (tray) -------------------------------------------------

    private bool _allowClose;
    private TaskbarIcon? _tray;

    /// <summary>Cria o ícone da bandeja em código (evita o markup do H.NotifyIcon no XAML).</summary>
    private void SetupTray()
    {
        var open = new MenuFlyoutItem { Text = "Abrir" };
        open.Click += OnTrayOpen;
        var exit = new MenuFlyoutItem { Text = "Sair" };
        exit.Click += OnTrayExit;

        var menu = new MenuFlyout();
        menu.Items.Add(open);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exit);

        _tray = new TaskbarIcon
        {
            ToolTipText = "OctoWatch",
            LeftClickCommand = new RelayCommand(ShowFromTray),
            ContextFlyout = menu,
            IconSource = new GeneratedIconSource
            {
                Text = "O",
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
        _tray.ForceCreate();
    }

    /// <summary>Interceta o X: em vez de sair, esconde para a bandeja.</summary>
    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
            return; // saída real (menu "Sair")
        args.Cancel = true;
        HideToTray();
    }

    /// <summary>Minimizar (botão nativo) também recolhe para a bandeja.</summary>
    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (
            AppWindow.Presenter is OverlappedPresenter p
            && p.State == OverlappedPresenterState.Minimized
        )
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        // Sai do estado minimizado para reabrir "restaurado" da próxima vez.
        if (
            AppWindow.Presenter is OverlappedPresenter p
            && p.State == OverlappedPresenterState.Minimized
        )
        {
            p.Restore();
        }
        AppWindow.Hide();
    }

    private void ShowFromTray()
    {
        PositionBottomRight(460, 640);
        AppWindow.Show();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);
    }

    private void OnTrayOpen(object sender, RoutedEventArgs e) => ShowFromTray();

    private void OnTrayExit(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        _tray?.Dispose();
        this.Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
