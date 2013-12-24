namespace Simple.Wpf.Terminal
{
    using System;

    public sealed class DisplayPathChangedEventArgs : EventArgs
    {
        public string DisplayPath { get; private set; }

        public DisplayPathChangedEventArgs(string displayPath)
        {
            DisplayPath = displayPath;
        }
    }
}