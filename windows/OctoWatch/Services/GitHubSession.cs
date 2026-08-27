using uniffi.octowatch_core;

namespace OctoWatch;

internal static class GitHubSession
{
    public const string DefaultScopes = "repo read:org notifications";

    public static string Token => CredentialStore.LoadToken();

    public static bool IsSignedIn => Token.Length > 0;

    public static Client CreateClient() => new(Token);
}
