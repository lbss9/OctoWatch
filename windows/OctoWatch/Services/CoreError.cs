using uniffi.octowatch_core;

namespace OctoWatch;

/// <summary>
/// Helpers for the errors thrown across the UniFFI boundary. The generated
/// exceptions carry a raw "@msg=..." string in their Message; these expose the
/// clean text and flag authentication failures so the UI can react to them.
/// </summary>
internal static class CoreError
{
    public static bool IsAuth(Exception ex) => ex is OctoException.Auth;

    public static string Describe(Exception ex) =>
        ex switch
        {
            OctoException.Auth a => a.msg,
            OctoException.NotFound n => n.msg,
            OctoException.Api p => p.msg,
            _ => ex.Message,
        };
}
