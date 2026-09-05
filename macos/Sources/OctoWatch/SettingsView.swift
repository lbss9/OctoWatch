import AppKit
import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var store: FeedStore

    @State private var signedIn = Session.isSignedIn
    @State private var whoami = ""
    @State private var userCode = ""
    @State private var verificationUri = ""
    @State private var deviceCode = ""
    @State private var polling = false
    @State private var status = ""

    private let pollingOptions = [30, 60, 120, 300, 600, 900, 1800, 3600]

    var body: some View {
        Form {
            Section("Account") {
                if signedIn {
                    Text(whoami.isEmpty ? "Signed in." : "Signed in as \(whoami).")
                    Button("Sign out", role: .destructive) {
                        Keychain.delete()
                        signedIn = false
                        whoami = ""
                    }
                } else if !userCode.isEmpty {
                    Text("Enter this code at github.com/login/device:")
                        .foregroundStyle(.secondary)
                    Text(userCode).font(.title2.monospaced()).textSelection(.enabled)
                    HStack {
                        Button("Copy code") { copyToPasteboard(userCode) }
                        Button("Open GitHub") { openURL(verificationUri) }
                    }
                    HStack {
                        if polling { ProgressView().controlSize(.small) }
                        Text(status).font(.caption).foregroundStyle(.secondary)
                    }
                } else {
                    Button("Sign in with GitHub") { Task { await startLogin() } }
                }
            }

            Section("Repository") {
                TextField("Owner", text: $store.owner)
                TextField("Repository", text: $store.repo)
            }

            Section("Polling") {
                Picker("Interval", selection: $store.pollingSeconds) {
                    ForEach(pollingOptions, id: \.self) { Text(pollingLabel($0)).tag($0) }
                }
                .onChange(of: store.pollingSeconds) { _, _ in store.scheduleTimer() }
            }
        }
        .formStyle(.grouped)
        .frame(width: 400, height: 440)
        .task { await refreshAccount() }
    }

    private func pollingLabel(_ seconds: Int) -> String {
        seconds < 60 ? "\(seconds)s" : (seconds < 3600 ? "\(seconds / 60)m" : "\(seconds / 3600)h")
    }

    private func refreshAccount() async {
        signedIn = Session.isSignedIn
        guard signedIn else { return }
        whoami = (try? await Task.detached { try Session.makeClient().whoami() }.value) ?? ""
    }

    private func startLogin() async {
        do {
            let code = try await Task.detached { try startDeviceLogin(scopes: Session.defaultScopes) }.value
            userCode = code.userCode
            verificationUri = code.verificationUri
            deviceCode = code.deviceCode
            openURL(code.verificationUri)
            await pollLoop(interval: Int(code.interval), expiresIn: Int(code.expiresIn))
        } catch {
            status = "\(error)"
        }
    }

    private func pollLoop(interval: Int, expiresIn: Int) async {
        polling = true
        status = "Waiting for authorization…"
        var wait = max(interval, 1)
        let deadline = Date().addingTimeInterval(TimeInterval(expiresIn))
        defer { polling = false }

        while Date() < deadline {
            try? await Task.sleep(nanoseconds: UInt64(wait) * 1_000_000_000)
            let code = deviceCode
            guard let result = try? await Task.detached(operation: { try pollDeviceLogin(deviceCode: code) }).value
            else { continue }

            switch result {
            case .pending:
                continue
            case .slowDown:
                wait += 5
            case .expired:
                status = "The code expired. Try again."
                return
            case .denied:
                status = "Access denied."
                return
            case .authorized(let token):
                Keychain.save(token)
                userCode = ""
                signedIn = true
                await refreshAccount()
                await store.refresh()
                return
            }
        }
        status = "The code expired. Try again."
    }

    private func copyToPasteboard(_ text: String) {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    private func openURL(_ string: String) {
        guard let url = URL(string: string), url.scheme == "https" else { return }
        NSWorkspace.shared.open(url)
    }
}
