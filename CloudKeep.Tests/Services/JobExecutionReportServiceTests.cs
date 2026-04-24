using API.DTOs;
using API.Services.Services;
using CloudKeep.Tests.Support;
using HostedService.Entities;
using HostedService.Enums;

namespace CloudKeep.Tests.Services;

public class JobExecutionReportServiceTests
{
    [Fact]
    public async Task GetHistorialAsync_paginates_and_maps_deleted_trabajo_name()
    {
        using var db = new TestAppDatabase();
        var ctx = db.Context;
        var h1 = new HistoryBackupExecutions
        {
            TrabajoId = 99,
            StartTime = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 1, 10, 12, 0, 5, DateTimeKind.Utc),
            Status = BackupStatus.Completed,
            Trigger = JobExecutionTrigger.Manual,
            ArchivosCopiados = 2
        };
        ctx.HistoryBackupExecutions.Add(h1);
        await ctx.SaveChangesAsync();

        var sut = new JobExecutionReportService(ctx);
        var page0 = await sut.GetHistorialAsync(null, null, null, null, page: 0, pageSize: 10);
        Assert.Single(page0.Items);
        Assert.Equal(1, page0.Total);
        Assert.Equal("(trabajo eliminado)", page0.Items[0].TrabajoNombre);
        Assert.Equal("completado", page0.Items[0].Estado);
        Assert.Equal("manual", page0.Items[0].Disparo);
        Assert.Equal("Sistema", page0.Items[0].EjecutadoPor);
        Assert.NotNull(page0.Items[0].DuracionSegundos);
    }

    [Fact]
    public async Task GetHistorialAsync_in_progress_has_null_duration()
    {
        using var db = new TestAppDatabase();
        var ctx = db.Context;
        ctx.HistoryBackupExecutions.Add(new HistoryBackupExecutions
        {
            TrabajoId = 1,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            Status = BackupStatus.InProgress,
            Trigger = JobExecutionTrigger.Programada
        });
        await ctx.SaveChangesAsync();

        var sut = new JobExecutionReportService(ctx);
        var res = await sut.GetHistorialAsync(null, null, null, null, 1, 50);
        Assert.Null(res.Items[0].DuracionSegundos);
        Assert.Equal("en_progreso", res.Items[0].Estado);
        Assert.Equal("programada", res.Items[0].Disparo);
        Assert.Equal("Sistema", res.Items[0].EjecutadoPor);
    }

    [Fact]
    public async Task GetHistorialAsync_filters_by_ejecutado_por()
    {
        using var db = new TestAppDatabase();
        var ctx = db.Context;
        ctx.HistoryBackupExecutions.AddRange(
            new HistoryBackupExecutions
            {
                TrabajoId = 1,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(-4),
                Status = BackupStatus.Completed,
                Trigger = JobExecutionTrigger.Manual,
                EjecutadoPor = "admin"
            },
            new HistoryBackupExecutions
            {
                TrabajoId = 2,
                StartTime = DateTime.UtcNow.AddMinutes(-3),
                EndTime = DateTime.UtcNow.AddMinutes(-2),
                Status = BackupStatus.Completed,
                Trigger = JobExecutionTrigger.Programada,
                EjecutadoPor = "Sistema"
            });
        await ctx.SaveChangesAsync();

        var sut = new JobExecutionReportService(ctx);
        var res = await sut.GetHistorialAsync(null, "admin", null, null, 1, 50);
        Assert.Single(res.Items);
        Assert.Equal("admin", res.Items[0].EjecutadoPor);
    }
}
