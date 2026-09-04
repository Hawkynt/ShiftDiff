namespace ShiftDiff.Cli.Tests;

// Test fixture helper: every temp artifact lands under one directory that the
// test can throw away, so nothing leaks into the system temp root.
public sealed class TempWorkspace : IDisposable {
  public TempWorkspace() {
    Root = Path.Combine(Path.GetTempPath(), "shiftdiff-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Root);
  }

  public string Root { get; }

  public string File(string content, string extension = ".txt") {
    var path = Path.Combine(Root, Guid.NewGuid().ToString("N") + extension);
    System.IO.File.WriteAllText(path, content);
    return path;
  }

  public string FileNamed(string name, string content) {
    var path = Path.Combine(Root, name);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    System.IO.File.WriteAllText(path, content);
    return path;
  }

  public string Folder(string name) {
    var path = Path.Combine(Root, name);
    Directory.CreateDirectory(path);
    return path;
  }

  public string MissingPath(string extension = ".txt") =>
      Path.Combine(Root, Guid.NewGuid().ToString("N") + extension);

  public void Dispose() {
    try {
      if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    } catch (IOException) {
      // A leftover temp directory must never fail a test run.
    }
  }
}
