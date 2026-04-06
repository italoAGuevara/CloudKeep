using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using HostedService.Scripts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services;

public sealed class ApplicationSettingsService : IApplicationSettingsService, IScriptExecutionTimeoutProvider
{
    public const int MinScriptTimeoutMinutes = 1;
    public const int MaxScriptTimeoutMinutes = 24 * 60;

    private readonly AppDbContext _db;

    public ApplicationSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetScriptExecutionTimeoutMinutesAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.ApplicationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ApplicationSettings.SingletonId, cancellationToken);
        if (row is null)
            return 0;

        var m = row.ScriptExecutionTimeoutMinutes;
        if (m < MinScriptTimeoutMinutes)
            return 0;

        return Math.Clamp(m, MinScriptTimeoutMinutes, MaxScriptTimeoutMinutes);
    }

    public async Task SetScriptExecutionTimeoutMinutesAsync(int minutes, CancellationToken cancellationToken = default)
    {
        if (minutes < MinScriptTimeoutMinutes || minutes > MaxScriptTimeoutMinutes)
            throw new BadRequestException(
                $"El tiempo de espera de scripts debe estar entre {MinScriptTimeoutMinutes} y {MaxScriptTimeoutMinutes} minutos.");

        var row = await _db.ApplicationSettings
            .FirstOrDefaultAsync(s => s.Id == ApplicationSettings.SingletonId, cancellationToken);
        if (row is null)
        {
            _db.ApplicationSettings.Add(new ApplicationSettings
            {
                Id = ApplicationSettings.SingletonId,
                ScriptExecutionTimeoutMinutes = minutes
            });
        }
        else
        {
            row.ScriptExecutionTimeoutMinutes = minutes;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
