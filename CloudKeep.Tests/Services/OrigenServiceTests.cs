using API.DTOs;
using API.Exceptions;
using API.Services.Services;
using CloudKeep.Tests.Support;
using HostedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudKeep.Tests.Services;

public class OrigenServiceTests
{
    [Fact]
    public async Task GetById_throws_not_found()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetById(404));
    }

    [Fact]
    public async Task Create_persists_and_returns_response()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        var req = new CreateOrigenRequest("Origen A", "C:\\data", "desc", "", "");

        var res = await sut.Create(req);

        Assert.True(res.Id > 0);
        Assert.Equal("Origen A", res.Nombre);
        Assert.Equal(1, await db.Context.Origenes.CountAsync());
    }

    [Fact]
    public async Task Create_throws_conflict_on_duplicate_nombre()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        await sut.Create(new CreateOrigenRequest("Dup", "C:\\a", "d", "", ""));
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.Create(new CreateOrigenRequest("Dup", "C:\\b", "d", "", "")));
    }

    [Fact]
    public async Task ValidarRutaAsync_normalizes_existing_temp_directory()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ck_orig_{Guid.NewGuid():N}")).FullName;
        try
        {
            var res = await sut.ValidarRutaAsync(temp);
            Assert.Equal(Path.GetFullPath(temp), res.RutaNormalizada, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ValidarRutaAsync_throws_when_directory_missing()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");
        await Assert.ThrowsAsync<BadRequestException>(() => sut.ValidarRutaAsync(missing));
    }

    [Fact]
    public async Task AsegurarPorRutaAsync_creates_origen_with_exclusiones()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ck_orig_{Guid.NewGuid():N}")).FullName;
        try
        {
            var res = await sut.AsegurarPorRutaAsync(temp, ".tmp, logs; cache");
            Assert.Equal(".tmp;logs;cache", res.FiltrosExclusiones);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task AsegurarPorRutaAsync_updates_exclusiones_when_origen_exists()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ck_orig_{Guid.NewGuid():N}")).FullName;
        try
        {
            var first = await sut.AsegurarPorRutaAsync(temp, ".tmp");
            var second = await sut.AsegurarPorRutaAsync(temp, ".tmp;logs");
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(".tmp;logs", second.FiltrosExclusiones);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Delete_returns_false_when_missing()
    {
        using var db = new TestAppDatabase();
        var sut = new OrigenService(db.Context, ServiceTestHarness.CreateLogMock().Object);
        Assert.False(await sut.Delete(999));
    }
}
