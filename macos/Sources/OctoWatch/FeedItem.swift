import SwiftUI

enum FeedState {
    case success, failure, running, other

    var color: Color {
        switch self {
        case .success: return Color(red: 0.18, green: 0.63, blue: 0.26)
        case .failure: return Color(red: 0.84, green: 0.23, blue: 0.23)
        case .running: return Color(red: 0.89, green: 0.70, blue: 0.25)
        case .other: return .gray
        }
    }
}

// One row in the feed. Mirrors the FeedItem the Windows app builds, kept as a
// view model here (the raw core models come from the generated bindings).
struct FeedItem: Identifiable {
    let id: String
    let kind: String  // "action" | "pr" | "branch"
    let title: String
    let subtitle: String
    let state: FeedState
    let url: String
    let updatedAt: String
    var pullNumber: Int64 = 0
    var repoFullName: String = ""
}

enum FeedMapping {
    static func runState(_ status: String, _ conclusion: String?) -> FeedState {
        if status != "completed" { return .running }
        switch conclusion {
        case "success": return .success
        case "failure", "timed_out", "startup_failure", "action_required": return .failure
        default: return .other
        }
    }

    static func pullState(state: String, draft: Bool, merged: Bool) -> FeedState {
        if merged { return .success }
        if state == "open" { return draft ? .other : .running }
        return .other
    }
}

enum RelativeTime {
    static func ago(_ iso: String) -> String {
        let formatter = ISO8601DateFormatter()
        guard !iso.isEmpty, let date = formatter.date(from: iso) else { return "" }
        let seconds = Date().timeIntervalSince(date)
        switch seconds {
        case ..<45: return "just now"
        case ..<3600: return "\(Int(seconds / 60))m ago"
        case ..<86400: return "\(Int(seconds / 3600))h ago"
        case ..<172_800: return "yesterday"
        default: return "\(Int(seconds / 86400))d ago"
        }
    }
}
