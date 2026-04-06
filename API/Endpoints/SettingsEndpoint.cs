using API.DTOs;
using API.Services.Interfaces;

namespace API.Endpoints;

public static class SettingsEndpoint
{
    public static void MapSettingsEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/script-execution-timeout", GetScriptExecutionTimeout)
            .WithName("GetScriptExecutionTimeout");

        group.MapPut("/script-execution-timeout", PutScriptExecutionTimeout)
            .WithName("PutScriptExecutionTimeout");
    }

    private static async Task<IResult> GetScriptExecutionTimeout(IApplicationSettingsService settings)
    {
        var minutes = await settings.GetScriptExecutionTimeoutMinutesAsync();
        if (minutes <= 0)
            minutes = 2;

        return Results.Ok(new ScriptExecutionTimeoutResponse(minutes));
    }

    private static async Task<IResult> PutScriptExecutionTimeout(
        UpdateScriptExecutionTimeoutRequest request,
        IApplicationSettingsService settings)
    {
        await settings.SetScriptExecutionTimeoutMinutesAsync(request.ScriptExecutionTimeoutMinutes);
        return Results.Ok(new ScriptExecutionTimeoutResponse(request.ScriptExecutionTimeoutMinutes));
    }
}
