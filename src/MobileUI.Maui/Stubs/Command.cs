namespace Microsoft.Maui.Controls;

/// <summary>
/// Stub implementation of MAUI Command for testing without MAUI runtime.
/// </summary>
public class Command : System.Windows.Input.ICommand
{
    private readonly Func<Task>? _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public Command(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        _execute?.Invoke();
    }

    public void ChangeCanExecute()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
