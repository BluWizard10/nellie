using Avalonia.Controls;
using Avalonia.Input;
using Nellie.ViewModels;

namespace Nellie.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnPlaylistDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.SelectedTrack is not null)
                vm.PlayTrackCommand.Execute(vm.SelectedTrack);
        }
    }
}
