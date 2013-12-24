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

    public sealed class Terminal : RichTextBox
    {
        public event EventHandler LineChanged;

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource",
            typeof(IEnumerable),
            typeof(Terminal),
            new PropertyMetadata(default(IEnumerable), OnItemsSourceChanged));

        public static readonly DependencyProperty LineProperty = DependencyProperty.Register("Line",
           typeof(string),
           typeof(TerminalItem),
           new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register("Item",
          typeof(string),
          typeof(TerminalItem),
          new PropertyMetadata(default(TerminalItem)));
        
        private readonly Paragraph _paragraph;
        private readonly List<string> _buffer;
        private readonly TerminalItem _item;

        private Run _promptInline;
        private INotifyCollectionChanged _notifyChanged;
        private PropertyInfo _displayPathProperty;
        private PropertyInfo _isErrorPathProperty;

        public Terminal()
        {
            _item = new TerminalItem();
            _item.DisplayPathChanged += (s, e) => { _displayPathProperty = null; };
            _item.IsErrorPathChanged += (s, e) => { _isErrorPathProperty = null; };

            _buffer = new List<string>();

            _paragraph = new Paragraph();

            Document = new FlowDocument(_paragraph);

            TextChanged += (s, e) => ScrollToEnd();

            DataObject.AddPastingHandler(this, PasteCommand);
            DataObject.AddCopyingHandler(this, CopyCommand);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public string Line
        {
            get { return (string)GetValue(LineProperty); }
            set { SetValue(LineProperty, value); }
        }

        public TerminalItem Item
        {
            get { return (TerminalItem)GetValue(ItemProperty); }
            set { SetValue(ItemProperty, value); }
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
            if (args.NewValue is INotifyCollectionChanged)
            {
                terminal.ObserveChanges((IEnumerable)args.NewValue);
            }
            else
            {
                terminal.ReplaceValues((IEnumerable)args.NewValue);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            _promptInline = new Run(_item.Prompt);
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            if (_notifyChanged != null)
            {
                _notifyChanged.CollectionChanged -= HandleValuesChanged;
            }
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

        private void ObserveChanges(IEnumerable values)
        {
            var notifyChanged = (INotifyCollectionChanged)values;

            if (_notifyChanged != null)
            {
                _notifyChanged.CollectionChanged += HandleValuesChanged;
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
                    inline.Foreground = _item.ErrorColor;
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
            var displayPath = _item.DisplayPath;
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
            var isErrorPath = _item.IsErrorPath;
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
            var handler = LineChanged;

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