using API.Audit;
using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services
{
    public class ScriptsService : IScriptsService
    {
        private readonly AppDbContext _context;
        private readonly ILogAccionesUsuarioWriter _logAcciones;

        public ScriptsService(AppDbContext context, ILogAccionesUsuarioWriter logAcciones)
        {
            _context = context;
            _logAcciones = logAcciones;
        }

        public async Task<IEnumerable<ScriptResponse>> GetAll()
        {
            var list = await _context.ScriptConfigurations
                .AsNoTracking()
                .OrderBy(s => s.Nombre)
                .ToListAsync();
            return list.Select(MapToResponse).OrderBy(x => x.Id);
        }

        public async Task<ScriptResponse?> GetById(int id)
        {
            var entity = await _context.ScriptConfigurations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

            if (entity is null)
                throw new NotFoundException($"Script por Id '{id}' no existe");

            return MapToResponse(entity);
        }

        public async Task<ScriptResponse> Create(CreateScriptRequest request)
        {
            ValidateTipo(request.Tipo);

            if (string.IsNullOrEmpty(request.Nombre)) 
            { 
                throw new BadRequestException("El campo 'Nombre' es obligatorio.");
            }

            if (!File.Exists(request.ScriptPath))
            {
                throw new BadRequestException("No se pudo encontrar la existencia fisica del script en el sistema.");
            }

            var entity = new ScriptConfiguration
            {
                Nombre = request.Nombre,
                ScriptPath = request.ScriptPath,
                Arguments = request.Arguments,
                Tipo = request.Tipo.ToLower()
            };
            _context.ScriptConfigurations.Add(entity);
            await _context.SaveChangesAsync();
            await _logAcciones.RegistrarAsync(TablasAfectadas.Script, AccionLog.Create, null, SnapshotScript(entity));
            return MapToResponse(entity);
        }

        public async Task<ScriptResponse?> Update(int id, UpdateScriptRequest request)
        {
            var entity = await _context.ScriptConfigurations.FirstOrDefaultAsync(s => s.Id == id);
            if (entity is null) return null;

            var antes = SnapshotScript(entity);

            if (request.Nombre is not null) entity.Nombre = request.Nombre;
            if (request.ScriptPath is not null) entity.ScriptPath = request.ScriptPath;
            if (request.Arguments is not null) entity.Arguments = request.Arguments;
            if (request.Tipo is not null)
            {
                ValidateTipo(request.Tipo);
                entity.Tipo = request.Tipo.ToLower();
            }

            await _context.SaveChangesAsync();
            await _logAcciones.RegistrarAsync(TablasAfectadas.Script, AccionLog.Update, antes, SnapshotScript(entity));
            return MapToResponse(entity);
        }

        public async Task<bool> Delete(int id)
        {
            var entity = await _context.ScriptConfigurations.FirstOrDefaultAsync(s => s.Id == id);
            if (entity is null) return false;

            var enUso = await _context.TrabajosScripts.AnyAsync(ts =>
                ts.ScriptPreId == id || ts.ScriptPostId == id);
            if (enUso)
                throw new ConflictException($"El script está asignado como pre o post en uno o más trabajos.");

            var antes = SnapshotScript(entity);
            _context.ScriptConfigurations.Remove(entity);
            await _context.SaveChangesAsync();
            await _logAcciones.RegistrarAsync(TablasAfectadas.Script, AccionLog.Delete, antes, null);
            return true;
        }

        private static object SnapshotScript(ScriptConfiguration s) => new
        {
            s.Id,
            s.Nombre,
            s.ScriptPath,
            s.Arguments,
            s.Tipo,
            s.CreatedOn,
            s.UpdatedOn
        };

        private static ScriptResponse MapToResponse(ScriptConfiguration s) => new(
            s.Id,
            s.Nombre,
            s.ScriptPath,
            s.Arguments,
            s.Tipo);

        private static void ValidateTipo(string tipo)
        {
            if (!string.Equals(tipo, ".ps1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tipo, ".bat", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tipo, ".js", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"Tipo de script '{tipo}' no es válido. Solo se permiten '.ps1', '.bat' o '.js'");
            }
        }
    }
}
