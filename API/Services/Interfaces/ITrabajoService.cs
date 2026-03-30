using API.DTOs;

namespace API.Services.Interfaces;

public interface ITrabajoService
{
    Task<IEnumerable<TrabajoResponse>> GetAll();
    Task<TrabajoResponse?> GetById(int id);
    Task<TrabajoResponse> Create(CreateTrabajoRequest request);
    Task<TrabajoResponse?> Update(int id, UpdateTrabajoRequest request);
    Task<bool> Delete(int id);
}
