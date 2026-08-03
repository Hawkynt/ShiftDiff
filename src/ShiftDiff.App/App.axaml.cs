using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace ShiftDiff.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyConfiguredTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyConfiguredTheme()
    {
        RequestedThemeVariant = Environment.GetEnvironmentVariable("SHIFTDIFF_THEME")?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            "system" => ThemeVariant.Default,
            _ => RequestedThemeVariant,
        };
    }
}
