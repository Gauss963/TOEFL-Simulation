using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Exam;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace TOEFLSimulation.App;

/// <summary>
/// Launch with <c>--smoke-test &lt;folder&gt;</c> (used by CI): opens every sidebar page and walks every
/// practice mode through its sections, saving screenshots and a log to the folder, then exits 0 when
/// nothing threw and 1 otherwise. Nothing is saved to the learner's history.
/// </summary>
internal static class SmokeTest
{
    public static string? OutputDirectory { get; private set; }
    private static readonly List<string> Log = [];
    private static int failures;

    public static bool Enabled => OutputDirectory is not null;

    public static void Configure(string[] args)
    {
        var index = Array.IndexOf(args, "--smoke-test");
        if (index >= 0 && index + 1 < args.Length) OutputDirectory = Path.GetFullPath(args[index + 1]);
    }

    public static void RecordFailure(string where, Exception exception)
    {
        failures++;
        Log.Add($"FAIL {where}: {exception}");
    }

    public static async Task RunAsync(MainWindow window)
    {
        Directory.CreateDirectory(OutputDirectory!);
        var shot = 0;
        async Task Step(string name, Action action, int settleMs = 900)
        {
            try
            {
                action();
                await Task.Delay(settleMs);
                await Capture(window, $"{++shot:000}-{name}");
                Log.Add($"ok   {name}");
            }
            catch (Exception exception)
            {
                RecordFailure(name, exception);
            }
        }

        try
        {
            await AppServices.Ready;
            await Task.Delay(1500);
            await Step("dashboard", () => { });
            foreach (var tag in new[] { "wordsDrill", "readingDrill", "writingGuide", "speakingGuide", "study", "history", "resources", "dashboard" })
                await Step($"page-{tag}", () => window.Navigate(tag), 1500);

            foreach (var mode in Enum.GetValues<ExamMode>())
            {
                TOEFLExamResult? finished = null;
                ExamView? view = null;
                await Step($"{mode}-start", () =>
                {
                    view = new ExamView(mode, onComplete: result => finished = result, onExit: () => { });
                    window.ShowExam(view);
                });
                if (view is null) continue;
                var session = view.SessionForTesting;
                var sections = 0;
                while (session.Phase == TOEFLExamPhase.Directions && sections++ < 6)
                {
                    var label = $"{mode}-{session.CurrentSection.Kind}";
                    await Step($"{label}-directions", () => { });
                    await Step($"{label}-begin", session.BeginSection, 2500);
                    for (var i = 0; i < 6 && session.Phase == TOEFLExamPhase.Testing; i++)
                    {
                        var before = session.CurrentQuestionIndex;
                        await Step($"{label}-next{i + 1}", session.GoNext, 1500);
                        if (session.CurrentQuestionIndex == before) break; // waiting for audio or a timed part
                    }
                    await Step($"{label}-finish", () =>
                    {
                        if (session.Phase != TOEFLExamPhase.Testing) return;
                        var index = session.CurrentSectionIndex;
                        session.FinishSection();
                        if (session.CurrentSectionIndex == index && session.Phase == TOEFLExamPhase.Testing)
                            session.AdvancePart();
                    }, 1500);
                    if (session.Phase == TOEFLExamPhase.Testing) break;
                }
                await Step($"{mode}-complete", session.CompleteWithSampleAnswersForTesting, 2500);
                if (session.Result is { } result)
                    await Step($"{mode}-report", () => window.ShowExam(new ScoreReportView(result)), 2000);
                view.Shutdown();
                window.CloseExam();
            }
        }
        catch (Exception exception)
        {
            RecordFailure("run", exception);
        }

        Log.Add(failures == 0 ? "SMOKE TEST PASSED" : $"SMOKE TEST FAILED ({failures})");
        await File.WriteAllLinesAsync(Path.Combine(OutputDirectory!, "smoke.log"), Log);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static async Task Capture(MainWindow window, string name)
    {
        if (window.Content is not UIElement root) return;
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(root);
        var pixels = await bitmap.GetPixelsAsync();
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
        stream.Seek(0);
        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        await File.WriteAllBytesAsync(Path.Combine(OutputDirectory!, name + ".png"), bytes);
    }
}
