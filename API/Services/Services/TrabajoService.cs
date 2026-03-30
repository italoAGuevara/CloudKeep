using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services;

public class TrabajoService : ITrabajoService
{
    private readonly AppDbContext _context;

    public TrabajoService(AppDbContext context) => _context = context;

    public async Task<IEnumerable<TrabajoResponse>> GetAll()
    {
        var list = await _context.Trabajos
            .AsNoTracking()
            .Include(t => t.TrabajosOrigenDestino)
            .OrderBy(t => t.Nombre)
            .ToListAsync();
        return list.Select(MapToResponse).OrderBy(x => x.Id);
    }

    public async Task<TrabajoResponse?> GetById(int id)
    {
        var entity = await _context.Trabajos
            .AsNoTracking()
            .Include(t => t.TrabajosOrigenDestino)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entity is null)
            throw new NotFoundException($"Trabajo con Id '{id}' no existe");

        return MapToResponse(entity);
    }

    public async Task<TrabajoResponse> Create(CreateTrabajoRequest request)
    {
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        ValidateRequired(request.CronExpression, nameof(request.CronExpression));

        await EnsureOrigenExistsAsync(request.OrigenId);
        await EnsureDestinoExistsAsync(request.DestinoId);

        var linkId = await GetOrCreateTrabajosOrigenDestinoIdAsync(request.OrigenId, request.DestinoId);

        var entity = new Trabajo
        {
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion.Trim(),
            TrabajosOrigenDestinoId = linkId,
            CronExpression = request.CronExpression.Trim(),
            Activo = request.Activo ?? true
        };
        _context.Trabajos.Add(entity);
        await _context.SaveChangesAsync();

        await _context.Entry(entity).Reference(t => t.TrabajosOrigenDestino).LoadAsync();
        return MapToResponse(entity);
    }

    public async Task<TrabajoResponse?> Update(int id, UpdateTrabajoRequest request)
    {
        var entity = await _context.Trabajos
            .Include(t => t.TrabajosOrigenDestino)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return null;

        if (request.Nombre is not null)
        {
            ValidateRequired(request.Nombre, nameof(request.Nombre));
            entity.Nombre = request.Nombre.Trim();
        }

        if (request.Descripcion is not null)
            entity.Descripcion = request.Descripcion.Trim();

        if (request.CronExpression is not null)
        {
            ValidateRequired(request.CronExpression, nameof(request.CronExpression));
            entity.CronExpression = request.CronExpression.Trim();
        }

        if (request.Activo is not null)
            entity.Activo = request.Activo.Value;

        if (request.Procesando is not null)
            entity.Procesando = request.Procesando.Value;

        if (request.EstatusPrevio is not null)
            entity.EstatusPrevio = request.EstatusPrevio.Trim();

        var changePair = request.OrigenId is not null || request.DestinoId is not null;
        if (changePair)
        {
            if (request.OrigenId is null || request.DestinoId is null)
                throw new BadRequestException("origenId y destinoId deben enviarse juntos para cambiar el vínculo.");

            await EnsureOrigenExistsAsync(request.OrigenId.Value);
            await EnsureDestinoExistsAsync(request.DestinoId.Value);
            entity.TrabajosOrigenDestinoId = await GetOrCreateTrabajosOrigenDestinoIdAsync(
                request.OrigenId.Value,
                request.DestinoId.Value);
            await _context.Entry(entity).Reference(t => t.TrabajosOrigenDestino).LoadAsync();
        }

        entity.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> Delete(int id)
    {
        var entity = await _context.Trabajos.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return false;
        _context.Trabajos.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    private static TrabajoResponse MapToResponse(Trabajo t)
    {
        var p = t.TrabajosOrigenDestino;
        return new TrabajoResponse(
            t.Id,
            t.Nombre,
            t.Descripcion,
            t.TrabajosOrigenDestinoId,
            p.OrigenId,
            p.DestinoId,
            t.CronExpression,
            t.Activo,
            t.Procesando,
            t.EstatusPrevio,
            t.FechaCreacion,
            t.FechaModificacion
        );
    }

    private async Task<int> GetOrCreateTrabajosOrigenDestinoIdAsync(int origenId, int destinoId)
    {
        var existing = await _context.TrabajosOrigenDestinos
            .FirstOrDefaultAsync(x => x.OrigenId == origenId && x.DestinoId == destinoId);
        if (existing is not null)
            return existing.Id;

        var link = new TrabajosOrigenDestino { OrigenId = origenId, DestinoId = destinoId };
        _context.TrabajosOrigenDestinos.Add(link);
        await _context.SaveChangesAsync();
        return link.Id;
    }

    private async Task EnsureOrigenExistsAsync(int id)
    {
        if (!await _context.Origenes.AnyAsync(o => o.Id == id))
            throw new BadRequestException($"No existe un origen con Id '{id}'.");
    }

    private async Task EnsureDestinoExistsAsync(int id)
    {
        if (!await _context.Destinos.AnyAsync(d => d.Id == id))
            throw new BadRequestException($"No existe un destino con Id '{id}'.");
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} es obligatorio.");
    }
}
