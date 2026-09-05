import AppKit
import SwiftUI

// Content of the menu bar popover (MenuBarExtra .window style) -- the macOS
// equivalent of the Windows tray flyout.
struct FeedView: View {
    @EnvironmentObject var store: FeedStore

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Label("OctoWatch", systemImage: "dot.radiowaves.left.and.right")
                    .font(.headline)
                Spacer()
                if store.loading { ProgressView().controlSize(.small) }
                Button { Task { await store.refresh() } } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .buttonStyle(.borderless)
                SettingsLink { Image(systemName: "gearshape") }
                    .buttonStyle(.borderless)
            }
            .padding(10)
            Divider()

            Group {
                if !store.signedIn {
                    message("Sign in to GitHub in Settings to see your feed.")
                } else if let error = store.error {
                    message(error)
                } else if store.items.isEmpty {
                    message("Nothing to show yet.")
                } else {
                    List(store.items) { row($0) }
                        .listStyle(.inset)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            Divider()
            HStack {
                Text("\(store.items.count) items")
                    .font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button("Quit") { NSApplication.shared.terminate(nil) }
                    .buttonStyle(.borderless).font(.caption)
            }
            .padding(8)
        }
        .frame(width: 420, height: 560)
    }

    private func row(_ item: FeedItem) -> some View {
        HStack(spacing: 10) {
            RoundedRectangle(cornerRadius: 2).fill(item.state.color).frame(width: 4)
            Circle().fill(item.state.color).frame(width: 9, height: 9)
            VStack(alignment: .leading, spacing: 1) {
                Text(item.title).lineLimit(1)
                Text(item.subtitle).font(.caption).foregroundStyle(.secondary).lineLimit(1)
            }
            Spacer()
            Text(RelativeTime.ago(item.updatedAt))
                .font(.caption).foregroundStyle(.secondary)
        }
        .padding(.vertical, 3)
        .contentShape(Rectangle())
        .onTapGesture { open(item.url) }
    }

    private func message(_ text: String) -> some View {
        Text(text)
            .foregroundStyle(.secondary)
            .multilineTextAlignment(.center)
            .padding()
    }

    // Only open http/https links (parity with the Windows SafeUrl guard).
    private func open(_ string: String) {
        guard let url = URL(string: string),
            url.scheme == "http" || url.scheme == "https"
        else { return }
        NSWorkspace.shared.open(url)
    }
}
