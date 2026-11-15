using Microsoft.UI.Xaml;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"TOEFL Simulation {AppInfo.Version}";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    }
}
