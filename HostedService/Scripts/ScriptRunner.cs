using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HostedService.Entities;
using Microsoft.Extensions.Options;

namespace HostedService.Scripts;

public sealed class ScriptRunner : IScriptRunner
{
    private readonly ScriptRunnerOptions _options;

    public ScriptRunner(IOptions<ScriptRunnerOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ScriptExecutionResult> RunAsync(ScriptConfiguration script, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        var fullPath = Path.GetFullPath(script.ScriptPath.Trim());
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"No se encontró el script: «{fullPath}».", fullPath);

        var tipo = NormalizeTipo(script.Tipo, fullPath);
        var arguments = script.Arguments?.Trim() ?? "";

        var scriptDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(scriptDir))
            scriptDir = Environment.CurrentDirectory;

        var scriptFileName = Path.GetFileName(fullPath);
        var psi = CreateStartInfo(tipo, scriptDir, scriptFileName, arguments, ResolveNodeExecutable());

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is FileNotFoundException or Win32Exception)
        {
            throw new InvalidOperationException(
                DescribirFalloAlIniciarProceso(tipo), ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ScriptExecutionResult(
            process.ExitCode,
            stdout.ToString().TrimEnd(),
            stderr.ToString().TrimEnd());
    }

    private static string NormalizeTipo(string tipo, string fullPath)
    {
        if (!string.IsNullOrWhiteSpace(tipo))
            return tipo.Trim().ToLowerInvariant();

        return Path.GetExtension(fullPath).ToLowerInvariant();
    }

    private string ResolveNodeExecutable()
    {
        var baseDir = AppContext.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(_options.NodeExecutablePath))
        {
            var raw = _options.NodeExecutablePath.Trim();
            var candidate = Path.IsPathRooted(raw)
                ? raw
                : Path.GetFullPath(Path.Combine(baseDir, raw));
            if (File.Exists(candidate))
                return candidate;
        }

        var bundled = Path.Combine(
            baseDir,
            "runtime",
            "node",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
        if (File.Exists(bundled))
            return bundled;

        return "node";
    }

    private string DescribirFalloAlIniciarProceso(string tipo)
    {
        if (!string.Equals(tipo, ".js", StringComparison.OrdinalIgnoreCase))
            return $"No se pudo iniciar el intérprete para «{tipo}».";

        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(
            baseDir,
            "runtime",
            "node",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
        var probado = ResolveNodeExecutable();
        return
            "No se pudo iniciar Node.js. Coloca el binario en «runtime/node/» junto a la API, define ScriptRunner:NodeExecutablePath en appsettings, o instala Node en el PATH. "
            + $"Se probó: «{probado}» (carpeta embebida esperada: «{bundled}»).";
    }

    private static ProcessStartInfo CreateStartInfo(
        string tipo,
        string workingDirectory,
        string scriptFileName,
        string arguments,
        string nodeExecutable)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var quotedName = $"\"{scriptFileName}\"";

        switch (tipo)
        {
            case ".bat":
            case ".cmd":
                psi.FileName = "cmd.exe";
                psi.Arguments = string.IsNullOrEmpty(arguments)
                    ? $"/c {quotedName}"
                    : $"/c {quotedName} {arguments}";
                return psi;

            case ".ps1":
                psi.FileName = "powershell.exe";
                psi.Arguments = string.IsNullOrEmpty(arguments)
                    ? $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File {quotedName}"
                    : $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File {quotedName} {arguments}";
                return psi;

            case ".js":
                psi.FileName = nodeExecutable;
                psi.Arguments = string.IsNullOrEmpty(arguments)
                    ? quotedName
                    : $"{quotedName} {arguments}";
                return psi;

            default:
                throw new NotSupportedException(
                    $"Tipo de script no soportado: «{tipo}». Solo se admiten .bat, .cmd, .ps1 y .js.");
        }
    }
}
