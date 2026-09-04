using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace ShiftDiff.App.Tests;

// Renders the window headlessly and, when SHIFTDIFF_SHOTS points at a
// directory, writes the frames there. Used to review the visual design without
// a display, and as a smoke test that a full frame renders at all.
public class ScreenshotTests : IDisposable {
  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-shots", Guid.NewGuid().ToString("N"));

  public ScreenshotTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [AvaloniaFact]
  public void SourceComparison_RendersAFullFrameInBothThemes() {
    var oldPath = Path.Combine(_root, "Sample.old.cs");
    var newPath = Path.Combine(_root, "Sample.new.cs");
    File.WriteAllText(oldPath, OldSource);
    File.WriteAllText(newPath, NewSource);

    var window = new MainWindow([oldPath, newPath]);
    window.Show();
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    Save(window, "dark.png");

    var themeSelector = window.FindControl<ComboBox>("ThemeSelector")!;
    themeSelector.SelectedIndex = 1;
    Pump();

    Save(window, "light.png");
  }

  [AvaloniaFact]
  public void ThreeWayMerge_RendersAFullFrame() {
    var basePath = Path.Combine(_root, "base.cs");
    var localPath = Path.Combine(_root, "local.cs");
    var remotePath = Path.Combine(_root, "remote.cs");
    File.WriteAllText(basePath, "public int Limit = 10;\npublic int Size = 4;\n");
    File.WriteAllText(localPath, "public int Limit = 20;\npublic int Size = 4;\n");
    File.WriteAllText(remotePath, "public int Limit = 30;\npublic int Size = 8;\n");

    var window = new MainWindow([basePath, localPath, remotePath]);
    window.Show();
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    Save(window, "threeway.png");
  }

  private void Save(Window window, string name) {
    using var frame = window.CaptureRenderedFrame();
    Assert.NotNull(frame);

    var target = Environment.GetEnvironmentVariable("SHIFTDIFF_SHOTS");
    if (string.IsNullOrEmpty(target)) return;

    Directory.CreateDirectory(target);
    frame!.Save(Path.Combine(target, name));
  }

  private static void Pump() {
    for (var i = 0; i < 8; i++) {
      Dispatcher.UIThread.RunJobs();
      Thread.Sleep(10);
    }
  }

  private static void PumpUntil(Func<bool> condition) {
    for (var i = 0; i < 300 && !condition(); i++) {
      Thread.Sleep(5);
      Dispatcher.UIThread.RunJobs();
    }

    Pump();
  }

  private const string OldSource = """
        using System;
        using System.Collections.Generic;

        namespace Demo;

        public sealed class OrderProcessor
        {
            private readonly List<Order> _orders = new();

            public bool Validate(Order order)
            {
                if (order.Total < 0)
                {
                    return false;
                }

                return order.Lines.Count > 0;
            }

            // Renders a short human readable summary of the order.
            public string Describe(Order order)
            {
                return $"{order.Id}: {order.Total:C} ({order.Lines.Count} lines)";
            }

            public void Add(Order order)
            {
                _orders.Add(order);
            }
        }
        """;

  private const string NewSource = """
        using System;
        using System.Collections.Generic;

        namespace Demo;

        public sealed class OrderProcessor
        {
            private readonly List<Order> _orders = new();
            private readonly ILogger _logger;

            // Renders a short human readable summary of the order.
            public string Describe(Order order)
            {
                return $"{order.Id}: {order.Total:C} ({order.Lines.Count} items)";
            }

            public bool Validate(Order order)
            {
                if (order.Total <= 0)
                {
                    return false;
                }

                return order.Lines.Count > 0;
            }

            public void Add(Order order)
            {
                _logger.Info("adding order");
                _orders.Add(order);
            }
        }
        """;
}
