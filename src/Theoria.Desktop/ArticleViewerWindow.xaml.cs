using System.Diagnostics;
using System.Windows;

namespace Theoria.Desktop;

/// <summary>
/// A secondary window that displays a web page inside the desktop application
/// using the built-in WPF WebBrowser control.
/// </summary>
public partial class ArticleViewerWindow : Window
{
    private readonly string _url;

    public ArticleViewerWindow(string url, string title)
    {
        InitializeComponent();
        _url = url;
        Title = $"Theoria – {title}";
        UrlDisplay.Text = url;
    }

    /// <summary>Called after the window is loaded — navigates to the URL.</summary>
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        try
        {
            Browser.Navigate(_url);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to load page: {ex.Message}", "Navigation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack)
            Browser.GoBack();
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _url,
            UseShellExecute = true
        });
    }
}
