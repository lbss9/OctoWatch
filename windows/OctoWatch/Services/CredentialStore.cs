using Windows.Security.Credentials;

namespace OctoWatch;

internal static class CredentialStore
{
    private const string Resource = "OctoWatch";
    private const string UserName = "github";

    public static void SaveToken(string token)
    {
        Clear();
        var vault = new PasswordVault();
        vault.Add(new PasswordCredential(Resource, UserName, token));
    }

    public static string LoadToken()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(Resource, UserName);
            credential.RetrievePassword();
            return credential.Password ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static void Clear()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(Resource, UserName);
            vault.Remove(credential);
        }
        catch
        {
        }
    }
}
