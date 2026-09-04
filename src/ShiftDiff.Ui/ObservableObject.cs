using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShiftDiff.Ui;

// Minimal change-notification base so the presentation layer stays free of any
// UI framework dependency and remains unit-testable.
public abstract class ObservableObject : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;

    field = value;
    Raise(propertyName);
    return true;
  }

  protected void Raise([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
