namespace Microsoft.Maui;

/// <summary>
/// Stub implementation of MAUI MainThread for testing without MAUI runtime.
/// </summary>
public static class MainThread
{
    public static void BeginInvokeOnMainThread(Action action)
    {
        action();
    }
}
