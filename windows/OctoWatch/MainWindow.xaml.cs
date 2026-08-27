using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using uniffi.octowatch_core;
using Windows.Graphics;
using Windows.System;

namespace OctoWatch;

/// <summary>Card exibido na lista (workflow run = GitHub Action).</summary>
public sealed record RunCard(string Title, string Subtitle, string State, string Url);

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Fundo Acrylic — o mesmo material semi-transparente do menu Iniciar do Win11.
        SystemBackdrop = new DesktopAcrylicBackdrop();

        // Title bar customizada com título próprio.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // Janela compacta, estilo flyout do OneDrive.
        AppWindow.Resize(new SizeInt32(460, 640));

        // Conveniência para testar rápido.
        OwnerBox.Text = "cli";
        RepoBox.Text = "cli";

        // Carrega assim que a janela abre.
        Activated += OnFirstActivated;
    }

    private bool _loaded;
    private void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_loaded) return;
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
                    result.Add(new RunCard(
                        Title: string.IsNullOrEmpty(run.name) ? run.commitMessage : run.name,
                        Subtitle: $"{run.branch} · {detail} · {run.commitMessage}",
                        State: state,
                        Url: run.htmlUrl));
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
        if (status is not null) StatusText.Text = status;
    }

    /// <summary>Traduz status/conclusion do GitHub para o estado da bolinha.</summary>
    private static string MapState(string status, string? conclusion)
    {
        if (status != "completed") return "running"; // queued | in_progress
        return conclusion switch
        {
            "success" => "success",
            "failure" or "timed_out" or "startup_failure" or "action_required" => "failure",
            _ => "other", // cancelled | skipped | neutral | null
        };
    }
}
