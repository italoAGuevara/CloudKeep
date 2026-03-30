using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints;

public static class DestinoEndpoint
{
    public static void MapDestinos(this WebApplication app)
    {
        var group = app.MapGroup("/api/destinos").RequireAuthorization();

        group.MapGet("/", GetDestinos)
            .WithName("GetDestinos");

        group.MapGet("/{id:int}", GetDestinoById)
            .WithName("GetDestinoById");

        group.MapPost("/", CreateDestino)
            .WithName("CreateDestino");

        group.MapPut("/{id:int}", UpdateDestino)
            .WithName("UpdateDestino");

        group.MapDelete("/{id:int}", DeleteDestino)
            .WithName("DeleteDestino");
    }

    private static async Task<IResult> GetDestinos(IDestinoService destinoService)
    {
        var list = await destinoService.GetAll();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetDestinoById(int id, IDestinoService destinoService)
    {
        var destino = await destinoService.GetById(id);
        return destino is null ? Results.NotFound() : Results.Ok(destino);
    }

    private static async Task<IResult> CreateDestino(CreateDestinoRequest request, IDestinoService destinoService)
    {
        var destino = await destinoService.Create(request);
        return Results.Created($"/api/destinos/{destino.Id}", destino);
    }

    private static async Task<IResult> UpdateDestino(int id, UpdateDestinoRequest request, IDestinoService destinoService)
    {
        var destino = await destinoService.Update(id, request);
        return destino is null ? Results.NotFound() : Results.Ok(destino);
    }

    private static async Task<IResult> DeleteDestino(int id, IDestinoService destinoService)
    {
        var deleted = await destinoService.Delete(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
