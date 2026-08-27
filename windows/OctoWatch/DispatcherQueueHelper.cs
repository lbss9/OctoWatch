using System.Runtime.InteropServices;

namespace OctoWatch;

/// <summary>
/// Garante um DispatcherQueueController no thread da UI — pré-requisito dos
/// controllers de backdrop (Acrylic/Mica) do Windows App SDK. Em apps WinUI o
/// thread principal já tem uma DispatcherQueue, então normalmente é no-op.
/// </summary>
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
        options.threadType = 2; // DQTYPE_THREAD_CURRENT
        options.apartmentType = 2; // DQTAT_COM_STA

        object? controller = null;
        CreateDispatcherQueueController(options, ref controller);
        _controller = controller;
    }
}
