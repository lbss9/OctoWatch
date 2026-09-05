using uniffi.octowatch_core;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
var client = new Client(token);
var repo = new Repo("cli", "cli");

var prs = client.ListPullRequests(repo);
Console.WriteLine($"Pull requests: {prs.Count}");
if (prs.Count > 0)
{
    Console.WriteLine($"  top: #{prs[0].number} {prs[0].title} ({prs[0].state}, merged={prs[0].merged})");
    var detail = client.GetPullRequest(repo, prs[0].number);
    Console.WriteLine(
        $"  detail: +{detail.additions} -{detail.deletions}, {detail.changedFiles} files, "
            + $"mergeable={detail.mergeable}, reviewers={detail.requestedReviewers.Count}, labels={detail.labels.Count}"
    );
}

var runs = client.ListWorkflowRuns(repo);
Console.WriteLine($"Workflow runs: {runs.Count}");
if (runs.Count > 0)
    Console.WriteLine($"  top: {runs[0].name} -> {runs[0].conclusion ?? runs[0].status}");

var branches = client.ListBranches(repo);
Console.WriteLine($"Branches: {branches.Count}");

if (!string.IsNullOrWhiteSpace(token))
{
    var repos = client.ListRepositories();
    Console.WriteLine($"Authenticated repos: {repos.Count}");
}

var device = OctowatchCoreMethods.StartDeviceLogin("repo");
Console.WriteLine($"Device flow: {device.userCode} → {device.verificationUri}");

Console.WriteLine("OK: FFI C# -> Rust -> GitHub working.");
