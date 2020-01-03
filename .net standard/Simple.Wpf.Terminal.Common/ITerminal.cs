using System;
using System.Collections;
using System.Collections.Generic;

namespace Simple.Wpf.Terminal.Common
{
    /// <summary>
    ///     Exposes the dependency properties and events exposed by the terminal control.
    /// </summary>
    public interface ITerminal
    {
        /// <summary>
        ///     The bound items to the terminal.
        /// </summary>
        IEnumerable ItemsSource { get; set; }

        /// <summary>
        ///     Bound auto completion strings to the terminal.
        /// </summary>
        IEnumerable<string> AutoCompletionsSource { get; set; }

        /// <summary>
        ///     The prompt of the terminal.
        /// </summary>
        string Prompt { get; set; }

        /// <summary>
        ///     The current editable line of the terminal (bottom line).
        /// </summary>
        string Line { get; set; }

        /// <summary>
        ///     The display path for the bound items.
        /// </summary>
        string ItemDisplayPath { get; set; }

        /// <summary>
        ///     Event fired when the user presses the Enter key.
        /// </summary>
        event EventHandler LineEntered;
    }
}