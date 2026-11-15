using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Exam;

/// <summary>
/// Starts a practice test full-window (the Mac ContentView's examDestination). Pages call <see cref="Start"/> from the UI
/// thread: the Practice Tests page for every mode, the Writing and Speaking guides for their section practice.
/// When the test finishes ("Save &amp; close" on the score report) the result is added to Score History and the app opens
/// that page; exiting without saving returns to the page the test was started from.
/// </summary>
public static class ExamLauncher
{
    private static bool starting;

    public static void Start(ExamMode mode) => _ = StartAsync(mode);

    private static async Task StartAsync(ExamMode mode)
    {
        if (starting) return;
        starting = true;
        try
        {
            await AppServices.Ready;
            ExamView? view = null;
            view = new ExamView(mode,
                onComplete: result =>
                {
                    AppServices.Core.History.Add(result);
                    view?.Shutdown();
                    MainWindow.Current.CloseExam("history");
                },
                onExit: () =>
                {
                    view?.Shutdown();
                    MainWindow.Current.CloseExam();
                });
            MainWindow.Current.ShowExam(view);
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
        }
        finally
        {
            starting = false;
        }
    }
}
