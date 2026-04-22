using API.DTOs;

namespace API.Services.Interfaces;

public interface IOrigenService
{
    Task<IEnumerable<OrigenResponse>> GetAll();
    Task<OrigenResponse?> GetById(int id);
    Task<OrigenResponse> Create(CreateOrigenRequest request);
    Task<OrigenResponse?> Update(int id, UpdateOrigenRequest request);
    Task<bool> Delete(int id);

    /// <summary>Comprueba que la ruta sea una carpeta existente en el servidor (donde corre la API).</summary>
    Task<RutaValidaResponse> ValidarRutaAsync(string ruta);

    /// <summary>Obtiene un origen por ruta (coincidencia sin distinguir mayúsculas) o lo crea si no existe.</summary>
    Task<OrigenResponse> AsegurarPorRutaAsync(string ruta, string? filtrosExclusiones = null);
}
