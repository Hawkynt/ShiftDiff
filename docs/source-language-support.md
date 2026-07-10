# Source language support

ShiftDiff detects a source language from the file extension and, for extensionless scripts,
from common shebangs. Detection is local and deterministic; file content is never uploaded.

The first language-aware comparison layer supports:

- C#, JavaScript, TypeScript, and Java
- C and C++
- Python, Go, and Rust
- PHP, Perl, and Ruby
- Visual Basic and VBScript
- HTML, CSS, and SQL

Each profile recognizes its keywords, comments, strings, identifiers, numeric literals,
operators, punctuation, and whitespace while preserving every input character. The tokenizer
API is independent of the desktop UI, so future language profiles can be added without
changing the comparison engine or renderer.

This is lexical awareness, not an AST diff. ShiftDiff uses the language tokens to make inline
changes more accurate—for example, URLs containing `//` remain strings instead of becoming
comments. AST-assisted refactoring detection remains outside the MVP scope.

## Adding a language

1. Add the language to `SourceLanguage`.
2. Register extensions or a shebang in `SourceLanguageDetector`.
3. Add a `LanguageProfile` in `SourceTokenizer`.
4. Add extension, comment/string-boundary, and lossless reconstruction tests.

The lossless reconstruction test is mandatory: concatenating all produced token texts must
reproduce the original line byte-for-byte at the character level. A tokenizer that eats source
code is not a tokenizer; it is a very small and unusually selective shredder.

