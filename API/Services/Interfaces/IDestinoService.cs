using API.DTOs;

namespace API.Services.Interfaces;

public interface IDestinoService
{
    Task<IEnumerable<DestinoResponse>> GetAll();
    Task<DestinoResponse?> GetById(int id);
    Task<DestinoResponse> Create(CreateDestinoRequest request);
    Task<DestinoResponse?> Update(int id, UpdateDestinoRequest request);
    Task<bool> Delete(int id);
}
