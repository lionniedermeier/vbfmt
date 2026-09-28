# vbfmt

An opinionated VB.NET source formatter built on Roslyn.

> [!WARNING]
> **Proof of concept.** This project is not production ready and comes with
> no stability guarantees. Large parts of it were written with the help of Claude Code.

Roslyn's own `Formatter` normalizes indentation and spacing but never introduces a line break. vbfmt wraps them, breaking only where VB already continues a line so that no `_` is ever written. It also sorts, de-duplicates and groups `Imports`.

## Installation

vbfmt is packed as a .NET tool. Clone the repository, then build and install it from source with:

```powershell
git clone https://github.com/lionniedermeier/vbfmt.git
cd vbfmt
dotnet pack src/VisualBasicFormatter.Cli -o nupkg
dotnet tool install --global --add-source nupkg vbfmt
```

## Command Line Usage

```
vbfmt [SUBCOMMAND] [OPTIONS...] [PATHS...]
```

### Formatting

```
vbfmt format [OPTIONS...] [PATHS...]
```

Formats one or more Visual Basic source files in place.

| Option | Description |
| --- | --- |
| `--stdin` | Read source from standard input and write the formatted result to standard output. |
| `-v`, `--verbose` | Print one line per formatted file with the time it took. |
| `--summary` | Print how many files were formatted and how long the run took. |
| `--no-cache` | Neither read nor write the format cache (see [Cache](#cache)). |

### Checking

```
vbfmt check [OPTIONS...] [PATHS...]
```

Reports files that would be reformatted without writing to them. Exits `1` if any file would change,
which makes it suitable for CI (see [Exit codes](#exit-codes)).

- `--diff`: Print the changes as a unified diff instead of a file list.

### Creating a default config

```
vbfmt init [OPTIONS...]
```

Writes a `.vbfmtrc` with the default options into the current working directory.

- `--force`: Overwrite an existing `.vbfmtrc`.

### Shared options

`format` and `check` both accept:

| Option | Description |
| --- | --- |
| `--print-width <n>` | The column width lines are wrapped at (default 120). A target, not a hard ceiling. |
| `--indent-size <n>` | The number of characters per indentation level (default 4). |
| `--use-tabs [true\|false]` | Indent with tabs instead of spaces.Fix |
| `--end-of-line <Auto\|Lf\|CrLf>` | Line ending of the output: `Auto` (default, follows the file), `Lf` or `CrLf`. |
| `--language-version <version>` | The VB language version the parser assumes, e.g. `16.9` or `latest` (default). |
| `--no-organize-imports` | Leave the `Imports` statements untouched. |
| `--config <path>` | Path to a config file. Without it, `.vbfmtrc`, `.vbfmtrc.json`, `vbnet-format.json`, `vbnetformatrc` and `vbnetformatrc.json` are searched for, walking up from each file's own directory to the nearest repository root. |
| `--ignore-path <path>` | Path to a file of ignore patterns. Repeatable; replaces `.gitignore` and the `.vbfmtignore` family below. |
| `--no-respect-gitignore` | Do not read `.gitignore`. |
| `--no-ignore` | Read no ignore file at all. |

An option passed on the command line overrides the same setting in a config file, which in turn
overrides the built-in default.

### Selecting files

Each path argument is a file, a directory or a glob pattern. A directory is searched recursively for
`**/*.vb`. With no path argument, vbfmt searches the current directory. `bin`, `obj`, `node_modules`,
`.git`, `.svn`, `.hg` and generated `*.Designer.vb` files are always skipped.

### Ignoring files

vbfmt also skips paths matched by `.gitignore` and, if present in the working directory, an ignore
file named `.vbfmtignore`.
`--ignore-path` replaces both with one or more explicit ignore files; `--no-respect-gitignore` and
`--no-ignore` narrow or disable this without needing `--ignore-path`.

### Cache

`vbfmt format` keeps a cache of already-formatted files at `%LOCALAPPDATA%/vbfmt/cache.json` (per tool
version) and skips a file when neither its content nor the effective options have changed since the
last run. Pass `--no-cache` to ignore and skip writing to it.

### Exit codes

- `0`: Success. For `check`, no file would change.
- `1`: `check` found one or more files that would be reformatted.
- `2`: An error occurred, e.g. invalid VB.NET source or a malformed config file.

## Trademark Disclaimer

vbfmt is an independent, non-commercial, open-source project and is not affiliated with, sponsored by, or endorsed by Microsoft Corporation.

Visual Basic, VB.NET, and .NET are trademarks of Microsoft Corporation. They are used solely to identify the technologies supported by this project.
