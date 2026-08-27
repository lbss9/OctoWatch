using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using uniffi.octowatch_core;

namespace OctoWatch;

/// <summary>Item exibido na lista (achatado para binding simples no XAML).</summary>
public sealed record ResultItem(string Primary, string Secondary, string Url);

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
    }

    private async void OnLoadPullRequests(object sender, RoutedEventArgs e) =>
        await Load("Pull Requests", repo =>
        {
            var items = new List<ResultItem>();
            foreach (var pr in Core(repo.Item1).ListPullRequests(repo.Item2))
            {
                var flag = pr.draft ? " · draft" : "";
                items.Add(new ResultItem(
                    $"#{pr.number}  {pr.title}",
                    $"{pr.author} · {pr.state}{flag} · {pr.headBranch} → {pr.baseBranch}",
                    pr.htmlUrl));
            }
            return items;
        });

    private async void OnLoadWorkflowRuns(object sender, RoutedEventArgs e) =>
        await Load("Workflow Runs", repo =>
        {
            var items = new List<ResultItem>();
            foreach (var run in Core(repo.Item1).ListWorkflowRuns(repo.Item2))
            {
                var result = run.conclusion ?? run.status;
                items.Add(new ResultItem(
                    $"{Glyph(run.conclusion, run.status)}  {run.name}",
                    $"{result} · {run.branch} · {run.commitMessage}",
                    run.htmlUrl));
            }
            return items;
        });

    private async void OnLoadBranches(object sender, RoutedEventArgs e) =>
        await Load("Branches", repo =>
        {
            var items = new List<ResultItem>();
            foreach (var b in Core(repo.Item1).ListBranches(repo.Item2))
            {
                var prot = b.@protected ? " · protegida" : "";
                items.Add(new ResultItem(b.name, $"{b.lastCommitSha[..7]}{prot}", ""));
            }
            return items;
        });

    // -----------------------------------------------------------------

    /// <summary>Cria o cliente do núcleo Rust com o token informado.</summary>
    private static Client Core(string token) => new Client(token);

    /// <summary>
    /// Executa a chamada bloqueante fora da thread de UI e atualiza a lista.
    /// O tuple carrega (token, repo) para a lambda.
    /// </summary>
    private async Task Load(string what, Func<(string, Repo), List<ResultItem>> work)
    {
        var owner = OwnerBox.Text.Trim();
        var name = RepoBox.Text.Trim();
        var token = TokenBox.Password;

        if (owner.Length == 0 || name.Length == 0)
        {
            StatusText.Text = "Informe owner e repositório.";
            return;
        }

        SetBusy(true, $"Carregando {what} de {owner}/{name}…");
        try
        {
            var repo = new Repo(owner, name);
            var items = await Task.Run(() => work((token, repo)));
            ResultsList.ItemsSource = items;
            StatusText.Text = $"{items.Count} {what.ToLower()} · {owner}/{name}";
        }
        catch (Exception ex)
        {
            ResultsList.ItemsSource = null;
            StatusText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? status)
    {
        Spinner.IsActive = busy;
        PrButton.IsEnabled = !busy;
        RunsButton.IsEnabled = !busy;
        BranchesButton.IsEnabled = !busy;
        if (status is not null) StatusText.Text = status;
    }

    private static string Glyph(string? conclusion, string status)
    {
        if (status != "completed") return "•";
        return conclusion switch
        {
            "success" => "✓",
            "failure" => "✗",
            "cancelled" => "⊘",
            _ => "•",
        };
    }
}
