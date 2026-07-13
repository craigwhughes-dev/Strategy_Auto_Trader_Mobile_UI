namespace Microsoft.Maui.Controls;

using System.ComponentModel;

/// <summary>
/// Stub implementation of MAUI BindableObject for net10.0 builds without MAUI runtime.
/// This is used for unit testing; platform-specific builds use the actual MAUI implementation.
/// </summary>
public class BindableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
