namespace HostedService.Scripts;

public sealed class ScriptRunnerOptions
{
    public const string SectionName = "ScriptRunner";

    /// <summary>
    /// Ruta al ejecutable de Node (absoluta, o relativa al directorio base de la aplicación).
    /// Vacío: se usa <c>runtime/node/node.exe</c> (Windows) o <c>runtime/node/node</c> si existe; si no, <c>node</c> del PATH.
    /// </summary>
    public string? NodeExecutablePath { get; set; }

    /// <summary>
    /// Respaldo si no hay valor en BD (p. ej. antes de migrar). Si es &lt;= 0, se usan 2 minutos.
    /// </summary>
    public int ScriptExecutionTimeoutMinutes { get; set; }
}
