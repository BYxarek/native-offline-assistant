using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OfflineAssistant;

public sealed partial class SphereVisual : UserControl
{
    public SphereVisual() => InitializeComponent();

    private void Visual_Loaded(object sender, RoutedEventArgs e) => SphereMotion.Begin();
    private void Visual_Unloaded(object sender, RoutedEventArgs e) => SphereMotion.Stop();
}
