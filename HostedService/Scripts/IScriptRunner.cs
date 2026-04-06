using HostedService.Entities;

namespace HostedService.Scripts;

public interface IScriptRunner
{
    /// <summary>Ejecuta un script .bat, .cmd, .ps1 o .js (este último vía Node.js en PATH).</summary>
    Task<ScriptExecutionResult> RunAsync(ScriptConfiguration script, CancellationToken cancellationToken = default);
}

public sealed record ScriptExecutionResult(int ExitCode, string StandardOutput, string StandardError);
