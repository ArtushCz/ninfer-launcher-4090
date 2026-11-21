using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Media;
using AvRichTextBox;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Block = Markdig.Syntax.Block;
using Paragraph = AvRichTextBox.Paragraph;
using NinferLauncher_v_2.Models;

namespace NinferLauncher_v_2.Services;

/// <summary>Turns a chat message into the <see cref="AvRichTextBox.FlowDocument"/> the
/// read-only <c>RichTextBox</c> (Simplecto.Avalonia.RichTextBox) in ChatView renders.
/// The control is selectable: drag to highlight, Ctrl+C copies the selection to the
/// clipboard as plain text.
/// User / pending messages are plain text; AI replies are parsed as Markdown
/// (headings, bold/italic, inline code, fenced code blocks, lists, tables, links),
/// because unformatted model output is unreadable. If the parse fails the raw
/// text is shown instead.</summary>
public static class ChatMarkdown
{
    static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas, Courier New");

    // The library's model defaults (Block.FontFamily "Meiryo", FontSize 16) and the
    // EditableRun constructor default (FontSize 16) silently override the bubble's
    // FontFamily/FontSize from ChatView.axaml, so every run and paragraph we create
    // must state its font explicitly – otherwise code blocks render at the same size
    // as body text and the bubble font is ignored.
    static readonly FontFamily BodyFont = new("Inter");
    const double BodySize = 15;      // keep in sync with FontSize on the bubble in ChatView.axaml
    const double CodeSize = 12;
    const double CaptionSize = 10.5;
    const double InlineCodeSize = 13.5;

    static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#C5C8C6"));
    static readonly IBrush HeadingBrush = new SolidColorBrush(Color.Parse("#E8EAED"));
    static readonly IBrush InlineCodeBrush = new SolidColorBrush(Color.Parse("#D8A0A0"));
    static readonly IBrush CodeTextBrush = new SolidColorBrush(Color.Parse("#C9CDCF"));
    static readonly IBrush CodeDimBrush = new SolidColorBrush(Color.Parse("#6E7279"));
    static readonly IBrush LinkBrush = new SolidColorBrush(Color.Parse("#6FA8DC"));
    static readonly IBrush CodeBg = new SolidColorBrush(Color.Parse("#191B1E"));
    static readonly IBrush CodeBorder = new SolidColorBrush(Color.Parse("#2A2D33"));
    static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#A54242"));

    static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .Build();

    public static FlowDocument Build(ChatMessage m)
    {
        var doc = new FlowDocument();
        if (doc.Blocks is null)
            doc.Blocks = new();
        if (m.IsUser || m.IsPending)
        {
            AddPlainText(doc, m.Text, m.IsError);
            return doc;
        }

        var tree = Markdown.Parse(m.Text, Pipeline, null);

        foreach (var b in tree)
            AppendBlock(doc, b, 0);

        if (doc.Blocks.Count == 0)
            AddPlainText(doc, m.Text, false);
        return doc;
    }

    /// <summary>Plain (non-Markdown) text, newlines preserved.</summary>
    static void AddPlainText(FlowDocument doc, string text, bool isError)
    {
        var brush = isError ? ErrorBrush : Normal;
        var p = new Paragraph { FontFamily = BodyFont, FontSize = BodySize };
        p.AddInline(new EditableRun(text) { Foreground = brush, FontFamily = BodyFont, FontSize = BodySize });
        doc.Blocks.Add(p);
    }

    static void AppendBlock(FlowDocument doc, Block block, int depth)
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                double size = h.Level switch
                {
                    1 => 20,
                    2 => 18,
                    3 => 16.5,
                    _ => 16,
                };
                var p = new Paragraph
                {
                    FontFamily = BodyFont,
                    FontSize = size,
                    FontWeight = FontWeight.SemiBold,
                };
                if (h.Inline != null)
                    AppendInlines(p, h.Inline, true, size);
                EnsureNotEmpty(p, HeadingBrush, size);
                doc.Blocks.Add(p);
                break;
            }

            case CodeBlock code:
            {
                // Fenced blocks start with a small dim "language" caption in its own
                // paragraph, kept outside the code box so selecting the box copies the
                // code verbatim (no tag, no offset).
                if (code is FencedCodeBlock f && !string.IsNullOrWhiteSpace(f.Info))
                {
                    var cap = new Paragraph
                    {
                        FontFamily = CodeFont,
                        FontSize = CaptionSize,
                        LeftIndent = 13,
                        Margin = new Thickness(6, 6, 6, 6),
                    };
                    cap.AddInline(new EditableRun(f.Info) { Foreground = CodeDimBrush, FontFamily = CodeFont, FontSize = CaptionSize });
                    doc.Blocks.Add(cap);
                }
                // Box padding: LeftIndent pushes the code inside the box's left edge,
                // Padding pushes it off the top/bottom border (a fake leading-space
                // run would become part of the selection and the copied text,
                // offsetting both). Margin = breathing room outside the box,
                // LineHeight = a little air between code lines.
                var p = new Paragraph
                {
                    Background = CodeBg,
                    BorderBrush = CodeBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 4, 0, 8),
                    Padding = new Thickness(0, 5, 0, 5),
                    LeftIndent = 12,
                    FontFamily = CodeFont,
                    FontSize = CodeSize,
                    LineHeight = 17,
                };
                p.AddInline(new EditableRun(JoinCodeLines(code)) { Foreground = CodeTextBrush, FontFamily = CodeFont, FontSize = CodeSize });
                doc.Blocks.Add(p);
                break;
            }

            case ListBlock list:
            {
                int n = 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    string marker = list.IsOrdered ? $"{n++}." : "•";
                    var p = new Paragraph
                    {
                        ListMarker = marker,
                        LeftIndent = (depth + 1) * 22,
                        FontFamily = BodyFont,
                        FontSize = BodySize,
                    };
                    bool first = true;
                    foreach (var b in item)
                    {
                        if (b is LeafBlock leaf && leaf.Inline != null)
                        {
                            if (!first)
                                p.AddInline(new EditableLineBreak());
                            AppendInlines(p, leaf.Inline, false, BodySize);
                            first = false;
                        }
                        else
                        {
                            // nested lists / code blocks inside the item
                            AppendBlock(doc, b, depth + 1);
                        }
                    }
                    EnsureNotEmpty(p, Normal);
                    doc.Blocks.Add(p);
                }
                break;
            }
case QuoteBlock quote:
            {
                foreach (var b in quote)
                {
                    if (b is LeafBlock lb && lb.Inline != null)
                    {
                        var p = new Paragraph { LeftIndent = (depth + 1) * 24, ListMarker = "❝", FontFamily = BodyFont, FontSize = BodySize };
                        AppendInlines(p, lb.Inline, false, BodySize);
                        EnsureNotEmpty(p, Normal);
                        doc.Blocks.Add(p);
                    }
                    else
                    {
                        AppendBlock(doc, b, depth);
                    }
                }
                break;
            }

            case ThematicBreakBlock:
            {
                var p = new Paragraph { FontFamily = BodyFont, FontSize = BodySize };
                p.AddInline(new EditableRun("───────────") { Foreground = CodeDimBrush, FontFamily = BodyFont, FontSize = BodySize });
                doc.Blocks.Add(p);
                break;
            }

            case Markdig.Extensions.Tables.Table table:
            {
                var p = new Paragraph
                {
                    Background = CodeBg,
                    BorderBrush = CodeBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 4, 0, 8),
                    Padding = new Thickness(0, 5, 0, 5),
                    LeftIndent = 12,
                    FontFamily = CodeFont,
                    FontSize = CodeSize,
                    LineHeight = 17,
                };
                bool first = true;
                foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                {
                    if (!first)
                        p.AddInline(new EditableLineBreak());
                    first = false;
                    string line = string.Join("  |  ",
                        row.OfType<Markdig.Extensions.Tables.TableCell>().Select(c => FlattenBlockText(c)));
                    p.AddInline(new EditableRun(line) { Foreground = CodeTextBrush, FontFamily = CodeFont, FontSize = CodeSize });
                }
                doc.Blocks.Add(p);
                break;
            }

            case HtmlBlock:
                break; // raw html – nothing useful to render

            case LeafBlock leaf:
            {
                if (leaf.Inline == null)
                    break;
                var p = new Paragraph { FontFamily = BodyFont, FontSize = BodySize };
                AppendInlines(p, leaf.Inline, false, BodySize);
                EnsureNotEmpty(p, Normal);
                doc.Blocks.Add(p);
                break;
            }

            default:
            {
                var text = FlattenBlockText(block);
                if (text.Length > 0)
                    AddPlainText(doc, text, false);
                break;
            }
        }
    }

    /// <summary>A paragraph that ended up empty still has to contain one inline.</summary>
    static void EnsureNotEmpty(Paragraph p, IBrush brush, double size = BodySize)
    {
        if (!p.GetInlines.Any())
            p.AddInline(new EditableRun(" ") { Foreground = brush, FontFamily = BodyFont, FontSize = size });
    }

    static void AppendInlines(Paragraph p, ContainerInline root, bool isHeading, double size = BodySize)
    {
        for (Inline inl = root; inl != null; inl = inl.NextSibling)
            AppendInline(p, inl, isHeading, FontWeight.Normal, FontStyle.Normal, size);
    }
    static void AppendInline(Paragraph p, Inline inl, bool isHeading, FontWeight weight, FontStyle style, double size)
    {
        switch (inl)
        {
            case CodeInline c:
                p.AddInline(new EditableRun(c.Content)
                {
                    FontFamily = CodeFont,
                    Foreground = InlineCodeBrush,
                    FontSize = InlineCodeSize,
                    FontWeight = weight,
                });
                break;

            case LinkInline link:
            {
                if (link.IsImage)
                    break; // images have no source to render
                var label = FlattenInlineText(link);
                if (label.Length == 0)
                    label = link.Url ?? "";
                // the control forces hyperlink styling (blue + underline); clicking opens the URL
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var _))
                {
                    var linkRun = new EditableHyperlink(label, link.Url);
                    linkRun.FontFamily = BodyFont;
                    linkRun.FontSize = size;
                    p.AddInline(linkRun);
                }
                else
                {
                    p.AddInline(new EditableRun(label) { Foreground = LinkBrush, FontFamily = BodyFont, FontSize = size });
                }
                break;
            }

            case EmphasisInline e:
            {
                // **bold** / __bold__ (DelimiterCount >= 2) vs *italic* / _italic_
                var w = e.DelimiterCount >= 2 ? FontWeight.Bold : weight;
                var s = e.DelimiterCount >= 2 ? style : FontStyle.Italic;
                for (var child = e.FirstChild; child != null; child = child.NextSibling)
                    AppendInline(p, child, isHeading, w, s, size);
                break;
            }

            case LineBreakInline:
                p.AddInline(new EditableLineBreak());
                break;

            case LiteralInline lit:
                p.AddInline(new EditableRun(lit.ToString())
                {
                    Foreground = isHeading ? HeadingBrush : Normal,
                    FontWeight = weight,
                    FontStyle = style,
                    FontFamily = BodyFont,
                    FontSize = size,
                });
                break;

            case ContainerInline span:
                // generic span – render children without extra styling
                for (var child = span.FirstChild; child != null; child = child.NextSibling)
                    AppendInline(p, child, isHeading, weight, style, size);
                break;

            default:
            {
                // any other leaf inline (html entities, autolinks, ...) – take its text
                if (inl is not ContainerInline)
                {
                    var text = inl.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        p.AddInline(new EditableRun(text)
                        {
                            Foreground = isHeading ? HeadingBrush : Normal,
                            FontWeight = weight,
                            FontStyle = style,
                            FontFamily = BodyFont,
                            FontSize = size,
                        });
                    }
                }
                break;
            }
        }
    }
/// <summary>Plain-text view of an inline subtree (links, emphasis, literals, breaks).</summary>
    static string FlattenInlineText(ContainerInline inl)
    {
        var sb = new StringBuilder();
        for (var c = inl.FirstChild; c != null; c = c.NextSibling)
        {
            switch (c)
            {
                case LiteralInline lit: sb.Append(lit.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case LineBreakInline lb: sb.Append(lb.IsHard ? '\n' : ' '); break;
                case ContainerInline ci: sb.Append(FlattenInlineText(ci)); break;
                default:
                    if (c is not ContainerInline)
                    {
                        var t = c.ToString();
                        if (!string.IsNullOrWhiteSpace(t))
                            sb.Append(t);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Plain-text view of a block (its inline content) for table cells / unknown blocks.</summary>
    static string FlattenBlockText(Block block)
    {
        if (block is LeafBlock leaf && leaf.Inline != null)
            return FlattenInlineText(leaf.Inline);
        var sb = new StringBuilder();
        if (block is ContainerBlock container)
        {
            foreach (var b in container)
                sb.Append(FlattenBlockText(b));
        }
        return sb.ToString();
    }

    static string JoinCodeLines(CodeBlock code)
    {
        var sb = new StringBuilder();
        bool first = true;
        if (code is FencedCodeBlock f)
        {
            foreach (var l in f.Lines)
            {
                if (!first)
                    sb.Append('\n');
                sb.Append(l.ToString().TrimEnd());
                first = false;
            }
        }
        else
        {
            foreach (var l in code.CodeBlockLines)
            {
                if (!first)
                    sb.Append('\n');
                sb.Append(l.ToString().TrimEnd());
                first = false;
            }
        }
        return sb.ToString();
    }
}