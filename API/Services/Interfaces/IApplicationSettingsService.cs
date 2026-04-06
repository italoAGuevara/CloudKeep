namespace API.Services.Interfaces;

public interface IApplicationSettingsService
{
    Task<int> GetScriptExecutionTimeoutMinutesAsync(CancellationToken cancellationToken = default);

    Task SetScriptExecutionTimeoutMinutesAsync(int minutes, CancellationToken cancellationToken = default);
}
