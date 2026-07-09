using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nellie.Services;
using Nellie.ViewModels;
using Nellie.Views;

namespace Nellie
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                var viewModel = new MainWindowViewModel(new FilePickerService(window));
                window.DataContext = viewModel;

                desktop.MainWindow = window;
                desktop.ShutdownRequested += (_, _) => viewModel.Dispose();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
