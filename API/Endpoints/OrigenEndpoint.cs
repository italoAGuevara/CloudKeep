using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints;

public static class OrigenEndpoint
{
    public static void MapOrigenes(this WebApplication app)
    {
        var group = app.MapGroup("/api/origenes").RequireAuthorization();

        group.MapGet("/", GetOrigenes)
            .WithName("GetOrigenes");

        group.MapGet("/{id:int}", GetOrigenById)
            .WithName("GetOrigenById");

        group.MapPost("/", CreateOrigen)
            .WithName("CreateOrigen");

        group.MapPost("/validar-ruta", ValidarRutaOrigen)
            .WithName("ValidarRutaOrigen");

        group.MapPost("/asegurar-por-ruta", AsegurarOrigenPorRuta)
            .WithName("AsegurarOrigenPorRuta");

        group.MapPut("/{id:int}", UpdateOrigen)
            .WithName("UpdateOrigen");

        group.MapDelete("/{id:int}", DeleteOrigen)
            .WithName("DeleteOrigen");
    }

    private static async Task<IResult> GetOrigenes(IOrigenService origenService)
    {
        var list = await origenService.GetAll();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetOrigenById(int id, IOrigenService origenService)
    {
        var origen = await origenService.GetById(id);
        return origen is null ? Results.NotFound() : Results.Ok(origen);
    }

    private static async Task<IResult> CreateOrigen(CreateOrigenRequest request, IOrigenService origenService)
    {
        var origen = await origenService.Create(request);
        return Results.Created($"/api/origenes/{origen.Id}", origen);
    }

    private static async Task<IResult> ValidarRutaOrigen(RutaOrigenRequest request, IOrigenService origenService)
    {
        var res = await origenService.ValidarRutaAsync(request.Ruta);
        return Results.Ok(res);
    }

    private static async Task<IResult> AsegurarOrigenPorRuta(AsegurarOrigenPorRutaRequest request, IOrigenService origenService)
    {
        var origen = await origenService.AsegurarPorRutaAsync(request.Ruta, request.FiltrosExclusiones);
        return Results.Ok(origen);
    }

    private static async Task<IResult> UpdateOrigen(int id, UpdateOrigenRequest request, IOrigenService origenService)
    {
        var origen = await origenService.Update(id, request);
        return origen is null ? Results.NotFound() : Results.Ok(origen);
    }

    private static async Task<IResult> DeleteOrigen(int id, IOrigenService origenService)
    {
        var deleted = await origenService.Delete(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
