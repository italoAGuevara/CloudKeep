using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HostedService.Entities;

namespace HostedService.Scripts;

public sealed class ScriptRunner : IScriptRunner
{
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
        var psi = CreateStartInfo(tipo, scriptDir, scriptFileName, arguments);

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
                $"No se pudo iniciar el intérprete para «{tipo}». Para .js debe existir «node» en el PATH.", ex);
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

    private static ProcessStartInfo CreateStartInfo(string tipo, string workingDirectory, string scriptFileName, string arguments)
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
                psi.FileName = "node";
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
