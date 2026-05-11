using System.Diagnostics;
using System.Net;
using System.Text;
using OpenQA.Selenium;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CloudKeep.SeleniumTests;

internal sealed record SeleniumEvidence(
    string Name,
    string Validation,
    string Outcome,
    TimeSpan Duration,
    string VideoPath,
    string? Error);

internal static class SeleniumEvidenceReport
{
    private static readonly object Sync = new();
    private static readonly List<SeleniumEvidence> Evidences = [];

    static SeleniumEvidenceReport()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string ReportDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("CLOUDKEEP_REPORT_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            return Path.Combine(FindRepositoryRoot(), "TestResults", "Selenium", "Evidence");
        }
    }

    public static void Run(string name, string validation, IWebDriver driver, Action testBody)
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        Directory.CreateDirectory(ReportDirectory);
        var stopwatch = Stopwatch.StartNew();
        using var recorder = ScreenVideoRecorder.Start(name);

        try
        {
            testBody();
            stopwatch.Stop();
            recorder.Stop();
            Record(name, validation, "Aprobada", stopwatch.Elapsed, recorder.VideoPath, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            recorder.Stop();
            Record(name, validation, "Fallida", stopwatch.Elapsed, recorder.VideoPath, ex);
            throw;
        }
    }

    private static void Record(
        string name,
        string validation,
        string outcome,
        TimeSpan duration,
        string videoPath,
        Exception? error)
    {
        var evidence = new SeleniumEvidence(
            name,
            validation,
            outcome,
            duration,
            videoPath,
            error?.Message);

        lock (Sync)
        {
            Evidences.RemoveAll(item => item.Name == evidence.Name);
            Evidences.Add(evidence);
            WriteHtmlReport();
            WritePdfReport();
        }
    }

    private static void WriteHtmlReport()
    {
        var total = Evidences.Count;
        var passed = Evidences.Count(item => item.Outcome == "Aprobada");
        var failed = Evidences.Count(item => item.Outcome == "Fallida");
        var duration = TimeSpan.FromMilliseconds(Evidences.Sum(item => item.Duration.TotalMilliseconds));

        var groups = string.Join(Environment.NewLine, Evidences
            .GroupBy(item => EvidenceGroup(item.Name))
            .OrderBy(group => GroupOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var groupItems = group.OrderBy(item => item.Name, StringComparer.Ordinal).ToList();
                var groupPassed = groupItems.Count(item => item.Outcome == "Aprobada");
                var groupFailed = groupItems.Count(item => item.Outcome == "Fallida");
                var groupDuration = TimeSpan.FromMilliseconds(groupItems.Sum(item => item.Duration.TotalMilliseconds));
                var rows = string.Join(Environment.NewLine, groupItems.Select(EvidenceRowHtml));

                return $$"""
                <details class="evidence-group" {{(groupFailed > 0 ? "open" : string.Empty)}}>
                  <summary>
                    <span class="group-title">{{WebUtility.HtmlEncode(group.Key)}}</span>
                    <span class="group-meta">{{groupItems.Count}} pruebas</span>
                    <span class="pill passed">{{groupPassed}} aprobadas</span>
                    <span class="pill failed">{{groupFailed}} fallidas</span>
                    <span class="group-meta">{{groupDuration.TotalSeconds:F2}} s</span>
                  </summary>
                  <table>
                    <thead>
                      <tr><th>Prueba</th><th>Validacion</th><th>Resultado</th><th>Duracion</th></tr>
                    </thead>
                    <tbody>
                {{rows}}
                    </tbody>
                  </table>
                </details>
                """;
            }));

        var html = $$"""
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Reporte Selenium CloudKeep</title>
          <style>
            body { margin: 0; background: #f4f7fb; color: #172033; font-family: "Segoe UI", Arial, sans-serif; }
            .page { max-width: 1080px; margin: 0 auto; padding: 40px 24px; }
            .hero { background: linear-gradient(135deg, #1d4ed8, #0f172a); color: white; border-radius: 24px; padding: 32px; }
            h1 { margin: 0; font-size: 32px; }
            .muted { color: #64748b; }
            .hero .muted { color: rgba(255,255,255,.8); }
            .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin: 24px 0; }
            .metric, .section { background: white; border: 1px solid #dbe4ef; border-radius: 18px; box-shadow: 0 14px 40px rgba(15,23,42,.06); }
            .metric { padding: 20px; }
            .metric-label { color: #64748b; font-size: 12px; letter-spacing: .08em; text-transform: uppercase; }
            .metric-value { margin-top: 8px; font-size: 28px; font-weight: 800; }
            .section { padding: 24px; }
            .evidence-group { border: 1px solid #dbe4ef; border-radius: 16px; margin-top: 16px; overflow: hidden; background: #fff; }
            .evidence-group summary { cursor: pointer; display: flex; align-items: center; gap: 12px; padding: 16px 18px; background: #f8fafc; font-weight: 700; }
            .evidence-group summary:hover { background: #eef4ff; }
            .group-title { min-width: 180px; font-size: 16px; }
            .group-meta { color: #64748b; font-size: 12px; font-weight: 700; }
            table { width: 100%; border-collapse: collapse; font-size: 14px; }
            th, td { padding: 12px 14px; border-bottom: 1px solid #dbe4ef; text-align: left; vertical-align: top; }
            th { background: #f8fafc; font-size: 12px; letter-spacing: .06em; text-transform: uppercase; }
            .pill { display: inline-flex; padding: 4px 10px; border-radius: 999px; font-weight: 700; font-size: 12px; }
            .passed { background: #dcfce7; color: #15803d; }
            .failed { background: #fee2e2; color: #b91c1c; }
            .evidence-video { width: 100%; max-height: 620px; border: 1px solid #dbe4ef; border-radius: 14px; margin-top: 8px; background: #0f172a; }
            .error { margin-top: 12px; padding: 12px; background: #fff1f2; color: #991b1b; border-radius: 12px; }
          </style>
        </head>
        <body>
          <main class="page">
            <section class="hero">
              <p>CloudKeep</p>
              <h1>Reporte de Automatizacion Selenium</h1>
              <p class="muted">Generado: {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}. Aplicacion: {{WebUtility.HtmlEncode(SeleniumTestSettings.BaseUrl.ToString())}}</p>
            </section>
            <section class="grid">
              <div class="metric"><div class="metric-label">Total</div><div class="metric-value">{{total}}</div></div>
              <div class="metric"><div class="metric-label">Aprobadas</div><div class="metric-value">{{passed}}</div></div>
              <div class="metric"><div class="metric-label">Fallidas</div><div class="metric-value">{{failed}}</div></div>
              <div class="metric"><div class="metric-label">Duracion</div><div class="metric-value">{{duration.TotalSeconds:F0}} s</div></div>
            </section>
            <section class="section">
              <h2>Detalle con evidencias</h2>
        {{groups}}
            </section>
          </main>
        </body>
        </html>
        """;

        File.WriteAllText(Path.Combine(ReportDirectory, "selenium-evidence-report.html"), html);
    }

    private static void WritePdfReport()
    {
        var total = Evidences.Count;
        var passed = Evidences.Count(item => item.Outcome == "Aprobada");
        var failed = Evidences.Count(item => item.Outcome == "Fallida");
        var duration = TimeSpan.FromMilliseconds(Evidences.Sum(item => item.Duration.TotalMilliseconds));

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("Reporte de Automatizacion Selenium")
                        .FontSize(22)
                        .Bold()
                        .FontColor(Colors.Blue.Darken3);
                    column.Item().Text($"CloudKeep - {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                    column.Item().Text($"Aplicacion: {SeleniumTestSettings.BaseUrl}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        AddMetric(row, "Total", total.ToString());
                        AddMetric(row, "Aprobadas", passed.ToString());
                        AddMetric(row, "Fallidas", failed.ToString());
                        AddMetric(row, "Duracion", $"{duration.TotalSeconds:F0} s");
                    });

                    foreach (var evidence in Evidences.OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        column.Item().Element(item => ComposeEvidence(item, evidence));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Pagina ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(Path.Combine(ReportDirectory, "selenium-evidence-report.pdf"));
    }

    private static void ComposeEvidence(IContainer container, SeleniumEvidence evidence)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(title =>
                {
                    title.Item().Text(evidence.Name).Bold().FontSize(13);
                    title.Item().Text(evidence.Validation).FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(90).AlignRight().Text(evidence.Outcome)
                    .Bold()
                    .FontColor(evidence.Outcome == "Aprobada" ? Colors.Green.Darken2 : Colors.Red.Darken2);
            });

            column.Item().Text($"Duracion: {evidence.Duration.TotalSeconds:F2} s").FontColor(Colors.Grey.Darken1);

            if (!string.IsNullOrWhiteSpace(evidence.Error))
                column.Item().Background(Colors.Red.Lighten5).Padding(8).Text(evidence.Error).FontColor(Colors.Red.Darken3);

            if (File.Exists(evidence.VideoPath))
                column.Item().Text($"Video: {Path.GetRelativePath(ReportDirectory, evidence.VideoPath)}").FontColor(Colors.Blue.Darken2);
        });
    }

    private static string EvidenceRowHtml(SeleniumEvidence item)
    {
        var relativeVideo = Path.GetRelativePath(ReportDirectory, item.VideoPath).Replace('\\', '/');
        return $"""
          <tr>
            <td>{WebUtility.HtmlEncode(item.Name)}</td>
            <td>{WebUtility.HtmlEncode(item.Validation)}</td>
            <td><span class="pill {CssOutcome(item.Outcome)}">{WebUtility.HtmlEncode(item.Outcome)}</span></td>
            <td>{item.Duration.TotalSeconds:F2} s</td>
          </tr>
          <tr>
            <td colspan="4">
              <video class="evidence-video" controls preload="metadata" src="{WebUtility.HtmlEncode(relativeVideo)}">
                Tu navegador no soporta la reproduccion de video.
              </video>
              {ErrorBlock(item.Error)}
            </td>
          </tr>
        """;
    }

    private static string EvidenceGroup(string testName)
    {
        if (testName.StartsWith("Scripts_", StringComparison.OrdinalIgnoreCase))
            return "Scripts";
        if (testName.StartsWith("Destinations_", StringComparison.OrdinalIgnoreCase))
            return "Destinos";
        if (testName.StartsWith("Jobs_", StringComparison.OrdinalIgnoreCase) || testName.StartsWith("Dashboard_", StringComparison.OrdinalIgnoreCase))
            return "Trabajos";
        if (testName.StartsWith("Login", StringComparison.OrdinalIgnoreCase))
            return "Autenticacion";
        if (testName.StartsWith("Sidebar_", StringComparison.OrdinalIgnoreCase))
            return "Navegacion";
        if (testName.StartsWith("Settings_", StringComparison.OrdinalIgnoreCase))
            return "Configuracion";

        return "Otras pruebas";
    }

    private static int GroupOrder(string group) =>
        group switch
        {
            "Scripts" => 1,
            "Destinos" => 2,
            "Trabajos" => 3,
            "Autenticacion" => 4,
            "Navegacion" => 5,
            "Configuracion" => 6,
            _ => 100,
        };

    private sealed class ScreenVideoRecorder : IDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _ffmpegOutput = new();
        private bool _stopped;

        private ScreenVideoRecorder(Process process, string videoPath)
        {
            _process = process;
            VideoPath = videoPath;
        }

        public string VideoPath { get; }

        public static ScreenVideoRecorder Start(string name)
        {
            var videoDirectory = Path.Combine(ReportDirectory, "videos");
            Directory.CreateDirectory(videoDirectory);

            var videoPath = Path.Combine(videoDirectory, $"{SanitizeFileName(name)}.mp4");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FfmpegPath,
                    Arguments = $"-y -f gdigrab -framerate 15 -i desktop -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{videoPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var recorder = new ScreenVideoRecorder(process, videoPath);
            try
            {
                process.OutputDataReceived += (_, args) => recorder.AppendFfmpegOutput(args.Data);
                process.ErrorDataReceived += (_, args) => recorder.AppendFfmpegOutput(args.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Thread.Sleep(750);

                if (process.HasExited)
                    throw new InvalidOperationException($"ffmpeg no pudo iniciar la grabacion de pantalla. Salida: {recorder._ffmpegOutput}");
            }
            catch (Exception ex)
            {
                process.Dispose();
                throw new InvalidOperationException("No se pudo iniciar la grabacion de pantalla. Verifique que ffmpeg este instalado o configure CLOUDKEEP_FFMPEG_PATH.", ex);
            }

            return recorder;
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;

            try
            {
                if (!_process.HasExited)
                {
                    _process.StandardInput.WriteLine("q");
                    if (!_process.WaitForExit(5000))
                    {
                        _process.Kill(entireProcessTree: true);
                        _process.WaitForExit(5000);
                    }
                }
            }
            finally
            {
                _process.Dispose();
            }
        }

        public void Dispose() => Stop();

        private void AppendFfmpegOutput(string? line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _ffmpegOutput.AppendLine(line);
        }
    }

    private static string FfmpegPath =>
        Environment.GetEnvironmentVariable("CLOUDKEEP_FFMPEG_PATH") ?? "ffmpeg";

    private static void AddMetric(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Text(label).FontColor(Colors.Grey.Darken1).FontSize(9);
            column.Item().Text(value).Bold().FontSize(18);
        });
    }

    private static string ErrorBlock(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return string.Empty;

        return $"""<div class="error">{WebUtility.HtmlEncode(error)}</div>""";
    }

    private static string CssOutcome(string outcome) =>
        outcome == "Aprobada" ? "passed" : "failed";

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProyectoDeGrado.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
