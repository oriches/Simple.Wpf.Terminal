using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace Simple.Wpf.Terminal.Example
{
    public sealed class ExampleViewModel : BaseViewModel
    {
        private readonly ObservableCollection<string> _items;
        private ICommand _executeItemCommand;

        public ExampleViewModel()
        {
            _items = new ObservableCollection<string>();

            var executingAssembly = Assembly.GetExecutingAssembly();
            foreach (var assembly in executingAssembly.GetReferencedAssemblies())
                _items.Add("Referenced assembly: " + assembly.FullName);

            _items.Add(string.Empty);
            _items.Add(string.Empty);
            _items.Add("Type a line and press ENTER, it will be added to the output...");
            _items.Add(string.Empty);

            _executeItemCommand = new RelayCommand<string>(AddItem, x => true);
        }

        public IEnumerable<string> Items => _items;

        public ICommand ExecuteItemCommand
        {
            get => _executeItemCommand;
            set => SetPropertyAndNotify(ref _executeItemCommand, value, "ExecuteItemCommand");
        }

        private void AddItem(string item)
        {
            _items.Add(item);
        }
    }
}