namespace Simple.Wpf.Terminal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// A WPF user control which mimics a terminal\console window, you are responsible for the service
    /// providing the data for display and processing the entered line when the LineEntered event is raised.
    /// The data is bound via the ItemsSource dependancy property.
    /// </summary>
    public sealed class Terminal : RichTextBox, ITerminal
    {
        /// <summary>
        /// Event fired when the user presses the Enter key.
        /// </summary>
        public event EventHandler LineEntered;

        /// <summary>
        /// The items to be displayed in the terminal window, e.g. an ObsrevableCollection.
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource",
            typeof(IEnumerable),
            typeof(Terminal),
            new PropertyMetadata(default(IEnumerable), OnItemsSourceChanged));

        /// <summary>
        /// The margin around the contents of the terminal window, optional field with a default value of 0.
        /// </summary>
        public static readonly DependencyProperty ItemsMarginProperty = DependencyProperty.Register("ItemsMargin",
            typeof(Thickness),
            typeof(Terminal),
            new PropertyMetadata(new Thickness(), OnItemsMarginChanged));

        /// <summary>
        /// The terminal prompt to be displayed.
        /// </summary>
        public static readonly DependencyProperty PromptProperty = DependencyProperty.Register("Prompt",
            typeof(string),
            typeof(Terminal),
            new PropertyMetadata(default(string), OnPromptChanged));

        /// <summary>
        /// The current the editable line in the terminal, there is only one editable line in the terminal and this is at the bottom of the content.
        /// </summary>
        public static readonly DependencyProperty LineProperty = DependencyProperty.Register("Line",
            typeof(string),
            typeof(Terminal),
            new PropertyMetadata(default(string)));

        /// <summary>
        /// The property name of the 'value' to be displayed, optional field which if null then ToString() is called on the
        /// bound instance.
        /// </summary>
        public static readonly DependencyProperty ItemDisplayPathProperty = DependencyProperty.Register("ItemDisplayPath",
            typeof(string),
            typeof(Terminal),
            new PropertyMetadata(default(string), OnDisplayPathChanged));

        /// <summary>
        /// The property name of the 'isError' field, optional field used to determine if the terminal output is an error for the bound instance. The default value is false.
        /// </summary>
        public static readonly DependencyProperty ItemIsErrorPathProperty = DependencyProperty.Register("ItemIsErrorPath",
            typeof(string),
            typeof(Terminal),
            new PropertyMetadata(default(string), OnIsErrorPathChanged));

        /// <summary>
        /// The color of standard error messages, optional field with a default value of Red.
        /// </summary>
        public static readonly DependencyProperty ItemErrorColorProperty = DependencyProperty.Register("ItemErrorColor",
            typeof(Brush),
            typeof(Terminal),
            new PropertyMetadata(new SolidColorBrush(Colors.Red)));

        /// <summary>
        /// The height of each line in the terminal window, optional field with a default value of 10.
        /// </summary>
        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register("ItemHeight",
            typeof(int),
            typeof(Terminal),
            new PropertyMetadata(10, OnItemHeightChanged));

        private readonly Paragraph _paragraph;
        private readonly List<string> _buffer;
        private readonly Run _promptInline;

        private INotifyCollectionChanged _notifyChanged;
        private PropertyInfo _displayPathProperty;
        private PropertyInfo _isErrorPathProperty;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Terminal()
        {
            _buffer = new List<string>();

            _paragraph = new Paragraph
                         {
                             Margin = ItemsMargin,
                             LineHeight = ItemHeight
                         };

            _promptInline = new Run(Prompt);
            _paragraph.Inlines.Add(_promptInline);

            IsUndoEnabled = false;

            Document = new FlowDocument(_paragraph);
            CaretPosition = Document.ContentEnd;

            TextChanged += (s, e) => ScrollToEnd();

            DataObject.AddPastingHandler(this, PasteCommand);
            DataObject.AddCopyingHandler(this, CopyCommand);

            ContextMenu = null;

            SetResourceReference(StyleProperty, "TerminalStyle");
        }

        /// <summary>
        /// The bound items to the terminal.
        /// </summary>
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        /// The prompt of the terminal.
        /// </summary>
        public string Prompt
        {
            get { return (string)GetValue(PromptProperty); }
            set { SetValue(PromptProperty, value); }
        }

        /// <summary>
        /// The current editable line of the terminal (bottom line).
        /// </summary>
        public string Line
        {
            get { return (string)GetValue(LineProperty); }
            set { SetValue(LineProperty, value); }
        }

        /// <summary>
        /// The display path for the bound items.
        /// </summary>
        public string ItemDisplayPath
        {
            get { return (string)GetValue(ItemDisplayPathProperty); }
            set { SetValue(ItemDisplayPathProperty, value); }
        }

        /// <summary>
        /// The is error path for the bound items.
        /// </summary>
        public string ItemIsErrorPath
        {
            get { return (string)GetValue(ItemIsErrorPathProperty); }
            set { SetValue(ItemIsErrorPathProperty, value); }
        }

        /// <summary>
        /// The error color for the bound items.
        /// </summary>
        public Brush ItemErrorColor
        {
            get { return (Brush)GetValue(ItemErrorColorProperty); }
            set { SetValue(ItemErrorColorProperty, value); }
        }

        /// <summary>
        /// The individual line height for the bound items.
        /// </summary>
        public int ItemHeight
        {
            get { return (int)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        /// <summary>
        /// The margin around the bound items.
        /// </summary>
        public Thickness ItemsMargin
        {
            get { return (Thickness)GetValue(ItemsMarginProperty); }
            set { SetValue(ItemsMarginProperty, value); }
        }
        
        /// <summary>
        /// Processes every key pressed when the control has focus.
        /// </summary>
        /// <param name="args">The key pressed arguments.</param>
        protected override void OnPreviewKeyDown(KeyEventArgs args)
        {
            base.OnPreviewKeyDown(args);

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
                default:
                    args.Handled = HandleAnyOtherKey();
                    break;
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal.HandleItemsSourceChanged((IEnumerable)args.NewValue);
        }

        private static void OnPromptChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal.HandlePromptChanged((string)args.NewValue);
        }

        private static void OnItemsMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal._paragraph.Margin = (Thickness) args.NewValue;
        }

        private static void OnItemHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal._paragraph.LineHeight = (int)args.NewValue;
        }
        
        private static void OnDisplayPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal._displayPathProperty = null;
        }

        private static void OnIsErrorPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal._isErrorPathProperty = null;
        }

        private void CopyCommand(object sender, DataObjectCopyingEventArgs args)
        {
            if (!string.IsNullOrEmpty(Selection.Text))
            {
                args.DataObject.SetData(typeof(string), Selection.Text);
            }

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
                _paragraph.Inlines.Add(_promptInline);

                return;
            }

            var changed = items as INotifyCollectionChanged;
            if (changed != null)
            {
                var notifyChanged = changed;
                if (_notifyChanged != null)
                {
                    _notifyChanged.CollectionChanged -= HandleItemsChanged;
                }

                _notifyChanged = notifyChanged;
                _notifyChanged.CollectionChanged += HandleItemsChanged;

                // ReSharper disable once PossibleMultipleEnumeration
                var existingItems = items.Cast<object>().ToArray();
                if (existingItems.Any())
                {
                    ReplaceItems(existingItems);
                }
                else
                {
                    ClearItems();
                }
            }
            else
            {
                // ReSharper disable once PossibleMultipleEnumeration
                ReplaceItems(items);
            }
        }

        private void HandlePromptChanged(string prompt)
        {
            if (_promptInline == null)
            {
                return;
            }

            _promptInline.Text = prompt;
        }

        private void HandleItemsChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            BeginChange();

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddItems(args.NewItems.Cast<object>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(args.OldItems);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReplaceItems(args.NewItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveItems(args.OldItems);
                    AddItems(args.NewItems.Cast<object>());
                    break;
            }

            EndChange();
        }

        private void ClearItems()
        {
            _paragraph.Inlines.Clear();
            _paragraph.Inlines.Add(_promptInline);
            CaretPosition = CaretPosition.DocumentEnd;
        }

        private void ReplaceItems(IEnumerable items)
        {
            Contract.Requires(items != null);

            BeginChange();

            _paragraph.Inlines.Clear();
            AddItems(ConvertToEnumerable(items));

            _paragraph.Inlines.Add(_promptInline);
            CaretPosition = CaretPosition.DocumentEnd;

            EndChange();
        }

        private void AddItems(IEnumerable items)
        {
            Contract.Requires(items != null);

            _paragraph.Inlines.Remove(_promptInline);

            foreach (var item in items.Cast<object>())
            {
                var value = ExtractValue(item);
                var isError = ExtractIsError(item);

                var inline = new Run(value);
                if (isError)
                {
                    inline.Foreground = ItemErrorColor;
                }

                _paragraph.Inlines.Add(inline);
            }

            _paragraph.Inlines.Add(_promptInline);
            CaretPosition = CaretPosition.DocumentEnd;
        }

        private void RemoveItems(IEnumerable items)
        {
            foreach (var item in items.Cast<object>())
            {
                var value = ExtractValue(item);

                var run = _paragraph.Inlines
                    .Cast<Run>()
                    .FirstOrDefault(x => x.Text == value);

                if (run != null)
                {
                    _paragraph.Inlines.Remove(run);
                }
            }
        }

        private static IEnumerable<object> ConvertToEnumerable(object item)
        {
            try
            {
                return item == null ? Enumerable.Empty<object>() : ((IEnumerable)item).Cast<object>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<object>();
            }
        }

        private static TextPointer GetTextPointer(TextPointer textPointer, LogicalDirection direction)
        {
            var currentTextPointer = textPointer;
            while (currentTextPointer != null)
            {
                var nextPointer = currentTextPointer.GetNextContextPosition(direction);
                if (nextPointer == null)
                {
                    return null;
                }

                if (nextPointer.GetPointerContext(direction) == TextPointerContext.Text)
                {
                    return nextPointer;
                }

                currentTextPointer = nextPointer;
            }

            return null;
        }

        private string ExtractValue(object item)
        {
            var displayPath = ItemDisplayPath;
            if (displayPath == null)
            {
                return item == null ? string.Empty : item.ToString();
            }

            if (_displayPathProperty == null)
            {
                _displayPathProperty = item.GetType().GetProperty(displayPath);
            }

            var value = _displayPathProperty.GetValue(item, null);
            return value == null ? string.Empty : value.ToString();
        }

        private bool ExtractIsError(object item)
        {
            var isErrorPath = ItemIsErrorPath;
            if (isErrorPath == null)
            {
                return false;
            }

            if (_isErrorPathProperty == null)
            {
                _isErrorPathProperty = item.GetType().GetProperty(isErrorPath);
            }

            var value = _isErrorPathProperty.GetValue(item, null);
            return (bool)value;
        }

        private bool HandleCopyKeys(KeyEventArgs args)
        {
            if (args.Key == Key.C)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    return false;
                }

                var promptEnd = _promptInline.ContentEnd;

                var pos = CaretPosition.CompareTo(promptEnd);
                var selectionPos = Selection.Start.CompareTo(CaretPosition);

                return pos < 0 || selectionPos < 0;
            }
            
            if (args.Key == Key.X || args.Key == Key.V)
            {
                var promptEnd = _promptInline.ContentEnd;

                var pos = CaretPosition.CompareTo(promptEnd);
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
            }

            return false;
        }


        private bool HandleUpDownKeys(KeyEventArgs args)
        {
            var pos = CaretPosition.CompareTo(_promptInline.ContentEnd);

            if (pos < 0)
            {
                return false;
            }

            if (!_buffer.Any())
            {
                return true;
            }

            ClearAfterPrompt();

            string existingLine;
            if (args.Key == Key.Down)
            {
                existingLine = _buffer[_buffer.Count - 1];
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

            CaretPosition = CaretPosition.DocumentEnd;

            OnLineEntered();
        }

        private bool HandleAnyOtherKey()
        {
            var promptEnd = _promptInline.ContentEnd;

            var pos = CaretPosition.CompareTo(promptEnd);

            if (pos < 0)
            {
                return true;
            }
            return false;
        }

        private bool HandleBackspaceKey()
        {
            var promptEnd = _promptInline.ContentEnd;

            var textPointer = GetTextPointer(promptEnd, LogicalDirection.Forward);
            if (textPointer == null)
            {
                var pos = CaretPosition.CompareTo(promptEnd);

                if (pos <= 0)
                {
                    return true;
                }
            }
            else
            {
                var pos = CaretPosition.CompareTo(textPointer);
                if (pos <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HandleLeftKey()
        {
            var promptEnd = _promptInline.ContentEnd;

            var textPointer = GetTextPointer(promptEnd, LogicalDirection.Forward);
            if (textPointer == null)
            {
                var pos = CaretPosition.CompareTo(promptEnd);

                if (pos == 0)
                {
                    return true;
                }
            }
            else
            {
                var pos = CaretPosition.CompareTo(textPointer);
                if (pos == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HandleDeleteKey()
        {
            var pos = CaretPosition.CompareTo(_promptInline.ContentEnd);

            return pos < 0;
        }

        private void OnLineEntered()
        {
            var handler = LineEntered;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void AddLine(string line)
        {
            CaretPosition = CaretPosition.DocumentEnd;

            var inline = new Run(line);
            _paragraph.Inlines.Add(inline);

            CaretPosition = CaretPosition.DocumentEnd;
        }

        private string AggregateAfterPrompt()
        {
            var inlineList = _paragraph.Inlines.ToList();
            var promptIndex = inlineList.IndexOf(_promptInline);

            return inlineList.Where((x, i) => i > promptIndex)
                .Cast<Run>()
                .Select(x => x.Text)
                .Aggregate(string.Empty, (current, part) => current + part);
        }

        private void ClearAfterPrompt()
        {
            var inlineList = _paragraph.Inlines.ToList();
            var promptIndex = inlineList.IndexOf(_promptInline);

            foreach (var inline in inlineList.Where((x, i) => i > promptIndex))
            {
                _paragraph.Inlines.Remove(inline);
            }
        }
    }
}