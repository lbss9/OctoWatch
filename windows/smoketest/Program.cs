// Smoke test do FFI: C# -> núcleo Rust -> API do GitHub.
using uniffi.octowatch_core;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
var client = new Client(token);
var repo = new Repo("cli", "cli");

var prs = client.ListPullRequests(repo);
Console.WriteLine($"Pull requests: {prs.Count}");
if (prs.Count > 0)
    Console.WriteLine($"  topo: #{prs[0].number} {prs[0].title} ({prs[0].state})");

var runs = client.ListWorkflowRuns(repo);
Console.WriteLine($"Workflow runs: {runs.Count}");
if (runs.Count > 0)
    Console.WriteLine($"  topo: {runs[0].name} -> {runs[0].conclusion ?? runs[0].status}");

var branches = client.ListBranches(repo);
Console.WriteLine($"Branches: {branches.Count}");

Console.WriteLine("OK: FFI C# -> Rust -> GitHub funcionando.");
