using API.Endpoints;
using API.Middleware;
using API.Platform;
using API.Services.Interfaces;
using HostedService;
using HostedService.Backup;
using HostedService.Scripts;
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
using API.Services.Scheduling;
using API.Services.Services;
using HostedService.Scheduling;


string _cors = "all";

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

builder.Services.AddSingleton<ICronMinutoEjecucionTracker, CronMinutoEjecucionTracker>();
builder.Services.AddScoped<ITrabajoCronTickHandler, TrabajoCronTickHandler>();

builder.Services.AddTransient<ILogin, LoginService>();
builder.Services.AddScoped<IScriptsService, ScriptsService>();
builder.Services.AddScoped<IOrigenService, OrigenService>();
builder.Services.AddScoped<IDestinoService, DestinoService>();
builder.Services.AddScoped<ITrabajoService, TrabajoService>();
builder.Services.AddScoped<ITrabajoEjecucionService, TrabajoEjecucionService>();
builder.Services.AddScoped<IJobExecutionReportService, JobExecutionReportService>();
builder.Services.AddScoped<IDestinoToCloudCopier, DestinoToCloudCopier>();
builder.Services.Configure<ScriptRunnerOptions>(builder.Configuration.GetSection(ScriptRunnerOptions.SectionName));
builder.Services.AddScoped<ApplicationSettingsService>();
builder.Services.AddScoped<IApplicationSettingsService>(sp => sp.GetRequiredService<ApplicationSettingsService>());
builder.Services.AddScoped<IScriptExecutionTimeoutProvider>(sp => sp.GetRequiredService<ApplicationSettingsService>());
builder.Services.AddScoped<IScriptRunner, ScriptRunner>();

var app = builder.Build();

var angularBrowserConfigured = app.Configuration["Spa:BrowserPath"]?.Trim();
if (string.IsNullOrEmpty(angularBrowserConfigured))
    throw new InvalidOperationException("Configure 'Spa:BrowserPath' in appsettings.json (absolute path or relative to the API content root).");

var angularPath = Path.GetFullPath(
    Path.IsPathRooted(angularBrowserConfigured)
        ? angularBrowserConfigured
        : Path.Combine(app.Environment.ContentRootPath, angularBrowserConfigured));

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



app.UseMiddleware<ResponseWrapperMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // Accessible at /scalar/v1
}

app.MapAuthEndpoint();
app.MapSettingsEndpoint();
app.MapReportesEndpoint();
app.MapScripts();
app.MapOrigenes();
app.MapDestinos();
app.MapTrabajos();
app.MapLogAccionesUsuario();

// RETURN index.html for any non-API route to allow Angular routing to work
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(angularPath)
});

try
{
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    var trayLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WindowsTray");
    WindowsTrayHost.Start(lifetime, app.Configuration, trayLogger);

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