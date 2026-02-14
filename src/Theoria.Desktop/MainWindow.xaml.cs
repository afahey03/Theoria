using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Web.WebView2.Wpf;
using Theoria.Desktop.ViewModels;

namespace Theoria.Desktop;

/// <summary>
/// Main application window.
/// DataContext is set in XAML to <see cref="SearchViewModel"/>.
/// Uses a TabControl: the first tab is always the search view, and clicking
/// a result opens a new tab with a WebView2 (Chromium) browser showing the
/// article in-app — no script-error dialogs, modern rendering.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the clicked URL in a new in-app tab with a WebView2 browser.
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

        var tabTitle = title.Length > 35 ? title[..32] + "\u2026" : title;
        var navigateUrl = e.Uri.AbsoluteUri;

        // --- WebView2 browser (Chromium-based — no script error dialogs) ---
        var webView = new WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 250, 248, 245)
        };

        // --- Tab header: title + close button ---
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var titleBlock = new TextBlock
        {
            Text = tabTitle,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            Foreground = FindResource("HeaderFg") as System.Windows.Media.Brush,
            ToolTip = title
        };
        var closeBtn = new Button
        {
            Content = "\u2715",
            FontSize = 10,
            Padding = new Thickness(5, 2, 5, 2),
            Margin = new Thickness(8, 0, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = FindResource("HeaderFg") as System.Windows.Media.Brush,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };

        header.Children.Add(titleBlock);
        header.Children.Add(closeBtn);

        // --- Toolbar: buttons + url bar ---
        var toolbar = CreateArticleToolbar(navigateUrl, webView);

        // --- Layout: toolbar on top, webview fills remaining space ---
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(webView, 1);
        grid.Children.Add(toolbar);
        grid.Children.Add(webView);

        var tab = new TabItem
        {
            Header = header,
            Content = grid,
            Style = FindResource("FlatTabItem") as Style
        };

        // Close button removes the tab, disposes the browser, switches back
        closeBtn.Click += (_, _) =>
        {
            MainTabs.Items.Remove(tab);
            webView.Dispose();
            MainTabs.SelectedIndex = 0;
        };

        // Hover effect on close button
        closeBtn.MouseEnter += (_, _) =>
            closeBtn.Foreground = System.Windows.Media.Brushes.White;
        closeBtn.MouseLeave += (_, _) =>
            closeBtn.Foreground = FindResource("HeaderFg") as System.Windows.Media.Brush;

        MainTabs.Items.Add(tab);
        MainTabs.SelectedItem = tab;

        // Navigate after the tab is rendered
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Navigate(navigateUrl);
            }
            catch
            {
                // WebView2 runtime unavailable — fall back to default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = navigateUrl,
                    UseShellExecute = true
                });
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        e.Handled = true;
    }

    /// <summary>
    /// Creates a flat, modern toolbar for an article tab.
    /// </summary>
    private Border CreateArticleToolbar(string url, WebView2 webView)
    {
        var urlBox = new TextBox
        {
            Text = url,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C4B8A8")),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E0F04")),
            CaretBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C4B8A8")),
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A3928")),
            FontSize = 12,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, 'Courier New', monospace"),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(10, 0, 0, 0),
        };

        // Double-click: select word, Triple-click: select all
        urlBox.PreviewMouseDown += (_, e) =>
        {
            if (e.ClickCount == 3)
            {
                urlBox.SelectAll();
                e.Handled = true;
            }
        };
        urlBox.MouseDoubleClick += (_, e) =>
        {
            // Select the "word" segment around the caret (delimited by / . : ? & = etc.)
            var text = urlBox.Text;
            var pos = urlBox.CaretIndex;
            if (string.IsNullOrEmpty(text) || pos < 0) return;

            var separators = new HashSet<char> { '/', '.', ':', '?', '&', '=', '#', '-', '_', ' ' };
            int start = pos;
            while (start > 0 && !separators.Contains(text[start - 1]))
                start--;
            int end = pos;
            while (end < text.Length && !separators.Contains(text[end]))
                end++;

            urlBox.Select(start, end - start);
            e.Handled = true;
        };

        // Navigate when user presses Enter
        urlBox.KeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Return) return;
            e.Handled = true;

            var input = urlBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            // Auto-prepend https:// if no scheme
            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "https://" + input;
            }

            try
            {
                webView.CoreWebView2?.Navigate(input);
            }
            catch
            {
                // Invalid URL — ignore
            }

            // Move focus away from the textbox
            System.Windows.Input.Keyboard.ClearFocus();
        };

        // Keep URL box in sync with navigation
        webView.SourceChanged += (_, _) =>
        {
            var newUrl = webView.Source?.AbsoluteUri;
            if (!string.IsNullOrEmpty(newUrl) && !urlBox.IsKeyboardFocused)
                urlBox.Text = newUrl;
        };

        var toolbarBtnStyle = CreateToolbarButtonStyle();

        var backBtn = new Button { Content = "\u2190", Style = toolbarBtnStyle, ToolTip = "Back" };
        backBtn.Click += (_, _) =>
        {
            try { if (webView.CanGoBack) webView.GoBack(); } catch { }
        };

        var fwdBtn = new Button { Content = "\u2192", Style = toolbarBtnStyle, ToolTip = "Forward" };
        fwdBtn.Click += (_, _) =>
        {
            try { if (webView.CanGoForward) webView.GoForward(); } catch { }
        };

        var openBtn = new Button { Content = "\u2197 Open in Browser", Style = toolbarBtnStyle, ToolTip = "Open in default browser" };
        openBtn.Click += (_, _) =>
        {
            var currentSrc = webView.Source?.AbsoluteUri ?? url;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = currentSrc, UseShellExecute = true });
            }
            catch { }
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(backBtn);
        stack.Children.Add(fwdBtn);
        stack.Children.Add(openBtn);

        var dock = new DockPanel();
        DockPanel.SetDock(stack, Dock.Left);
        dock.Children.Add(stack);
        dock.Children.Add(urlBox);

        return new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E1A0C")),
            Padding = new Thickness(8, 6, 12, 6),
            Child = dock
        };
    }

    /// <summary>
    /// Creates a flat toolbar button style with rounded corners and hover effect.
    /// </summary>
    private static Style CreateToolbarButtonStyle()
    {
        var normalBg = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A3928"));
        var hoverBg = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B4C30"));
        var fg = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F0E8"));

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(0, 0, 4, 0)));
        style.Setters.Add(new Setter(Button.FontSizeProperty, 12.0));
        style.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
        style.Setters.Add(new Setter(Button.ForegroundProperty, fg));
        style.Setters.Add(new Setter(Button.BackgroundProperty, normalBg));
        style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));

        // ControlTemplate with rounded corners
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, normalBg);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
        borderFactory.Name = "bd";

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(cp);
        template.VisualTree = borderFactory;

        var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "bd"));
        template.Triggers.Add(hover);

        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        return style;
    }
}