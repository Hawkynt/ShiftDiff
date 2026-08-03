using System.Windows.Input;

namespace ShiftDiff.App;

// Keyboard shortcuts bind to commands; the window has no other need for a
// command layer, so this is deliberately the smallest thing that works.
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
