using System.Text;
using ShiftDiff.Cli;

// The semantic output uses box-drawing and emoji markers; without this the
// Windows console mangles them.
TryUseUtf8();

return CliRunner.Run(args, Console.Out, Console.Error);

static void TryUseUtf8() {
  try {
    Console.OutputEncoding = Encoding.UTF8;
  } catch (IOException) {
    // Redirected or closed output; the default encoding has to do.
  }
}
