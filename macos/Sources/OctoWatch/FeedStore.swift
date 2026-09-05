import SwiftUI

// Polls the configured repository through the shared Rust core and exposes the
// feed to SwiftUI. The core calls are blocking, so they run off the main actor.
@MainActor
final class FeedStore: ObservableObject {
    @Published var items: [FeedItem] = []
    @Published var error: String?
    @Published var loading = false
    @Published var signedIn = Session.isSignedIn

    // Persisted preferences. @AppStorage is for Views, so back these with
    // UserDefaults manually inside the ObservableObject.
    @Published var owner = UserDefaults.standard.string(forKey: "manualOwner") ?? "cli" {
        didSet { UserDefaults.standard.set(owner, forKey: "manualOwner") }
    }
    @Published var repo = UserDefaults.standard.string(forKey: "manualRepo") ?? "cli" {
        didSet { UserDefaults.standard.set(repo, forKey: "manualRepo") }
    }
    @Published var pollingSeconds =
        (UserDefaults.standard.object(forKey: "pollingSeconds") as? Int) ?? 60
    {
        didSet { UserDefaults.standard.set(pollingSeconds, forKey: "pollingSeconds") }
    }

    private var timer: Timer?

    func start() {
        Task { await refresh() }
        scheduleTimer()
    }

    func scheduleTimer() {
        timer?.invalidate()
        let interval = TimeInterval(max(30, pollingSeconds))
        timer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { await self?.refresh() }
        }
    }

    func refresh() async {
        signedIn = Session.isSignedIn
        let owner = self.owner
        let repo = self.repo
        guard !owner.isEmpty, !repo.isEmpty else { return }

        loading = true
        defer { loading = false }
        do {
            let fetched = try await Task.detached(priority: .userInitiated) {
                try FeedStore.fetch(owner: owner, repo: repo)
            }.value
            items = fetched
            error = nil
        } catch {
            // TODO: mirror the Windows CoreError handling (drop the token and
            // prompt re-sign-in on an auth failure).
            self.error = "\(error)"
        }
    }

    // Runs on a background task; must not touch @MainActor state.
    private static func fetch(owner: String, repo: String) throws -> [FeedItem] {
        let client = try Session.makeClient()
        let r = Repo(owner: owner, name: repo)
        var out: [FeedItem] = []

        for run in try client.listWorkflowRuns(repo: r) {
            out.append(
                FeedItem(
                    id: "a:\(owner)/\(repo):\(run.id)",
                    kind: "action",
                    title: run.name.isEmpty ? run.commitMessage : run.name,
                    subtitle: "\(owner)/\(repo) · \(run.branch)",
                    state: FeedMapping.runState(run.status, run.conclusion),
                    url: run.htmlUrl,
                    updatedAt: run.updatedAt))
        }
        for pr in try client.listPullRequests(repo: r) {
            out.append(
                FeedItem(
                    id: "p:\(owner)/\(repo):\(pr.number)",
                    kind: "pr",
                    title: "#\(pr.number) \(pr.title)",
                    subtitle: "\(owner)/\(repo) · \(pr.author) · \(pr.headBranch) → \(pr.baseBranch)",
                    state: FeedMapping.pullState(state: pr.state, draft: pr.draft, merged: pr.merged),
                    url: pr.htmlUrl,
                    updatedAt: pr.updatedAt,
                    pullNumber: pr.number,
                    repoFullName: "\(owner)/\(repo)"))
        }
        for branch in try client.listBranches(repo: r) {
            out.append(
                FeedItem(
                    id: "b:\(owner)/\(repo):\(branch.name)",
                    kind: "branch",
                    title: branch.name,
                    subtitle: "\(owner)/\(repo) · \(branch.lastCommitSha.prefix(7))",
                    state: branch.protected ? .other : .success,
                    url: "https://github.com/\(owner)/\(repo)/tree/\(branch.name)",
                    updatedAt: ""))
        }
        return out
    }
}
