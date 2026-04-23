using API.Exceptions;
using API.Services.Services;
using CloudKeep.Tests.Support;
using HostedService.Backup;
using HostedService.Entities;
using HostedService.Scripts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CloudKeep.Tests.Services;

public class TrabajoEjecucionServiceTests
{
    [Fact]
    public async Task EjecutarManualAsync_throws_not_found()
    {
        using var db = new TestAppDatabase();
        var sut = new TrabajoEjecucionService(
            db.Context,
            ServiceTestHarness.CreateCredentialProtector(),
            new Mock<IDestinoToCloudCopier>().Object,
            new Mock<IScriptRunner>().Object,
            NullLogger<TrabajoEjecucionService>.Instance);
        await Assert.ThrowsAsync<NotFoundException>(() => sut.EjecutarManualAsync(404));
    }

    [Fact]
    public async Task EjecutarManualAsync_throws_conflict_when_already_processing()
    {
        using var db = new TestAppDatabase();
        var trabajoId = await SeedTrabajoAsync(db.Context, processing: true);
        var sut = new TrabajoEjecucionService(
            db.Context,
            ServiceTestHarness.CreateCredentialProtector(),
            new Mock<IDestinoToCloudCopier>().Object,
            new Mock<IScriptRunner>().Object,
            NullLogger<TrabajoEjecucionService>.Instance);
        await Assert.ThrowsAsync<ConflictException>(() => sut.EjecutarManualAsync(trabajoId));
    }

    [Fact]
    public async Task EjecutarManualAsync_completes_and_returns_copied_count()
    {
        using var db = new TestAppDatabase();
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ck_job_{Guid.NewGuid():N}")).FullName;
        await File.WriteAllTextAsync(Path.Combine(temp, "f.txt"), "x");
        var trabajoId = await SeedTrabajoAsync(db.Context, processing: false, origenRuta: temp);

        var copier = new Mock<IDestinoToCloudCopier>();
        copier
            .Setup(c => c.CopyOrigenToDestinoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Destino>(),
                It.IsAny<string>(),
                It.IsAny<CopiaArchivosFiltros?>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var scripts = new Mock<IScriptRunner>();
        var sut = new TrabajoEjecucionService(
            db.Context,
            ServiceTestHarness.CreateCredentialProtector(),
            copier.Object,
            scripts.Object,
            NullLogger<TrabajoEjecucionService>.Instance);

        var res = await sut.EjecutarManualAsync(trabajoId);
        Assert.Equal(7, res.ArchivosCopiados);
        Assert.Contains("7", res.Mensaje, StringComparison.Ordinal);

        var t = await db.Context.Trabajos.FindAsync(trabajoId);
        Assert.NotNull(t);
        Assert.False(t.Procesando);
        scripts.Verify(
            s => s.RunAsync(It.IsAny<ScriptConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        try
        {
            Directory.Delete(temp, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task EjecutarManualAsync_pre_script_failure_stops_when_configured()
    {
        using var db = new TestAppDatabase();
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ck_pre_{Guid.NewGuid():N}")).FullName;
        await File.WriteAllTextAsync(Path.Combine(temp, "f.txt"), "x");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"pre_{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptPath, "#");
        try
        {
            var trabajoId = await SeedTrabajoAsync(
                db.Context,
                processing: false,
                origenRuta: temp,
                preScriptPath: scriptPath,
                preDetenerEnFallo: true);

            var copier = new Mock<IDestinoToCloudCopier>();
            copier.Setup(c => c.CopyOrigenToDestinoAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Destino>(),
                    It.IsAny<string>(),
                    It.IsAny<CopiaArchivosFiltros?>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var scripts = new Mock<IScriptRunner>();
            scripts
                .Setup(s => s.RunAsync(It.IsAny<ScriptConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ScriptExecutionResult(1, string.Empty, "err-line"));

            var sut = new TrabajoEjecucionService(
                db.Context,
                ServiceTestHarness.CreateCredentialProtector(),
                copier.Object,
                scripts.Object,
                NullLogger<TrabajoEjecucionService>.Instance);

            await Assert.ThrowsAsync<BadRequestException>(() => sut.EjecutarManualAsync(trabajoId));

            copier.Verify(
                c => c.CopyOrigenToDestinoAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Destino>(),
                    It.IsAny<string>(),
                    It.IsAny<CopiaArchivosFiltros?>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
                Directory.Delete(temp, true);
            }
            catch
            {
            }
        }
    }

    private static async Task<int> SeedTrabajoAsync(
        AppDbContext ctx,
        bool processing,
        string? origenRuta = null,
        string? preScriptPath = null,
        bool preDetenerEnFallo = false)
    {
        var protector = ServiceTestHarness.CreateCredentialProtector();
        var ruta = origenRuta ?? Path.Combine(Path.GetTempPath(), $"ck_placeholder_{Guid.NewGuid():N}");
        ctx.Origenes.Add(new Origen { Nombre = "o", Ruta = ruta, Descripcion = "d" });
        ctx.Destinos.Add(new Destino
        {
            Nombre = "dst",
            TipoDeDestino = HostedService.DestinoTipos.S3,
            Credenciales = protector.Protect("{}"),
            BucketName = "b",
            S3Region = "us-east-1",
            CarpetaDestino = "p/"
        });
        await ctx.SaveChangesAsync();
        var o = await ctx.Origenes.SingleAsync();
        var d = await ctx.Destinos.SingleAsync();
        var link = new TrabajosOrigenDestino { OrigenId = o.Id, DestinoId = d.Id };
        ctx.TrabajosOrigenDestinos.Add(link);
        await ctx.SaveChangesAsync();

        int? scriptPreId = null;
        if (preScriptPath is not null)
        {
            var sc = new ScriptConfiguration
            {
                Nombre = "pre",
                ScriptPath = preScriptPath,
                Arguments = string.Empty,
                Tipo = ".ps1"
            };
            ctx.ScriptConfigurations.Add(sc);
            await ctx.SaveChangesAsync();
            scriptPreId = sc.Id;
        }

        var ts = new TrabajoScripts
        {
            ScriptPreId = scriptPreId,
            PreDetenerEnFallo = preDetenerEnFallo,
            ScriptPostId = null,
            PostDetenerEnFallo = false
        };
        ctx.TrabajosScripts.Add(ts);
        await ctx.SaveChangesAsync();

        var trabajo = new Trabajo
        {
            Nombre = "j",
            Descripcion = "d",
            TrabajosOrigenDestinoId = link.Id,
            TrabajosScriptsId = ts.Id,
            CronExpression = "0 * * * *",
            Procesando = processing
        };
        ctx.Trabajos.Add(trabajo);
        await ctx.SaveChangesAsync();
        return trabajo.Id;
    }
}
