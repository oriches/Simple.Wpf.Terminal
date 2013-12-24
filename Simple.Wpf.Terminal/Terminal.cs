namespace Simple.Wpf.Terminal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// A WPF user control which mimics a terminal\console window, you are responsible for the service
    /// behind the control - the data to display and processing the line when the Enter key is pressed
    /// LineEntered event.
    /// </summary>
    public sealed class Terminal : RichTextBox
    {
        /// <summary>
        /// Event fired when the user presses the Enter key
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
        /// The current the editable line in the terminal, there is only one editable line in the terminal and this is at the bottom
        /// of the content.
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
        /// The property name of the 'isError' field, optional field used to determine if the terminal output is an error for the
        /// bound instance. The default value is false.
        /// bound instance.
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
        /// Default constructor
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

            Document = new FlowDocument(_paragraph);

            TextChanged += (s, e) => ScrollToEnd();

            DataObject.AddPastingHandler(this, PasteCommand);
            DataObject.AddCopyingHandler(this, CopyCommand);
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
        
        protected override void OnPreviewKeyDown(KeyEventArgs args)
        {
            base.OnPreviewKeyDown(args);

            if (args.Key == Key.Enter)
            {
                HandleEnterKey();
                args.Handled = true;
            }
            else if (args.Key == Key.PageUp || args.Key == Key.PageDown)
            {
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                ClearAfterPrompt();
                args.Handled = true;
            }
            else if (args.Key == Key.Down || args.Key == Key.Up)
            {
                if (_buffer.Any())
                {
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
                }

                args.Handled = true;
            }
            else if (args.Key == Key.Left || args.Key == Key.Back)
            {
                var promptEnd = _promptInline.ContentEnd;

                var textPointer = GetTextPointer(promptEnd, LogicalDirection.Forward);
                if (textPointer == null)
                {
                    if (CaretPosition.CompareTo(promptEnd) == 0)
                    {
                        args.Handled = true;
                    }
                }
                else
                {
                    if (CaretPosition.CompareTo(textPointer) == 0)
                    {
                        args.Handled = true;
                    }
                }
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            terminal.ProcessItems((IEnumerable)args.NewValue);
        }

        private static void OnPromptChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminal = ((Terminal)d);
            if (terminal._promptInline != null)
            {
                var newPrompt = string.Empty;
                if (args.NewValue != null)
                {
                    newPrompt = args.NewValue.ToString();
                }

                terminal._promptInline.Text = newPrompt;
            }
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
                AddLine(text);
            }

            args.CancelCommand();
            args.Handled = true;
        }

        private void ProcessItems(IEnumerable items)
        {
            if (items == null)
            {
                _paragraph.Inlines.Clear();
                _paragraph.Inlines.Add(_promptInline);

                return;
            }

            if (items is INotifyCollectionChanged)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                ObserveValues(items);
            }
            else
            {
                // ReSharper disable once PossibleMultipleEnumeration
                ReplaceValues(items);
            }

            // ReSharper disable once PossibleMultipleEnumeration
            var valuesNow = items.Cast<object>().ToArray();
            if (valuesNow.Any())
            {
                _paragraph.Inlines.Remove(_promptInline);

                AddOutputs(valuesNow);

                _paragraph.Inlines.Add(_promptInline);
                CaretPosition = CaretPosition.DocumentEnd;
            }
        }

        private void ObserveValues(IEnumerable values)
        {
            var notifyChanged = (INotifyCollectionChanged)values;

            if (_notifyChanged != null)
            {
                _notifyChanged.CollectionChanged -= HandleValuesChanged;
            }

            _notifyChanged = notifyChanged;
            _notifyChanged.CollectionChanged += HandleValuesChanged;
        }

        private void HandleValuesChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            _paragraph.Inlines.Remove(_promptInline);

            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                AddOutputs(args.NewItems.Cast<object>());
            }
            else
            {
                ReplaceValues(args.NewItems);
            }

            _paragraph.Inlines.Add(_promptInline);
            CaretPosition = CaretPosition.DocumentEnd;
        }

        private void ReplaceValues(IEnumerable outputs)
        {
            _paragraph.Inlines.Clear();
            AddOutputs(ConvertToEnumerable(outputs));

            _paragraph.Inlines.Add(_promptInline);
            CaretPosition = CaretPosition.DocumentEnd;
        }

        private void AddOutputs(IEnumerable outputs)
        {
            foreach (var output in outputs.Cast<object>())
            {
                var value = ExtractValue(output);
                var isError = ExtractIsError(output);

                var inline = new Run(value);
                if (isError)
                {
                    inline.Foreground = ItemErrorColor;
                }

                _paragraph.Inlines.Add(inline);
            }
        }

        private static IEnumerable<object> ConvertToEnumerable(object values)
        {
            try
            {
                return values == null ? Enumerable.Empty<object>() : ((IEnumerable)values).Cast<object>();
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

        private string ExtractValue(object output)
        {
            var displayPath = ItemDisplayPath;
            if (displayPath == null)
            {
                return output == null ? string.Empty : output.ToString();
            }

            if (_displayPathProperty == null)
            {
                _displayPathProperty = output.GetType().GetProperty(displayPath);
            }

            var value = _displayPathProperty.GetValue(output, null);
            return value == null ? string.Empty : value.ToString();
        }

        private bool ExtractIsError(object output)
        {
            var isErrorPath = ItemIsErrorPath;
            if (isErrorPath == null)
            {
                return false;
            }

            if (_isErrorPathProperty == null)
            {
                _isErrorPathProperty = output.GetType().GetProperty(isErrorPath);
            }

            var value = _isErrorPathProperty.GetValue(output, null);
            return (bool)value;
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