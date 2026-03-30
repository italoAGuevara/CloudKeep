using API.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Services.Interfaces
{
    public interface IScriptsService
    {
        Task<IEnumerable<ScriptResponse>> GetAll();
        Task<ScriptResponse?> GetById(int id);
        Task<ScriptResponse> Create(CreateScriptRequest request);
        Task<ScriptResponse?> Update(int id, UpdateScriptRequest request);
        Task<bool> Delete(int id);
    }
}
