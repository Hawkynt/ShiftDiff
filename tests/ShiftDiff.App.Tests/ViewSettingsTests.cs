using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace ShiftDiff.App.Tests;

// Every switch in the options bar has to change something: a setting that the
// window never reads is worse than no setting at all.
public class ViewSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-view", Guid.NewGuid().ToString("N"));

    public ViewSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [AvaloniaFact]
    public void ZoomIn_IncreasesTheDiffFontSize()
    {
        var window = Show();
        var before = window.FindControl<ListBox>("DiffList")!.FontSize;

        window.ZoomInCommand.Execute(null);
        Pump();

        Assert.True(window.FindControl<ListBox>("DiffList")!.FontSize > before);
    }

    [AvaloniaFact]
    public void ZoomOut_DecreasesTheDiffFontSize()
    {
        var window = Show();
        var before = window.FindControl<ListBox>("DiffList")!.FontSize;

        window.ZoomOutCommand.Execute(null);
        Pump();

        Assert.True(window.FindControl<ListBox>("DiffList")!.FontSize < before);
    }

    [AvaloniaFact]
    public void Zoom_IsClampedToAReadableRange()
    {
        var window = Show();

        for (var i = 0; i < 60; i++) window.ZoomOutCommand.Execute(null);
        Pump();

        Assert.True(window.FindControl<ListBox>("DiffList")!.FontSize >= 8);
    }

    [AvaloniaFact]
    public void WrapLines_TurnsOffTheHorizontalScrollBarSoRowsCanWrap()
    {
        var window = Show();
        var list = window.FindControl<ListBox>("DiffList")!;
        Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(list));

        window.FindControl<CheckBox>("WrapCheck")!.IsChecked = true;
        Pump();

        Assert.Equal(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(list));
    }

    [AvaloniaFact]
    public void HighContrast_AddsTheStyleClassThatStrengthensTheFills()
    {
        var window = Show();
        Assert.DoesNotContain("highcontrast", window.Classes);

        window.FindControl<CheckBox>("ContrastCheck")!.IsChecked = true;
        Pump();

        Assert.Contains("highcontrast", window.Classes);
    }

    [AvaloniaFact]
    public void RevisionBar_IsHiddenOutsideARepositorySession()
    {
        var window = Show();

        Assert.False(window.FindControl<StackPanel>("RevisionBar")!.IsVisible);
    }

    [AvaloniaFact]
    public void LayoutSelector_IsDisabledForAThreeWayComparison()
    {
        var basePath = Write("base.txt", "one\n");
        var localPath = Write("local.txt", "ONE\n");
        var remotePath = Write("remote.txt", "one\n");

        var window = Show(basePath, localPath, remotePath);
        PumpUntil(() => window.FindControl<ItemsControl>("PaneHeaders")!.ItemCount == 3);

        Assert.False(window.FindControl<ComboBox>("LayoutSelector")!.IsEnabled);
    }

    [AvaloniaFact]
    public void LayoutSelector_StaysEnabledForATwoWayComparison()
    {
        var window = Show(Write("a.txt", "one\n"), Write("b.txt", "two\n"));
        PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

        Assert.True(window.FindControl<ComboBox>("LayoutSelector")!.IsEnabled);
    }

    [AvaloniaFact]
    public void Swap_OnAFilePair_ExchangesTheSides()
    {
        var left = Write("a.txt", "left\n");
        var right = Write("b.txt", "right\n");
        var window = Show(left, right);
        PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

        var headers = window.FindControl<ItemsControl>("PaneHeaders")!;
        var firstBefore = headers.Items.Cast<string>().First();

        window.FindControl<Button>("SwapButton")!.Command?.Execute(null);
        Pump();

        Assert.Equal(firstBefore, headers.Items.Cast<string>().First());
    }

    [AvaloniaFact]
    public void ResolvePanel_InAThreeWayMerge_OffersBaseAndRemote()
    {
        var window = Show(
            Write("base.txt", TwoLines("two")),
            Write("local.txt", TwoLines("LOCAL")),
            Write("remote.txt", TwoLines("remote")));
        PumpUntil(() => window.FindControl<ItemsControl>("PaneHeaders")!.ItemCount == 3);

        Assert.True(window.FindControl<StackPanel>("ResolvePanel")!.IsVisible);
        Assert.True(window.FindControl<Button>("TakeRemoteButton")!.IsVisible);
        Assert.Equal("◀ Take base", window.FindControl<Button>("TakeLeftButton")!.Content);
    }

    [AvaloniaFact]
    public void ResolvePanel_InAFourWayComparison_IsHidden()
    {
        var window = Show(
            Write("base.txt", TwoLines("two")),
            Write("local.txt", TwoLines("LOCAL")),
            Write("remote.txt", TwoLines("remote")),
            Write("target.txt", TwoLines("LOCAL")));
        PumpUntil(() => window.FindControl<ItemsControl>("PaneHeaders")!.ItemCount == 4);

        Assert.False(window.FindControl<StackPanel>("ResolvePanel")!.IsVisible);
    }

    [AvaloniaFact]
    public void ResolvePanel_InATwoWayComparison_OffersOnlyTheLeftSide()
    {
        var window = Show(Write("a.txt", TwoLines("one")), Write("b.txt", TwoLines("two")));
        PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

        Assert.True(window.FindControl<StackPanel>("ResolvePanel")!.IsVisible);
        Assert.False(window.FindControl<Button>("TakeRemoteButton")!.IsVisible);
        Assert.Equal("◀ Take left", window.FindControl<Button>("TakeLeftButton")!.Content);
    }

    private MainWindow Show(params string[] args)
    {
        var window = new MainWindow(args);
        window.Show();
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    private static void PumpUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Thread.Sleep(5);
            Dispatcher.UIThread.RunJobs();
        }

        Pump();
    }

    private static string TwoLines(string second) => string.Join('\n', ["one", second, string.Empty]);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
