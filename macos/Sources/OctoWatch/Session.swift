import Foundation
import Security

// GitHub token storage in the macOS Keychain (the equivalent of the Windows
// Credential Manager used on the Windows side).
enum Keychain {
    static let service = "OctoWatch"
    static let account = "github"

    static func save(_ token: String) {
        delete()
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: Data(token.utf8),
        ]
        SecItemAdd(query as CFDictionary, nil)
    }

    static func load() -> String {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var result: AnyObject?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
            let data = result as? Data,
            let token = String(data: data, encoding: .utf8)
        else { return "" }
        return token
    }

    static func delete() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(query as CFDictionary)
    }
}

enum Session {
    static let defaultScopes = "repo read:org notifications"

    static var token: String { Keychain.load() }
    static var isSignedIn: Bool { !token.isEmpty }

    static func makeClient() throws -> Client { try Client(token: token) }
}
