using Avalonia;
using AvRichTextBox;

namespace NinferLauncher_v_2.Controls;

/// <summary>
/// Thin wrapper around the Simplecto <see cref="RichTextBox"/> (read-only chat bubble)
/// that exposes the document through a <em>public</em> property of our own.
///
/// The library's backing field <c>RichTextBox.FlowDocumentProperty</c> is declared
/// <c>internal</c>. XamlIl emits a direct static-field reference when compiling
/// <c>FlowDocument="{Binding ...}"</c>, and our assembly cannot access that internal
/// field at runtime – the message DataTemplate throws
/// <see cref="System.FieldAccessException"/> the moment the first message is rendered.
/// Binding <see cref="Doc"/> instead keeps the compiled reference inside this assembly
/// (it forwards to the library's public <c>FlowDocument</c> property).
/// </summary>
public class ChatRichTextBox : RichTextBox
{
    public ChatRichTextBox()
    {
        // The library defaults IsCaretVisible to true and only flips it in
        // GotFocus/LostFocus handlers. Chat bubbles are read-only and
        // IsTabStop=False, so they never receive logical focus – the handlers
        // never fire and a blinking caret was left visible at the start of
        // every message. Hide it until the bubble is actually focused.
        IsCaretVisible = false;
    }

    public static readonly DirectProperty<ChatRichTextBox, FlowDocument> DocProperty =
        AvaloniaProperty.RegisterDirect<ChatRichTextBox, FlowDocument>(
            nameof(Doc),
            o => o.FlowDocument,
            (o, v) => o.FlowDocument = v);

    /// <summary>Public mirror of the library's <c>FlowDocument</c>, for XAML binding.</summary>
    public FlowDocument Doc
    {
        get => GetValue(DocProperty);
        set => SetValue(DocProperty, value);
    }
}