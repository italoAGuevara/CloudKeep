using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints;

public static class TrabajoEndpoint
{
    public static void MapTrabajos(this WebApplication app)
    {
        var group = app.MapGroup("/api/trabajos").RequireAuthorization();

        group.MapGet("/", GetTrabajos).WithName("GetTrabajos");
        group.MapGet("/{id:int}", GetTrabajoById).WithName("GetTrabajoById");
        group.MapPost("/", CreateTrabajo).WithName("CreateTrabajo");
        group.MapPut("/{id:int}", UpdateTrabajo).WithName("UpdateTrabajo");
        group.MapDelete("/{id:int}", DeleteTrabajo).WithName("DeleteTrabajo");
    }

    private static async Task<IResult> GetTrabajos(ITrabajoService trabajoService)
    {
        var list = await trabajoService.GetAll();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetTrabajoById(int id, ITrabajoService trabajoService)
    {
        var trabajo = await trabajoService.GetById(id);
        return trabajo is null ? Results.NotFound() : Results.Ok(trabajo);
    }

    private static async Task<IResult> CreateTrabajo(CreateTrabajoRequest request, ITrabajoService trabajoService)
    {
        var trabajo = await trabajoService.Create(request);
        return Results.Created($"/api/trabajos/{trabajo.Id}", trabajo);
    }

    private static async Task<IResult> UpdateTrabajo(int id, UpdateTrabajoRequest request, ITrabajoService trabajoService)
    {
        var trabajo = await trabajoService.Update(id, request);
        return trabajo is null ? Results.NotFound() : Results.Ok(trabajo);
    }

    private static async Task<IResult> DeleteTrabajo(int id, ITrabajoService trabajoService)
    {
        var deleted = await trabajoService.Delete(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
