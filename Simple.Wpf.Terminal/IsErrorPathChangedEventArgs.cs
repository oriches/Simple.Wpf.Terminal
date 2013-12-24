namespace Simple.Wpf.Terminal
{
    using System;

    public sealed class IsErrorPathChangedEventArgs : EventArgs
    {
        public string IsErrorPath { get; private set; }

        public IsErrorPathChangedEventArgs(string isErrorPath)
        {
            IsErrorPath = isErrorPath;
        }
    }
}