using API.DTOs;
using API.Exceptions;
using API.Services.Interfaces;
using HostedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Services.Services
{
    public class ScriptsService : IScriptsService
    {
        private readonly AppDbContext _context;

        public ScriptsService(AppDbContext context) => _context = context;

        public async Task<IEnumerable<ScriptResponse>> GetAll()
        {
            var list = await _context.ScriptConfigurations
                .AsNoTracking()
                .OrderBy(s => s.Name)
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
            var entity = new ScriptConfiguration
            {
                Name = request.Name,
                ScriptPath = request.ScriptPath,
                Arguments = request.Arguments,
                Tipo = request.Tipo.ToLower()
            };
            _context.ScriptConfigurations.Add(entity);
            await _context.SaveChangesAsync();
            return MapToResponse(entity);
        }

        public async Task<ScriptResponse?> Update(int id, UpdateScriptRequest request)
        {
            var entity = await _context.ScriptConfigurations.FirstOrDefaultAsync(s => s.Id == id);
            if (entity is null) return null;

            if (request.Name is not null) entity.Name = request.Name;
            if (request.ScriptPath is not null) entity.ScriptPath = request.ScriptPath;
            if (request.Arguments is not null) entity.Arguments = request.Arguments;
            if (request.Tipo is not null)
            {
                ValidateTipo(request.Tipo);
                entity.Tipo = request.Tipo.ToLower();
            }

            await _context.SaveChangesAsync();
            return MapToResponse(entity);
        }

        public async Task<bool> Delete(int id)
        {
            var entity = await _context.ScriptConfigurations.FirstOrDefaultAsync(s => s.Id == id);
            if (entity is null) return false;
            _context.ScriptConfigurations.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        private static ScriptResponse MapToResponse(ScriptConfiguration s) => new(
            s.Id,
            s.Name,
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
