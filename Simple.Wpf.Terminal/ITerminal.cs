namespace Simple.Wpf.Terminal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Data;

    /// <summary>
    /// Exposes the dependancy properties and events exposed by the terminal control.
    /// </summary>
    public interface ITerminal
    {
        /// <summary>
        /// Event fired when the user presses the Enter key.
        /// </summary>
        event EventHandler LineEntered;

        /// <summary>
        /// The bound items to the terminal.
        /// </summary>
        IEnumerable ItemsSource { get; set; }

        /// <summary>
        /// Bound autocompletion strings to the terminal.
        /// </summary>
        IEnumerable<string> AutoCompletionsSource { get; set; }

        /// <summary>
        /// The prompt of the terminal.
        /// </summary>
        string Prompt { get; set; }

        /// <summary>
        /// The current editable line of the terminal (bottom line).
        /// </summary>
        string Line { get; set; }

        /// <summary>
        /// The display path for the bound items.
        /// </summary>
        string ItemDisplayPath { get; set; }

        /// <summary>
        /// The error color for the bound items.
        /// </summary>
        IValueConverter LineColorConverter { get; set; }

        /// <summary>
        /// The individual line height for the bound items.
        /// </summary>
        int ItemHeight { get; set; }

        /// <summary>
        /// The margin around the bound items.
        /// </summary>
        Thickness ItemsMargin { get; set; }
    }
}
