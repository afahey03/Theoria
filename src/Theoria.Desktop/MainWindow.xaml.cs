using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Theoria.Desktop.ViewModels;

namespace Theoria.Desktop;

/// <summary>
/// Main application window.
/// DataContext is set in XAML to <see cref="ViewModels.SearchViewModel"/>.
/// Uses a TabControl: the first tab is always the search view, and clicking
/// a result opens a new tab with a WebBrowser showing the article in-app.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the clicked URL in a new in-app tab with a WebBrowser.
    /// </summary>
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is null) return;

        var title = "Article";
        if (sender is System.Windows.Documents.Hyperlink hyperlink)
        {
            var run = hyperlink.Inlines.FirstInline as System.Windows.Documents.Run;
            if (run is not null && !string.IsNullOrWhiteSpace(run.Text))
                title = run.Text;
        }

        // Truncate long titles for the tab header
        var tabTitle = title.Length > 30 ? title[..27] + "..." : title;

        var browser = new WebBrowser();

        // Tab header with title + close button
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var titleBlock = new TextBlock
        {
            Text = tabTitle,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = title
        };
        var closeBtn = new Button
        {
            Content = "\u2715",
            FontSize = 11,
            Padding = new Thickness(4, 1, 4, 1),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };

        header.Children.Add(titleBlock);
        header.Children.Add(closeBtn);

        // Tab content: toolbar + browser
        var toolbar = CreateArticleToolbar(e.Uri.AbsoluteUri, browser);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(browser, 1);
        grid.Children.Add(toolbar);
        grid.Children.Add(browser);

        var tab = new TabItem
        {
            Header = header,
            Content = grid
        };

        // Close button removes the tab and switches back to search
        closeBtn.Click += (_, _) =>
        {
            MainTabs.Items.Remove(tab);
            MainTabs.SelectedIndex = 0;
        };

        MainTabs.Items.Add(tab);
        MainTabs.SelectedItem = tab;

        // Navigate after the tab is visible
        Dispatcher.BeginInvoke(() =>
        {
            try { browser.Navigate(e.Uri.AbsoluteUri); }
            catch { /* best effort */ }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        e.Handled = true;
    }

    /// <summary>
    /// Creates a toolbar for an article tab with Back, Forward, and Open in Browser buttons.
    /// </summary>
    private static Border CreateArticleToolbar(string url, WebBrowser browser)
    {
        var urlText = new TextBlock
        {
            Text = url,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B5A898")),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var btnStyle = new Style(typeof(Button));
        btnStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(10, 4, 10, 4)));
        btnStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(0, 0, 4, 0)));
        btnStyle.Setters.Add(new Setter(Button.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A3928"))));
        btnStyle.Setters.Add(new Setter(Button.ForegroundProperty, System.Windows.Media.Brushes.White));
        btnStyle.Setters.Add(new Setter(Button.BorderBrushProperty,
            new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B4C30"))));
        btnStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));

        var backBtn = new Button { Content = "\u2190 Back", Style = btnStyle };
        backBtn.Click += (_, _) => { if (browser.CanGoBack) browser.GoBack(); };

        var fwdBtn = new Button { Content = "Forward \u2192", Style = btnStyle };
        fwdBtn.Click += (_, _) => { if (browser.CanGoForward) browser.GoForward(); };

        var openBtn = new Button { Content = "Open in Browser", Style = btnStyle };
        openBtn.Click += (_, _) =>
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(backBtn);
        stack.Children.Add(fwdBtn);
        stack.Children.Add(openBtn);

        var dock = new DockPanel();
        DockPanel.SetDock(stack, Dock.Right);
        dock.Children.Add(stack);
        dock.Children.Add(urlText);

        return new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3C2415")),
            Padding = new Thickness(10, 6, 10, 6),
            Child = dock
        };
    }
}