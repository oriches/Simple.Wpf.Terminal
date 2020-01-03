using System.Windows;

namespace Simple.Wpf.Terminal.Example
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = new MainWindow {DataContext = new ExampleViewModel()};

            window.Show();
        }
    }
}