using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OfflineAssistant;

public sealed partial class MainPage : Page
{
    public MainPage() => InitializeComponent();

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var size = Math.Max(0, Math.Min(700, Math.Min(e.NewSize.Width, e.NewSize.Height) - 32));
        SphereView.Width = size;
        SphereView.Height = size;
    }

    private void Sphere_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        (App.CurrentWindow as MainWindow)?.OpenChat();
}
