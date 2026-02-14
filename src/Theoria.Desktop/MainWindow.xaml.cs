using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Theoria.Desktop.ViewModels;

namespace Theoria.Desktop;

/// <summary>
/// Main application window.
/// DataContext is set in XAML to <see cref="ViewModels.SearchViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the clicked URL in the default browser.
    /// </summary>
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }

    /// <summary>
    /// Triggers "load more" when scrolled near the bottom.
    /// </summary>
    private void ListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 50
            && e.VerticalChange > 0)
        {
            if (DataContext is SearchViewModel vm && vm.LoadMoreCommand.CanExecute(null))
                vm.LoadMoreCommand.Execute(null);
        }
    }
}