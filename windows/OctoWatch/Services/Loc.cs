using Windows.ApplicationModel.Resources;

namespace OctoWatch;

internal static class Loc
{
    private static ResourceLoader? _loader;

    public static void Reset() => _loader = null;

    public static string Get(string key)
    {
        try
        {
            _loader ??= new ResourceLoader();
            var value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }
}
