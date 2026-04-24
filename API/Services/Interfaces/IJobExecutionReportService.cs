using API.DTOs;

namespace API.Services.Interfaces;

public interface IJobExecutionReportService
{
    Task<EjecucionHistorialListResponse> GetHistorialAsync(
        int? trabajoId,
        string? ejecutadoPor,
        DateTime? desdeUtc,
        DateTime? hastaUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
