using Microsoft.UI.Xaml;

namespace TOEFLSimulation.App;

public partial class App : Application
{
    private MainWindow? window;

    public App()
    {
        InitializeComponent();
        // Missing resources are otherwise reported only as "XAML parsing failed."
        DebugSettings.XamlResourceReferenceFailed += (_, e) => CrashLog.Write(new InvalidOperationException("XAML resource reference failed: " + e.Message));
        DebugSettings.BindingFailed += (_, e) => CrashLog.Write(new InvalidOperationException("Binding failed: " + e.Message));
        UnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception);
            if (SmokeTest.Enabled)
            {
                SmokeTest.RecordFailure("unhandled", e.Exception);
                e.Handled = true;
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();

        SmokeTest.Configure(Environment.GetCommandLineArgs());
        if (SmokeTest.Enabled) _ = SmokeTest.RunAsync(window);
    }
}
