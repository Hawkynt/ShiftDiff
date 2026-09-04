using Avalonia;
using Avalonia.Headless;
using ShiftDiff.App;
using ShiftDiff.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace ShiftDiff.App.Tests;

// Runs the real application (styles, themes, templates) on the headless
// platform so the window and its data templates are exercised for real.
public static class TestAppBuilder {
  public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<global::ShiftDiff.App.App>()
          .UseSkia()
          .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
