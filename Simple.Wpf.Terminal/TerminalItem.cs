namespace Simple.Wpf.Terminal
{
    using System;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Media;

    public sealed class TerminalItem : DependencyObject
    {
        public event EventHandler<DisplayPathChangedEventArgs> DisplayPathChanged;
        public event EventHandler<IsErrorPathChangedEventArgs> IsErrorPathChanged;
 
        public static readonly DependencyProperty DisplayPathProperty = DependencyProperty.Register("DisplayPath",
            typeof(string),
            typeof(TerminalItem),
            new PropertyMetadata(default(string), OnDisplayPathChanged));

        public static readonly DependencyProperty IsErrorPathProperty = DependencyProperty.Register("IsErrorPath",
            typeof(string),
            typeof(TerminalItem),
            new PropertyMetadata(default(string), OnIsErrorPathChanged));

        public static readonly DependencyProperty PromptProperty = DependencyProperty.Register("Prompt",
            typeof(string),
            typeof(TerminalItem),
            new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ErrorColorProperty = DependencyProperty.Register("ErrorColor",
            typeof(Brush),
            typeof(TerminalItem),
            new PropertyMetadata(new SolidColorBrush(Colors.Red)));

        public string DisplayPath
        {
            get { return (string)GetValue(DisplayPathProperty); }
            set { SetValue(DisplayPathProperty, value); }
        }

        public string IsErrorPath
        {
            get { return (string)GetValue(IsErrorPathProperty); }
            set { SetValue(IsErrorPathProperty, value); }
        }

        public string Prompt
        {
            get { return (string)GetValue(PromptProperty); }
            set { SetValue(PromptProperty, value); }
        }

        public Brush ErrorColor
        {
            get { return (Brush)GetValue(ErrorColorProperty); }
            set { SetValue(ErrorColorProperty, value); }
        }

        private static void OnDisplayPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminalItem = ((TerminalItem)d);
            string displayPath = null;
            if (args.NewValue != null)
            {
                displayPath = (string)args.NewValue;
            }

            terminalItem.OnDisplayPathChangedImpl(displayPath);
        }

        private static void OnIsErrorPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue == args.OldValue)
            {
                return;
            }

            var terminalItem = ((TerminalItem)d);
            string isErrorPath = null;
            if (args.NewValue != null)
            {
                isErrorPath = (string)args.NewValue;
            }

            terminalItem.OnIsErrorPathChangedImpl(isErrorPath);
        }

        private void OnDisplayPathChangedImpl(string displayPath)
        {
            var handler = DisplayPathChanged;

            if (handler != null)
            {
                handler(this, new DisplayPathChangedEventArgs(displayPath));
            }
        }

        private void OnIsErrorPathChangedImpl(string isErrorPath)
        {
            var handler = IsErrorPathChanged;

            if (handler != null)
            {
                handler(this, new IsErrorPathChangedEventArgs(isErrorPath));
            }
        }
    }
}