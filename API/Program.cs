using API.Endpoints;
using API.Middleware;
using API.Services.Interfaces;
using HostedService;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Reflection;
using System.Text;
using API.Seeding;
using API.Services.Services;


string _cors = "all";

var angularPath = Path.Combine(Directory.GetCurrentDirectory(), "C:\\Users\\italo\\Documents\\proyecto-grado\\ProyectoDeGrado\\Interfaz\\dist\\Interfaz\\browser");

var builder = WebApplication.CreateSlimBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()    
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "tu-app.com",
            ValidAudience = "tu-app.com",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("Tu_Llave_Super_Secreta_De_Al_Menos_32_Chars!"))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILogAccionesUsuarioWriter, LogAccionesUsuarioWriter>();
builder.Services.AddScoped<ILogAccionesUsuarioQueryService, LogAccionesUsuarioQueryService>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IDestinoCredentialProtector, DestinoCredentialProtector>();

//builder.Services.ConfigureHttpJsonOptions(options =>
//{
//    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
//});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

builder.Services.AddInfrastructurePersistence();


builder.Services.AddCors(options =>
{
    options.AddPolicy(_cors, builder =>
      builder.SetIsOriginAllowed(origin =>
      {
          var uri = new Uri(origin);
          return uri.Host == "localhost" ||
                  uri.Host == "localhost:4200";
      })
    .AllowAnyMethod()
    .AllowAnyHeader());
});


builder.Services.AddHostedService<Robot>();

builder.Services.AddTransient<ILogin, LoginService>();
builder.Services.AddScoped<IScriptsService, ScriptsService>();
builder.Services.AddScoped<IOrigenService, OrigenService>();
builder.Services.AddScoped<IDestinoService, DestinoService>();
builder.Services.AddScoped<ITrabajoService, TrabajoService>();
builder.Services.AddScoped<ITrabajoEjecucionService, TrabajoEjecucionService>();

var app = builder.Build();

// Asegurar usuario único y datos de ejemplo (orígenes, scripts, jobs)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.EnsureSeedData();
    var destinoProtector = scope.ServiceProvider.GetRequiredService<IDestinoCredentialProtector>();
    TrabajoDemoSeed.EnsureDemoTrabajo(db, destinoProtector);
}

app.UseCors(_cors);

app.UseAuthentication();
app.UseAuthorization();

app.UseSerilogRequestLogging();

// LOAD STATIC FILES
app.UseDefaultFiles(); // Busca index.html por defecto
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(angularPath),
    RequestPath = ""
});

// RETURN index.html for any non-API route to allow Angular routing to work
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(angularPath)
});

app.UseMiddleware<ResponseWrapperMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // Accessible at /scalar/v1
}

app.MapAuthEndpoint();
app.MapScripts();
app.MapOrigenes();
app.MapDestinos();
app.MapTrabajos();
app.MapLogAccionesUsuario();

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down");
    Log.CloseAndFlush();
}