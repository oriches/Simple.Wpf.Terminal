using System;
using System.Windows.Input;

namespace Simple.Wpf.Terminal.Example;

public sealed class RelayCommand : RelayCommand<object>
{
    public RelayCommand(Action execute)
        : base(_ => execute(), _ => true)
    {
    }

    public RelayCommand(Action execute, Func<bool> canExecute) : base(_ => execute(), _ => canExecute())
    {
    }
}

public class RelayCommand<T> : IRelayCommand<T>
{
    private readonly Func<T, bool> _canExecute;
    private readonly Action<T> _execute;

    public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public void Execute(object parameter)
    {
        if (CanExecute(parameter)) _execute((T)parameter);
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}