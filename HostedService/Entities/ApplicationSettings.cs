namespace HostedService.Entities;

/// <summary>Fila única (Id = 1) con parámetros globales de la aplicación.</summary>
public sealed class ApplicationSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Tiempo máximo de ejecución de scripts PRE/POST (minutos).</summary>
    public int ScriptExecutionTimeoutMinutes { get; set; } = 2;
}
