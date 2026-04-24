using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints;

public static class ReportesEndpoint
{
    public static void MapReportesEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/reportes").RequireAuthorization();

        group.MapGet("/ejecuciones", GetEjecucionesHistorial)
            .WithName("GetEjecucionesHistorial");

        group.MapGet("/log-acciones-usuario", GetLogAccionesUsuario)
            .WithName("GetReporteLogAccionesUsuario");
    }

    private static async Task<IResult> GetLogAccionesUsuario(
        ILogAccionesUsuarioQueryService logAcciones,
        int? limite,
        CancellationToken cancellationToken)
    {
        var list = await logAcciones.ListarAsync(limite, cancellationToken);
        return Results.Ok(new LogAccionesUsuarioListResponse(list));
    }

    private static async Task<IResult> GetEjecucionesHistorial(
        IJobExecutionReportService reportes,
        int? trabajoId,
        string? ejecutadoPor,
        DateTime? desdeUtc,
        DateTime? hastaUtc,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await reportes.GetHistorialAsync(
            trabajoId,
            ejecutadoPor,
            desdeUtc,
            hastaUtc,
            page,
            pageSize,
            cancellationToken);
        return Results.Ok(result);
    }
}
