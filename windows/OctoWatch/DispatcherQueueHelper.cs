using System.Runtime.InteropServices;

namespace OctoWatch;

internal sealed class DispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        [MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController
    );

    private object? _controller;

    public void EnsureDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is not null)
            return;
        if (_controller is not null)
            return;

        DispatcherQueueOptions options;
        options.dwSize = Marshal.SizeOf<DispatcherQueueOptions>();
        options.threadType = 2;
        options.apartmentType = 2;

        object? controller = null;
        CreateDispatcherQueueController(options, ref controller);
        _controller = controller;
    }
}
