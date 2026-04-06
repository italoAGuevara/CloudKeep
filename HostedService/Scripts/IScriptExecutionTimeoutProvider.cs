namespace HostedService.Scripts;

/// <summary>Obtiene el timeout de scripts persistido (BD). Si no hay fila o el valor es inválido, puede devolver 0 para usar fallback (appsettings).</summary>
public interface IScriptExecutionTimeoutProvider
{
    Task<int> GetScriptExecutionTimeoutMinutesAsync(CancellationToken cancellationToken = default);
}
