import AppKit
import SwiftUI

@main
struct OctoWatchApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @StateObject private var store = FeedStore()

    var body: some Scene {
        // The menu bar item (top of the screen). `.window` style opens a popover
        // when clicked -- the macOS counterpart of the Windows tray flyout.
        MenuBarExtra("OctoWatch", systemImage: "dot.radiowaves.left.and.right") {
            FeedView()
                .environmentObject(store)
                .task { store.start() }
        }
        .menuBarExtraStyle(.window)

        // Standard macOS Settings scene (⌘,).
        Settings {
            SettingsView().environmentObject(store)
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Menu-bar-only app: hide the Dock icon.
        NSApplication.shared.setActivationPolicy(.accessory)
    }
}
