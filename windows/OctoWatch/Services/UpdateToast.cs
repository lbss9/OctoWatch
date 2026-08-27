using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace OctoWatch;

internal static class UpdateToast
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        try
        {
            if (!AppNotificationManager.IsSupported())
                return;
            AppNotificationManager.Default.NotificationInvoked += OnInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch
        {
        }
    }

    public static void Unregister()
    {
        if (!_registered)
            return;
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
        }
        _registered = false;
    }

    public static void ShowNewItems(IReadOnlyList<FeedItem> items)
    {
        if (items.Count == 0)
            return;
        try
        {
            if (!AppNotificationManager.IsSupported())
                return;
            Register();
            if (items.Count == 1)
            {
                Show(items[0].Title, items[0].Subtitle, items[0].Url);
                return;
            }
            Show(
                string.Format(Loc.Get("Notify_ManyTitle"), items.Count),
                items[0].Title,
                items[0].Url
            );
        }
        catch
        {
        }
    }

    private static void Show(string title, string body, string url)
    {
        var notification = new AppNotificationBuilder()
            .AddArgument("url", Uri.EscapeDataString(url ?? ""))
            .AddText("OctoWatch")
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }

    private static void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        var encoded = "";
        if (args.Arguments.TryGetValue("url", out var value))
            encoded = value;
        App.Main?.DispatcherQueue.TryEnqueue(async () =>
        {
            App.Main?.ShowFromTray();
            if (string.IsNullOrEmpty(encoded))
                return;
            await SafeUrl.OpenAsync(Uri.UnescapeDataString(encoded));
        });
    }
}
