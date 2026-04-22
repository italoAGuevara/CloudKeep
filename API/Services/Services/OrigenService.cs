using System.IO;
using API.Audit;
using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services;

public class OrigenService : IOrigenService
{
    private readonly AppDbContext _context;
    private readonly ILogAccionesUsuarioWriter _logAcciones;

    public OrigenService(AppDbContext context, ILogAccionesUsuarioWriter logAcciones)
    {
        _context = context;
        _logAcciones = logAcciones;
    }

    public async Task<IEnumerable<OrigenResponse>> GetAll()
    {
        var list = await _context.Origenes
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .ToListAsync();
        return list.Select(MapToResponse).OrderBy(x => x.Id);
    }

    public async Task<OrigenResponse?> GetById(int id)
    {
        var entity = await _context.Origenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);

        if (entity is null)
            throw new NotFoundException($"Origen con Id '{id}' no existe");

        return MapToResponse(entity);
    }

    public async Task<OrigenResponse> Create(CreateOrigenRequest request)
    {
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.Ruta, nameof(request.Ruta));

        var nombre = request.Nombre.Trim();
        await EnsureNombreUnicoAsync(nombre, excludeId: null);

        var entity = new Origen
        {
            Nombre = nombre,
            Ruta = request.Ruta.Trim(),
            Descripcion = request.Descripcion.Trim()
        };
        _context.Origenes.Add(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Origen, AccionLog.Create, null, SnapshotOrigen(entity));
        return MapToResponse(entity);
    }

    public async Task<OrigenResponse?> Update(int id, UpdateOrigenRequest request)
    {
        var entity = await _context.Origenes.FirstOrDefaultAsync(o => o.Id == id);
        if (entity is null) return null;

        var antes = SnapshotOrigen(entity);

        if (request.Nombre is not null)
        {
            ValidateRequired(request.Nombre, nameof(request.Nombre));
            var nombre = request.Nombre.Trim();
            await EnsureNombreUnicoAsync(nombre, excludeId: id);
            entity.Nombre = nombre;
        }

        if (request.Ruta is not null)
        {
            ValidateRequired(request.Ruta, nameof(request.Ruta));
            entity.Ruta = request.Ruta.Trim();
        }

        if (request.Descripcion is not null)
            entity.Descripcion = request.Descripcion.Trim();

        entity.FechaModificacion = DateTime.UtcNow;
        entity.TamanoMaximo = request.TamanoMaximo;
        entity.FiltrosExclusiones = request.FiltrosExclusiones;

        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Origen, AccionLog.Update, antes, SnapshotOrigen(entity));
        return MapToResponse(entity);
    }

    public async Task<bool> Delete(int id)
    {
        var entity = await _context.Origenes.FirstOrDefaultAsync(o => o.Id == id);
        if (entity is null) return false;
        var antes = SnapshotOrigen(entity);
        _context.Origenes.Remove(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Origen, AccionLog.Delete, antes, null);
        return true;
    }

    public Task<RutaValidaResponse> ValidarRutaAsync(string ruta)
    {
        var normalized = NormalizeAndEnsureDirectoryExists(ruta);
        return Task.FromResult(new RutaValidaResponse(normalized));
    }

    public async Task<OrigenResponse> AsegurarPorRutaAsync(string ruta, string? filtrosExclusiones = null)
    {
        var normalized = NormalizeAndEnsureDirectoryExists(ruta);
        var normalizedFiltros = NormalizeFiltrosExclusiones(filtrosExclusiones);
        var existing = await FindByRutaCaseInsensitiveAsync(normalized);
        if (existing is not null)
        {
            if (normalizedFiltros is not null
                && !string.Equals(existing.FiltrosExclusiones, normalizedFiltros, StringComparison.Ordinal))
            {
                var antes = SnapshotOrigen(existing);
                existing.FiltrosExclusiones = normalizedFiltros;
                existing.FechaModificacion = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _logAcciones.RegistrarAsync(
                    TablasAfectadas.Origen,
                    AccionLog.Update,
                    antes,
                    SnapshotOrigen(existing));
            }
            return MapToResponse(existing);
        }

        var nombre = await BuildUniqueNombreFromPathAsync(normalized);
        var entity = new Origen
        {
            Nombre = nombre,
            Ruta = normalized,
            Descripcion = $"Respaldo desde carpeta local.",
            TamanoMaximo = string.Empty,
            FiltrosExclusiones = normalizedFiltros ?? string.Empty
        };
        _context.Origenes.Add(entity);
        await _context.SaveChangesAsync();
        await _logAcciones.RegistrarAsync(TablasAfectadas.Origen, AccionLog.Create, null, SnapshotOrigen(entity));
        return MapToResponse(entity);
    }

    private static string NormalizeAndEnsureDirectoryExists(string rutaRaw)
    {
        if (string.IsNullOrWhiteSpace(rutaRaw))
            throw new BadRequestException("ruta es obligatoria.");

        var trimmed = rutaRaw.Trim();
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new BadRequestException("La ruta no es válida.");
        }

        if (!Directory.Exists(fullPath))
            throw new BadRequestException(
                "La carpeta no existe en el equipo donde se ejecuta la API. Comprueba la ruta en ese servidor o máquina.");

        return fullPath;
    }

    private async Task<Origen?> FindByRutaCaseInsensitiveAsync(string normalizedFullPath)
    {
        var list = await _context.Origenes.ToListAsync();
        return list.FirstOrDefault(o =>
            string.Equals(o.Ruta.Trim(), normalizedFullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeFiltrosExclusiones(string? raw)
    {
        if (raw is null)
            return null;

        return string.Join(
            ";",
            raw.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0));
    }

    private async Task<string> BuildUniqueNombreFromPathAsync(string fullPath)
    {
        string name;
        try
        {
            name = new DirectoryInfo(fullPath).Name;
        }
        catch
        {
            name = "Carpeta";
        }

        if (string.IsNullOrWhiteSpace(name))
            name = "Raíz";

        var baseName = $"Respaldo · {name}";
        var candidate = baseName;
        var i = 2;
        while (await _context.Origenes.AnyAsync(o => o.Nombre == candidate))
        {
            candidate = $"{baseName} ({i})";
            i++;
        }

        return candidate;
    }

    private static object SnapshotOrigen(Origen o) => new
    {
        o.Id,
        o.Nombre,
        o.Ruta,
        o.Descripcion,
        o.TamanoMaximo,
        o.FiltrosExclusiones,
        o.FechaCreacion,
        o.FechaModificacion
    };

    private static OrigenResponse MapToResponse(Origen o) => new(
        o.Id,
        o.Nombre,
        o.Ruta,
        o.Descripcion,
        o.TamanoMaximo,
        o.FiltrosExclusiones,
        o.FechaCreacion,
        o.FechaModificacion
        );

    private async Task EnsureNombreUnicoAsync(string nombre, int? excludeId)
    {
        var exists = excludeId is null
            ? await _context.Origenes.AnyAsync(o => o.Nombre == nombre)
            : await _context.Origenes.AnyAsync(o => o.Nombre == nombre && o.Id != excludeId);

        if (exists)
            throw new ConflictException($"Ya existe un origen con el nombre '{nombre}'.");
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} es obligatorio.");
    }
}
