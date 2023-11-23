using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Simple.Wpf.Terminal;

/// <summary>
///     A WPF user control which mimics a terminal\console window, you are responsible for the service
///     providing the data for display and processing the entered line when the LineEntered event is raised.
///     The data is bound via the ItemsSource dependency property.
/// </summary>
public sealed class Terminal : RichTextBox, ITerminalEx
{
    /// <summary>
    ///     The items to be displayed in the terminal window, e.g. an ObservableCollection.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(Terminal),
        new PropertyMetadata(default(IEnumerable), OnItemsSourceChanged));

    /// <summary>
    ///     Auto-completion-strings to be traversed in terminal window when tab is pressed, e.g. an ObservableCollection.
    /// </summary>
    public static readonly DependencyProperty AutoCompletionsSourceProperty = DependencyProperty.Register(
        nameof(AutoCompletionsSource),
        typeof(IEnumerable<string>),
        typeof(Terminal),
        new PropertyMetadata(default(IEnumerable<string>)));

    /// <summary>
    ///     The margin around the contents of the terminal window, optional field with a default value of 0.
    /// </summary>
    public static readonly DependencyProperty ItemsMarginProperty = DependencyProperty.Register(nameof(ItemsMargin),
        typeof(Thickness),
        typeof(Terminal),
        new PropertyMetadata(new Thickness(), OnItemsMarginChanged));

    /// <summary>
    ///     The terminal prompt to be displayed.
    /// </summary>
    public static readonly DependencyProperty PromptProperty = DependencyProperty.Register(nameof(Prompt),
        typeof(string),
        typeof(Terminal),
        new PropertyMetadata(default(string), OnPromptChanged));

    /// <summary>
    ///     The current the editable line in the terminal, there is only one editable line in the terminal and this is at the
    ///     bottom of the content.
    /// </summary>
    public static readonly DependencyProperty LineProperty = DependencyProperty.Register(nameof(Line),
        typeof(string),
        typeof(Terminal),
        new PropertyMetadata(default(string)));

    /// <summary>
    ///     The property name of the 'value' to be displayed, optional field which if null then ToString() is called on the
    ///     bound instance.
    /// </summary>
    public static readonly DependencyProperty ItemDisplayPathProperty = DependencyProperty.Register(
        nameof(ItemDisplayPath),
        typeof(string),
        typeof(Terminal),
        new PropertyMetadata(default(string), OnDisplayPathChanged));

    /// <summary>
    ///     The color converter for lines.
    /// </summary>
    public static readonly DependencyProperty LineColorConverterProperty = DependencyProperty.Register(
        nameof(LineColorConverter),
        typeof(IValueConverter),
        typeof(Terminal),
        new PropertyMetadata(null, OnLineConverterChanged));

    /// <summary>
    ///     The height of each line in the terminal window, optional field with a default value of 10.
    /// </summary>
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight),
        typeof(int),
        typeof(Terminal),
        new PropertyMetadata(10, OnItemHeightChanged));

    /// <summary>
    ///     Automatic scroll to end of vertical scrollbar when content is added.
    /// </summary>
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.Register(nameof(AutoScroll),
            typeof(bool),
            typeof(Terminal),
            new PropertyMetadata(true));

    private readonly List<string> _buffer;
    private readonly Paragraph _paragraph;

    private int _autoCompletionIndex;
    private List<string> _currentAutoCompletionList = new();
    private PropertyInfo _displayPathProperty;
    private INotifyCollectionChanged _notifyChanged;
    private Run _promptInline;
    private ScrollBar _verticalScrollBar;

    /// <summary>
    ///     Default constructor.
    /// </summary>
    public Terminal()
    {
        _buffer = new List<string>();

        _paragraph = new Paragraph
        {
            Margin = ItemsMargin,
            LineHeight = ItemHeight
        };

        IsUndoEnabled = false;

        if (!string.IsNullOrWhiteSpace(Prompt))
            _promptInline = new Run(Prompt);

        Document = new FlowDocument(_paragraph);

        AddPrompt();
        CaretPosition = Document.ContentEnd;

        TextChanged += (_, _) =>
        {
            Line = AggregateAfterPrompt();
            if (AutoScroll)
                ScrollToEnd();
        };

        SizeChanged += (_, _) =>
        {
            if (_verticalScrollBar != null)
                Document.PageWidth = ActualWidth - _verticalScrollBar.ActualWidth;
        };

        Loaded += (_, _) =>
        {
            _verticalScrollBar = this.GetVisualDescendents<ScrollBar>()
                .FirstOrDefault(scrollBar => scrollBar.Name == "PART_VerticalScrollBar");
            if (_verticalScrollBar != null)
                Document.PageWidth = ActualWidth - _verticalScrollBar.ActualWidth;
        };

        DataObject.AddPastingHandler(this, PasteCommand);
        DataObject.AddCopyingHandler(this, CopyCommand);
    }

    /// <summary>
    ///     Automatic scroll to end of vertical scrollbar
    /// </summary>
    public bool AutoScroll
    {
        get => (bool)GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    /// <summary>
    ///     Event fired when the user presses the Enter key.
    /// </summary>
    public event EventHandler LineEntered;

    /// <summary>
    ///     The bound items to the terminal.
    /// </summary>
    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    ///     The bound auto-completion-strings to the terminal.
    /// </summary>
    public IEnumerable<string> AutoCompletionsSource
    {
        get => (IEnumerable<string>)GetValue(AutoCompletionsSourceProperty);
        set => SetValue(AutoCompletionsSourceProperty, value);
    }

    /// <summary>
    ///     The prompt of the terminal.
    /// </summary>
    public string Prompt
    {
        get => (string)GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    /// <summary>
    ///     The current editable line of the terminal (bottom line).
    /// </summary>
    public string Line
    {
        get => (string)GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    /// <summary>
    ///     The display path for the bound items.
    /// </summary>
    public string ItemDisplayPath
    {
        get => (string)GetValue(ItemDisplayPathProperty);
        set => SetValue(ItemDisplayPathProperty, value);
    }

    /// <summary>
    ///     The error color for the bound items.
    /// </summary>
    public IValueConverter LineColorConverter
    {
        get => (IValueConverter)GetValue(LineColorConverterProperty);
        set => SetValue(LineColorConverterProperty, value);
    }

    /// <summary>
    ///     The individual line height for the bound items.
    /// </summary>
    public int ItemHeight
    {
        get => (int)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>
    ///     The margin around the bound items.
    /// </summary>
    public Thickness ItemsMargin
    {
        get => (Thickness)GetValue(ItemsMarginProperty);
        set => SetValue(ItemsMarginProperty, value);
    }

    /// <summary>
    ///     Raises the Initialized event. This method is invoked whenever IsInitialized is set to true internally.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (Style == null)
            if (Application.Current.TryFindResource("DefaultTerminalStyle") is Style defaultStyle)
                Style = defaultStyle;
    }

    private bool HandleReadOnlyKeyUp()
    {
        if (Template.FindName("PART_ContentHost", this) is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - FontSize);
            return true;
        }

        return false;
    }

    private bool HandleReadOnlyKeyDown()
    {
        if (Template.FindName("PART_ContentHost", this) is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + FontSize);
            return true;
        }

        return false;
    }

    private bool HandleReadOnlyKeyPageUp()
    {
        if (Template.FindName("PART_ContentHost", this) is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollViewer.ViewportHeight);
            return true;
        }

        return false;
    }

    private bool HandleReadOnlyKeyPageDown()
    {
        if (Template.FindName("PART_ContentHost", this) is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollViewer.ViewportHeight);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Processes every key pressed when the control has focus.
    /// </summary>
    /// <param name="args">The key pressed arguments.</param>
    protected override void OnPreviewKeyDown(KeyEventArgs args)
    {
        base.OnPreviewKeyDown(args);

        if (IsReadOnly)
        {
            switch (args.Key)
            {
                case Key.Up:
                    args.Handled = HandleReadOnlyKeyUp();
                    break;
                case Key.Down:
                    args.Handled = HandleReadOnlyKeyDown();
                    break;
                case Key.PageUp:
                    args.Handled = HandleReadOnlyKeyPageUp();
                    break;
                case Key.PageDown:
                    args.Handled = HandleReadOnlyKeyPageDown();
                    break;
            }

            return;
        }

        if (args.Key != Key.Tab) _currentAutoCompletionList.Clear();

        switch (args.Key)
        {
            case Key.A:
                args.Handled = HandleSelectAllKeys();
                break;
            case Key.X:
            case Key.C:
            case Key.V:
                args.Handled = HandleCopyKeys(args);
                break;
            case Key.Left:
                args.Handled = HandleLeftKey();
                break;
            case Key.Right:
                break;
            case Key.PageDown:
            case Key.PageUp:
                args.Handled = true;
                break;
            case Key.Escape:
                ClearAfterPrompt();
                args.Handled = true;
                break;
            case Key.Up:
            case Key.Down:
                args.Handled = HandleUpDownKeys(args);
                break;
            case Key.Delete:
                args.Handled = HandleDeleteKey();
                break;
            case Key.Back:
                args.Handled = HandleBackspaceKey();
                break;
            case Key.Enter:
                HandleEnterKey();
                args.Handled = true;
                break;
            case Key.Tab:
                HandleTabKey();
                args.Handled = true;
                break;
            default:
                args.Handled = HandleAnyOtherKey();
                break;
        }
    }

    /// <summary>
    ///     Processes style changes for the terminal.
    /// </summary>
    /// <param name="oldStyle">The current style applied to the terminal.</param>
    /// <param name="newStyle">The new style to be applied to the terminal.</param>
    protected override void OnStyleChanged(Style oldStyle, Style newStyle)
    {
        base.OnStyleChanged(oldStyle, newStyle);

        if (ItemsSource != null)
            using (DeclareChangeBlock())
            {
                ReplaceItems(ItemsSource.Cast<object>()
                    .ToArray());
            }
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        terminal.HandleItemsSourceChanged((IEnumerable)args.NewValue);
    }

    private static void OnPromptChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        terminal.HandlePromptChanged((string)args.NewValue);
    }

    private static void OnItemsMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        if (terminal._paragraph != null)
            terminal._paragraph.Margin = (Thickness)args.NewValue;
    }

    private static void OnItemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        if (terminal._paragraph != null)
            terminal._paragraph.LineHeight = (int)args.NewValue;
    }

    private static void OnDisplayPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        terminal._displayPathProperty = null;
    }

    private static void OnLineConverterChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue == args.OldValue) return;

        var terminal = (Terminal)d;
        terminal.HandleLineConverterChanged();
    }

    private void CopyCommand(object sender, DataObjectCopyingEventArgs args)
    {
        if (!string.IsNullOrEmpty(Selection.Text)) args.DataObject.SetData(typeof(string), Selection.Text);

        args.Handled = true;
    }

    private void PasteCommand(object sender, DataObjectPastingEventArgs args)
    {
        var text = (string)args.DataObject.GetData(typeof(string));

        if (!string.IsNullOrEmpty(text))
        {
            if (Selection.Start != Selection.End)
            {
                Selection.Start.DeleteTextInRun(Selection.Text.Length);
                Selection.Start.InsertTextInRun(text);

                var selectionEnd = Selection.Start.GetPositionAtOffset(text.Length);
                CaretPosition = selectionEnd;
            }
            else
            {
                AddLine(text);
            }
        }

        args.CancelCommand();
        args.Handled = true;
    }

    private void HandleItemsSourceChanged(IEnumerable items)
    {
        if (items == null)
        {
            _paragraph.Inlines.Clear();
            AddPrompt();
            CaretPosition = CaretPosition.DocumentEnd;

            return;
        }

        using (DeclareChangeBlock())
        {
            if (items is INotifyCollectionChanged changed)
            {
                var notifyChanged = changed;
                if (_notifyChanged != null) _notifyChanged.CollectionChanged -= HandleItemsChanged;

                _notifyChanged = notifyChanged;
                _notifyChanged.CollectionChanged += HandleItemsChanged;

                // ReSharper disable once PossibleMultipleEnumeration
                var existingItems = items.Cast<object>()
                    .ToArray();
                if (existingItems.Any())
                    ReplaceItems(existingItems);
                else
                    ClearItems();
            }
            else
            {
                // ReSharper disable once PossibleMultipleEnumeration
                ReplaceItems(ItemsSource.Cast<object>()
                    .ToArray());
            }
        }
    }

    private void HandlePromptChanged(string prompt) =>
        _promptInline = string.IsNullOrWhiteSpace(Prompt)
            ? null
            : new Run(Prompt)
            {
                Text = prompt
            };

    private void HandleLineConverterChanged()
    {
        using (DeclareChangeBlock())
        {
            foreach (var run in _paragraph.Inlines
                         .Where(x => x is Run)
                         .Cast<Run>())
                run.Foreground = GetForegroundColor(run.Text);
        }
    }

    private void HandleItemsChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        using (DeclareChangeBlock())
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddItems(args.NewItems?.Cast<object>()
                        .ToArray() ?? Array.Empty<object>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(args.OldItems?.Cast<object>()
                        .ToArray() ?? Array.Empty<object>());
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReplaceItems(((IEnumerable)sender).Cast<object>()
                        .ToArray());
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveItems(args.OldItems?.Cast<object>()
                        .ToArray() ?? Array.Empty<object>());
                    AddItems(args.NewItems?.Cast<object>()
                        .ToArray() ?? Array.Empty<object>());
                    break;
            }
        }
    }

    private void ClearItems()
    {
        _paragraph.Inlines.Clear();

        AddPrompt();
    }

    private void ReplaceItems(object[] items)
    {
        _paragraph.Inlines.Clear();

        AddItems(items);
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private void AddItems(object[] items)
    {
        var command = AggregateAfterPrompt();
        ClearAfterPrompt();

        if (_promptInline != null)
            _paragraph.Inlines.Remove(_promptInline);

        var inlines = items.SelectMany(x =>
            {
                var value = ExtractValue(x);

                var newInlines = new List<Inline>();
                using var reader = new StringReader(value);

                var line = reader.ReadLine();
                newInlines.Add(new Run(line) { Foreground = GetForegroundColor(x) });
                newInlines.Add(new LineBreak());

                return newInlines;
            })
            .ToArray();

        _paragraph.Inlines.AddRange(inlines);
        AddPrompt();
        _paragraph.Inlines.Add(new Run(command));
    }

    private Brush GetForegroundColor(object item)
    {
        if (LineColorConverter != null)
            return (Brush)LineColorConverter.Convert(item, typeof(Brush), null, CultureInfo.InvariantCulture);

        return Foreground;
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private void RemoveItems(object[] items)
    {
        foreach (var item in items)
        {
            var value = ExtractValue(item);

            var run = _paragraph.Inlines
                .Where(x => x is Run)
                .Cast<Run>()
                .FirstOrDefault(x => x.Text == value);

            if (run != null) _paragraph.Inlines.Remove(run);
        }
    }

    private static TextPointer GetTextPointer(TextPointer textPointer, LogicalDirection direction)
    {
        var currentTextPointer = textPointer;
        while (currentTextPointer != null)
        {
            var nextPointer = currentTextPointer.GetNextContextPosition(direction);
            if (nextPointer == null) return null;

            if (nextPointer.GetPointerContext(direction) == TextPointerContext.Text) return nextPointer;

            currentTextPointer = nextPointer;
        }

        return null;
    }

    private string ExtractValue(object item)
    {
        var displayPath = ItemDisplayPath;
        if (displayPath == null) return item == null ? string.Empty : item.ToString();

        if (_displayPathProperty == null)
            _displayPathProperty = item.GetType()
                .GetProperty(displayPath);

        var value = _displayPathProperty?.GetValue(item, null);
        return value == null ? string.Empty : value.ToString();
    }

    private bool HandleCopyKeys(KeyEventArgs args)
    {
        if (args.Key == Key.C)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) return false;

            var pos = 0;
            if (_promptInline != null)
            {
                var promptEnd = _promptInline.ContentEnd;
                pos = CaretPosition.CompareTo(promptEnd);
            }

            var selectionPos = Selection.Start.CompareTo(CaretPosition);

            return pos < 0 || selectionPos < 0;
        }

        if (args.Key == Key.X || args.Key == Key.V)
        {
            var pos = 0;
            if (_promptInline != null)
            {
                var promptEnd = _promptInline.ContentEnd;
                pos = CaretPosition.CompareTo(promptEnd);
            }

            var selectionPos = Selection.Start.CompareTo(CaretPosition);

            return pos < 0 || selectionPos < 0;
        }

        return false;
    }

    private bool HandleSelectAllKeys()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            Selection.Select(Document.ContentStart, Document.ContentEnd);

            return true;
        }

        return HandleAnyOtherKey();
    }

    private void HandleTabKey()
    {
        if (!_currentAutoCompletionList.Any())
            _currentAutoCompletionList =
                AutoCompletionsSource != null ? AutoCompletionsSource.ToList() : new List<string>();

        if (_currentAutoCompletionList.Any())
        {
            if (_autoCompletionIndex >= _currentAutoCompletionList.Count) _autoCompletionIndex = 0;
            ClearAfterPrompt();
            AddLine(_currentAutoCompletionList[_autoCompletionIndex]);
            _autoCompletionIndex++;
        }
    }

    private bool HandleUpDownKeys(KeyEventArgs args)
    {
        var pos = 0;
        if (_promptInline != null)
            pos = CaretPosition.CompareTo(_promptInline.ContentEnd);

        if (pos < 0) return false;

        if (!_buffer.Any()) return true;

        ClearAfterPrompt();

        string existingLine;
        if (args.Key == Key.Down)
        {
            existingLine = _buffer[^1];
            _buffer.RemoveAt(_buffer.Count - 1);
            _buffer.Insert(0, existingLine);
        }
        else
        {
            existingLine = _buffer[0];
            _buffer.RemoveAt(0);
            _buffer.Add(existingLine);
        }

        AddLine(existingLine);

        return true;
    }

    private void HandleEnterKey()
    {
        var line = AggregateAfterPrompt();

        ClearAfterPrompt();

        Line = line;
        _buffer.Insert(0, line);

        CaretPosition = Document.ContentEnd;

        OnLineEntered();
    }

    private bool HandleAnyOtherKey()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) return false;

        if (string.IsNullOrWhiteSpace(Prompt)) return false;
        var promptEnd = _promptInline.ContentEnd;

        var pos = CaretPosition.CompareTo(promptEnd);
        return pos < 0;
    }

    private bool HandleBackspaceKey()
    {
        if (_promptInline == null) return false;
        var promptEnd = _promptInline.ContentEnd;

        var textPointer = GetTextPointer(promptEnd, LogicalDirection.Forward);
        if (textPointer == null)
        {
            var pos = CaretPosition.CompareTo(promptEnd);

            if (pos <= 0) return true;
        }
        else
        {
            var pos = CaretPosition.CompareTo(textPointer);
            if (pos <= 0) return true;
        }

        return false;
    }

    private bool HandleLeftKey()
    {
        if (_promptInline == null) return false;
        var promptEnd = _promptInline.ContentEnd;

        var textPointer = GetTextPointer(promptEnd, LogicalDirection.Forward);
        if (textPointer == null)
        {
            var pos = CaretPosition.CompareTo(promptEnd);

            if (pos == 0) return true;
        }
        else
        {
            var pos = CaretPosition.CompareTo(textPointer);
            if (pos == 0) return true;
        }

        return false;
    }

    private bool HandleDeleteKey()
    {
        if (_promptInline == null) return false;
        var pos = CaretPosition.CompareTo(_promptInline.ContentEnd);

        return pos < 0;
    }

    private void OnLineEntered()
    {
        var handler = LineEntered;

        handler?.Invoke(this, EventArgs.Empty);
    }

    private void AddLine(string line)
    {
        CaretPosition = CaretPosition.DocumentEnd;

        var inline = new Run(line);
        _paragraph.Inlines.Add(inline);

        CaretPosition = Document.ContentEnd;
    }

    private string AggregateAfterPrompt()
    {
        if (_promptInline == null) return string.Empty;
        var inlineList = _paragraph.Inlines.ToList();
        var promptIndex = inlineList.IndexOf(_promptInline);

        if (promptIndex != -1)
            return inlineList.Where((_, i) => i > promptIndex)
                .Where(x => x is Run)
                .Cast<Run>()
                .Select(x => x.Text)
                .Aggregate(string.Empty, (current, part) => current + part);
        return inlineList.OfType<Run>()
            .LastOrDefault()
            ?.Text;
    }

    private void ClearAfterPrompt()
    {
        if (_promptInline == null) return;

        var inlineList = _paragraph.Inlines.ToList();
        var promptIndex = inlineList.IndexOf(_promptInline);

        if (promptIndex != -1)
            foreach (var inline in inlineList.Where((_, i) => i > promptIndex))
                _paragraph.Inlines.Remove(inline);
    }

    private void AddPrompt()
    {
        if (_promptInline != null) _paragraph.Inlines.Add(_promptInline);

        _paragraph.Inlines.Add(new Run());
    }
}