using System.Collections.Immutable;
using System.Text;

namespace VisualBasicFormatter.Printing;

/// <summary>
/// Renders a <see cref="Doc"/> to text. Every group is measured against the column limit and printed
/// flat if it fits; a group that does not fit prints its own line breaks, while the groups nested in
/// it are measured again from the column they then start at.
/// </summary>
internal sealed class DocPrinter
{
    private readonly PrintOptions _options;
    private readonly StringBuilder _output = new();
    private readonly Stack<Command> _commands = new();
    private readonly List<Command> _lineSuffixes = [];

    // Reused across every Fits call; Fits never runs while another Fits is on the stack.
    private readonly Stack<Command> _fitsQueue = new();

    // Space-indent strings by width. Widths repeat heavily -- one per level, plus the align columns.
    private readonly Dictionary<int, string> _indents = new() { [0] = "" };

    private int _column;
    private int _lineStart;
    private int _rootBreaks;

    private DocPrinter(PrintOptions options) => _options = options;

    private Indentation Deeper(Indentation current)
    {
        if (_options.UseTabs)
        {
            return new Indentation(
                current.Text + '\t',
                TextWidth.Advance(current.Width, '\t', _options.IndentSize)
            );
        }

        var width = current.Width + _options.IndentSize;
        return new Indentation(Spaces(width), width);
    }

    private string Spaces(int width) =>
        _indents.TryGetValue(width, out var text) ? text : _indents[width] = new string(' ', width);

    private enum PrintMode
    {
        Flat,
        Break,
    }

    /// <summary>Renders <paramref name="document"/>.</summary>
    public static string Print(Doc document, PrintOptions options) =>
        new DocPrinter(options).Run(document);

    private string Run(Doc document)
    {
        _commands.Push(new Command(Indentation.Root, PrintMode.Break, document));

        while (_commands.Count > 0 || _lineSuffixes.Count > 0)
        {
            // A comment parked by LineSuffix still has to be emitted when nothing follows it.
            if (_commands.Count == 0)
            {
                PushLineSuffixes();
                continue;
            }

            Step(_commands.Pop());
        }

        TrimTrailingWhitespace();

        return _output.ToString();
    }

    private void Step(Command command)
    {
        switch (command.Doc)
        {
            case DocNothing or DocExpandParent:
                break;

            case DocText text:
                Write(text.Value);
                break;

            case DocConcat concat:
                PushReversed(command, concat.Parts);
                break;

            case DocIndent indent:
                _commands.Push(
                    command with
                    {
                        Indent = Deeper(command.Indent),
                        Doc = indent.Content,
                    }
                );
                break;

            // The align column is the column reached right here, which is what makes
            // WrapStyle.Align land flush behind the opening paren.
            case DocAlign align:
                _commands.Push(
                    command with
                    {
                        Indent = new Indentation(Spaces(_column), _column),
                        Doc = align.Content,
                    }
                );
                break;

            case DocDedentToRoot dedent:
                _commands.Push(
                    command with
                    {
                        Indent = command.Root ?? command.Indent,
                        AtRoot = command.Root is not null,
                        Doc = dedent.Content,
                    }
                );
                break;

            case DocRootChoice rootChoice:
                PrintRootChoice(command, rootChoice);
                break;

            case DocWholeFit wholeFit:
                _commands.Push(command with { Doc = wholeFit.Content });
                break;

            case DocGroup group:
                _commands.Push(
                    command with
                    {
                        Mode = ModeFor(group, command),
                        Doc = group.Content,
                    }
                );
                break;

            case DocConditionalGroup choice:
                PushChosenState(command, choice);
                break;

            case DocConditional conditional:
                _commands.Push(
                    command with
                    {
                        Doc =
                            command.Mode == PrintMode.Break
                                ? conditional.WhenBroken
                                : conditional.WhenFlat,
                    }
                );
                break;

            case DocLineSuffix suffix:
                _lineSuffixes.Add(command with { Doc = suffix.Content });
                break;

            case DocLine line:
                PrintLine(command, line);
                break;

            case DocVerbatim verbatim:
                PrintVerbatim(command, verbatim);
                break;

            case DocFill fill:
                PrintFill(command, fill);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unhandled doc element {command.Doc.GetType().Name}."
                );
        }
    }

    private PrintMode ModeFor(DocGroup group, Command command)
    {
        // Content that breaks unconditionally is never worth measuring.
        if (group.Expands)
        {
            return PrintMode.Break;
        }

        // Inside a group that already fits flat, everything nested fits too.
        if (command.Mode == PrintMode.Flat)
        {
            return PrintMode.Flat;
        }

        return Fits(command with { Mode = PrintMode.Flat, Doc = group.Content })
            ? PrintMode.Flat
            : PrintMode.Break;
    }

    /// <summary>
    /// The first layout whose line still fits, or the most expanded one.
    /// </summary>
    /// <remarks>
    /// The chosen state is pushed flat even though it may contain a group that breaks:
    /// <see cref="ModeFor"/> answers for <see cref="DocGroup.ShouldBreak"/> before it looks at the
    /// enclosing mode, so exactly the breaks this layout was built around are taken and nothing
    /// else. That is what keeps the elements in front of an expanded one on their line.
    /// </remarks>
    private void PushChosenState(Command command, DocConditionalGroup choice)
    {
        for (var i = 0; i < choice.States.Length - 1; i++)
        {
            if (command.InFallback && choice.States[i] is DocWholeFit)
            {
                continue;
            }

            var state = command with { Mode = PrintMode.Flat, Doc = choice.States[i] };

            if (Fits(state, declaredBreaksOnly: true, wholeFit: state.Doc is DocWholeFit))
            {
                _commands.Push(state);
                return;
            }
        }

        _commands.Push(command with { Mode = PrintMode.Break, Doc = choice.States[^1] });
    }

    private void PrintRootChoice(Command command, DocRootChoice choice)
    {
        var root = command with { Root = command.Indent, AtRoot = false };
        var depth = _commands.Count;
        var lineStart = _lineStart;
        var column = _column;
        var lineText = _output.ToString(lineStart, _output.Length - lineStart);
        var suffixes = _lineSuffixes.ToArray();
        var breaksBefore = _rootBreaks;

        _rootBreaks = 0;
        _commands.Push(root with { Doc = choice.Preferred });

        while (_commands.Count > depth)
        {
            Step(_commands.Pop());
        }

        var broke = _rootBreaks > 0;
        _rootBreaks = breaksBefore;

        if (!broke)
        {
            return;
        }

        _output.Length = lineStart;
        _output.Append(lineText);
        _lineStart = lineStart;
        _column = column;
        _lineSuffixes.Clear();
        _lineSuffixes.AddRange(suffixes);
        _commands.Push(root with { InFallback = true, Doc = choice.Fallback });
    }

    private void PrintLine(Command command, DocLine line)
    {
        if (command.Mode == PrintMode.Flat && line.Kind is LineKind.Soft or LineKind.Space)
        {
            if (line.Kind == LineKind.Space)
            {
                Write(" ");
            }

            return;
        }

        // A trailing comment belongs on the line that is about to end, not on the next one.
        if (_lineSuffixes.Count > 0)
        {
            _commands.Push(command);
            PushLineSuffixes();
            return;
        }

        if (command.AtRoot)
        {
            _rootBreaks++;
        }

        if (line.Kind == LineKind.Empty)
        {
            NewLine();
        }

        NewLine();
        WriteIndent(command.Indent);
    }

    private void PrintVerbatim(Command command, DocVerbatim verbatim)
    {
        // Raw content owns its columns outright, so the indent written for this line has to go --
        // otherwise every pass would indent it once more.
        if (verbatim.Mode == VerbatimMode.Raw)
        {
            TrimTrailingWhitespace();
        }

        for (var i = 0; i < verbatim.Lines.Length; i++)
        {
            if (i > 0)
            {
                NewLine(trim: verbatim.Mode != VerbatimMode.Anchored);

                // Raw keeps the original columns; the lines carry their own leading whitespace.
                if (verbatim.Mode == VerbatimMode.Preserve)
                {
                    WriteIndent(command.Indent);
                }
            }

            Write(verbatim.Lines[i]);
        }
    }

    /// <summary>
    /// Greedy filling: a separator breaks only once what follows it no longer fits.
    /// <paramref name="fill"/> alternates content and separator.
    /// </summary>
    /// <remarks>
    /// Unless the fill is strict, the content is measured in the <em>enclosing</em> mode rather than
    /// flat. A chunk that can wrap itself therefore counts as fitting, so the separator in front of
    /// it stays flat -- which is what keeps a last-resort break out of a line an ordinary one
    /// already solves. A list wants the opposite and asks for <see cref="DocFill.Strict"/>: every
    /// one of its elements could wrap itself, so leniency would reflow nothing at all.
    /// </remarks>
    private void PrintFill(Command command, DocFill fill)
    {
        var parts = fill.Parts;
        var content = command with { Doc = parts[0] };
        var probe = fill.Strict ? command with { Mode = PrintMode.Flat } : command;

        if (parts.Length == 1)
        {
            _commands.Push(content);
            return;
        }

        var separator = parts[1];

        if (parts.Length == 2)
        {
            _commands.Push(
                command with
                {
                    Mode = Fits(probe with { Doc = parts[0] }) ? PrintMode.Flat : PrintMode.Break,
                    Doc = separator,
                }
            );
            _commands.Push(content);
            return;
        }

        // A flat separator is at most one column wide, which is what Space stands in for here.
        var pairFits = Fits(probe with { Doc = Doc.Concat(parts[0], Doc.Space, parts[2]) });

        // Pushed first, so processed last.
        _commands.Push(command with { Doc = Doc.Fill(parts.RemoveRange(0, 2), fill.Strict) });

        _commands.Push(
            command with
            {
                Mode = pairFits ? PrintMode.Flat : PrintMode.Break,
                Doc = separator,
            }
        );

        _commands.Push(content);
    }

    /// <summary>
    /// Whether <paramref name="next"/> still fits on the current line, flat. The commands already
    /// queued are walked too -- a parameter list has to know that the <c>) As Double</c> behind it
    /// needs room as well -- until the first line break that would really be emitted.
    /// </summary>
    /// <param name="next">The command under test.</param>
    /// <param name="declaredBreaksOnly">
    /// Measure a nested group flat unless it breaks unconditionally, rather than letting it inherit
    /// the mode under test. A prepared layout is only worth taking if it fits on the breaks it
    /// declares: every other group is measured again while printing and may well stay flat, so
    /// ending the measurement at one of its breaks would accept a layout that then overruns. Applies
    /// to <paramref name="next"/> only, never to the commands already queued behind it.
    /// </param>
    /// <param name="wholeFit">Continue measuring after a break inside <paramref name="next"/>.</param>
    private bool Fits(Command next, bool declaredBreaksOnly = false, bool wholeFit = false)
    {
        if (_column > _options.PrintWidth)
        {
            return false;
        }

        var column = _column;
        var queue = _fitsQueue;
        queue.Clear();
        queue.Push(next);

        var rest = _commands.GetEnumerator();
        var inRest = false;

        while (true)
        {
            if (queue.Count == 0)
            {
                if (!rest.MoveNext())
                {
                    return true;
                }

                inRest = true;
                queue.Push(rest.Current);
                continue;
            }

            var command = queue.Pop();

            switch (command.Doc)
            {
                case DocNothing or DocExpandParent or DocLineSuffix:
                    break;

                case DocText text:
                    column = TextWidth.Measure(text.Value, column, _options.IndentSize);
                    break;

                case DocConcat concat:
                    for (var i = concat.Parts.Length - 1; i >= 0; i--)
                    {
                        queue.Push(command with { Doc = concat.Parts[i] });
                    }

                    break;

                case DocFill fill:
                    for (var i = fill.Parts.Length - 1; i >= 0; i--)
                    {
                        queue.Push(command with { Doc = fill.Parts[i] });
                    }

                    break;

                case DocIndent indent:
                    queue.Push(
                        command with
                        {
                            Indent = wholeFit && !inRest ? Deeper(command.Indent) : command.Indent,
                            Doc = indent.Content,
                        }
                    );
                    break;

                case DocDedentToRoot dedent:
                    queue.Push(command with { Doc = dedent.Content });
                    break;

                case DocRootChoice rootChoice:
                    queue.Push(command with { Doc = rootChoice.Preferred });
                    break;

                case DocWholeFit whole:
                    queue.Push(command with { Doc = whole.Content });
                    break;

                case DocAlign align:
                    queue.Push(command with { Doc = align.Content });
                    break;

                case DocGroup group:
                    queue.Push(
                        command with
                        {
                            Mode = ModeUnderTest(group, command, declaredBreaksOnly && !inRest),
                            Doc = group.Content,
                        }
                    );
                    break;

                // What it would come to under measurement: the layout it starts from.
                case DocConditionalGroup choice:
                    queue.Push(command with { Doc = choice.States[0] });
                    break;

                case DocConditional conditional:
                    queue.Push(
                        command with
                        {
                            Doc =
                                command.Mode == PrintMode.Break
                                    ? conditional.WhenBroken
                                    : conditional.WhenFlat,
                        }
                    );
                    break;

                case DocVerbatim verbatim:
                    column = TextWidth.Measure(verbatim.Lines[0], column, _options.IndentSize);

                    // A further line means the line under test ends here.
                    if (verbatim.Lines.Length > 1 && column <= _options.PrintWidth)
                    {
                        return true;
                    }

                    break;

                case DocLine line:
                    if (
                        wholeFit
                        && !inRest
                        && command.Mode == PrintMode.Break
                        && line.Kind is LineKind.Soft or LineKind.Space
                    )
                    {
                        column = command.Indent.Width;
                        break;
                    }

                    if (
                        command.Mode == PrintMode.Break
                        || line.Kind is LineKind.Hard or LineKind.Empty
                    )
                    {
                        return true;
                    }

                    if (line.Kind == LineKind.Space)
                    {
                        column++;
                    }

                    break;
            }

            if (column > _options.PrintWidth)
            {
                return false;
            }
        }
    }

    /// <summary>The mode a nested group is measured in while <see cref="Fits"/> walks it.</summary>
    private static PrintMode ModeUnderTest(
        DocGroup group,
        Command command,
        bool declaredBreaksOnly
    ) =>
        group.Expands ? PrintMode.Break
        : declaredBreaksOnly ? PrintMode.Flat
        : command.Mode;

    private void PushReversed(Command command, ImmutableArray<Doc> parts)
    {
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            _commands.Push(command with { Doc = parts[i] });
        }
    }

    private void PushLineSuffixes()
    {
        for (var i = _lineSuffixes.Count - 1; i >= 0; i--)
        {
            _commands.Push(_lineSuffixes[i]);
        }

        _lineSuffixes.Clear();
    }

    private void Write(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _output.Append(text);
        _column = TextWidth.Measure(text, _column, _options.IndentSize);
    }

    private void WriteIndent(Indentation indent)
    {
        _output.Append(indent.Text);
        _column = indent.Width;
    }

    private void NewLine(bool trim = true)
    {
        if (trim)
        {
            TrimTrailingWhitespace();
        }

        _output.Append(_options.NewLine);
        _lineStart = _output.Length;
        _column = 0;
    }

    /// <summary>An indent written for a line that then stayed empty must not survive as trailing space.</summary>
    private void TrimTrailingWhitespace()
    {
        var end = _output.Length;
        var tabs = false;

        while (end > _lineStart && _output[end - 1] is ' ' or '\t')
        {
            tabs |= _output[end - 1] == '\t';
            end--;
        }

        if (end == _output.Length)
        {
            return;
        }

        // Removing only spaces walks the column straight back; a tab in the run needs a remeasure.
        _column = tabs
            ? TextWidth.Measure(
                _output.ToString(_lineStart, end - _lineStart),
                0,
                _options.IndentSize
            )
            : _column - (_output.Length - end);

        _output.Length = end;
    }

    private readonly record struct Command(
        Indentation Indent,
        PrintMode Mode,
        Doc Doc,
        Indentation? Root = null,
        bool AtRoot = false,
        bool InFallback = false
    );
}
