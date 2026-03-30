using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints
{
    public static class ScriptEndpoint
    {
        public static void MapScripts(this WebApplication app)
        {
            var group = app.MapGroup("/api/scripts").RequireAuthorization();

            group.MapGet("/", GetScripts)
            .WithName("GetScripts");

            group.MapGet("/{id:int}", GetScriptById)
            .WithName("GetScriptById");

            group.MapPost("/", CreateScript)
            .WithName("CreateScript");

            group.MapPut("/{id:int}", UpdateScript)
            .WithName("UpdateScript");

            group.MapDelete("/{id:int}", DeleteScript)
            .WithName("DeleteScript");
        }

        private static async Task<IResult> GetScripts(IScriptsService scriptsService)
        {
            var list = await scriptsService.GetAll();
            return Results.Ok(list);
        }

        private static async Task<IResult> GetScriptById(int id, IScriptsService scriptsService)
        {
            var script = await scriptsService.GetById(id);
            return script is null ? Results.NotFound() : Results.Ok(script);
        }

        private static async Task<IResult> CreateScript(CreateScriptRequest request, IScriptsService scriptsService)
        {
            var script = await scriptsService.Create(request);
            return Results.Created($"/api/scripts/{script.Id}", script);
        }

        private static async Task<IResult> UpdateScript(int id, UpdateScriptRequest request, IScriptsService scriptsService)
        {
            var script = await scriptsService.Update(id, request);
            return script is null ? Results.NotFound() : Results.Ok(script);
        }

        private static async Task<IResult> DeleteScript(int id, IScriptsService scriptsService)
        {
            var deleted = await scriptsService.Delete(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
    }
}
