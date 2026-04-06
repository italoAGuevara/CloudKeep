using HostedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trabajo> Trabajos => Set<Trabajo>();
    public DbSet<TrabajosOrigenDestino> TrabajosOrigenDestinos => Set<TrabajosOrigenDestino>();
    public DbSet<TrabajoScripts> TrabajosScripts => Set<TrabajoScripts>();
    public DbSet<User> Users => Set<User>();
    //public DbSet<FileMetadata> FileMetadatas => Set<FileMetadata>();
    public DbSet<HistoryBackupExecutions> HistoryBackupExecutions => Set<HistoryBackupExecutions>();
    public DbSet<Origen> Origenes => Set<Origen>();
    public DbSet<Destino> Destinos => Set<Destino>();
    public DbSet<ScriptConfiguration> ScriptConfigurations => Set<ScriptConfiguration>();
    //public DbSet<StorageProvider> StorageProviders => Set<StorageProvider>();
    //public DbSet<UserStorages> UserStorages => Set<UserStorages>();
    public DbSet<LogAccionesUsuario> LogAccionesUsuario => Set<LogAccionesUsuario>();
    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TrabajosOrigenDestino>(entity =>
        {
            entity.HasOne(t => t.Origen)
                .WithMany()
                .HasForeignKey(t => t.OrigenId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Destino)
                .WithMany()
                .HasForeignKey(t => t.DestinoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.OrigenId, t.DestinoId }).IsUnique();
        });

        modelBuilder.Entity<TrabajoScripts>(entity =>
        {
            entity.ToTable("TrabajosScripts");

            entity.HasOne(ts => ts.ScriptPre)
                .WithMany(s => s.TrabajoScriptsComoPre)
                .HasForeignKey(ts => ts.ScriptPreId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ts => ts.ScriptPost)
                .WithMany(s => s.TrabajoScriptsComoPost)
                .HasForeignKey(ts => ts.ScriptPostId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Trabajo>(entity =>
        {
            entity.HasOne(t => t.TrabajosOrigenDestino)
                .WithMany(p => p.Trabajos)
                .HasForeignKey(t => t.TrabajosOrigenDestinoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.TrabajosScripts)
                .WithMany(ts => ts.Trabajos)
                .HasForeignKey(t => t.TrabajosScriptsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Origen>(entity =>
        {
            entity.HasIndex(o => o.Nombre).IsUnique();
        });

        modelBuilder.Entity<Destino>(entity =>
        {
            entity.HasIndex(d => d.Nombre).IsUnique();
        });

        modelBuilder.Entity<ScriptConfiguration>(entity =>
        {
            entity.HasKey(rj => new { rj.Id });
        });
    }

    /// <summary>
    /// Ejecuta migraciones y crea datos por defecto si no existen.
    /// </summary>
    public void EnsureSeedData()
    {
        Database.Migrate();

        if (!Users.Any())
        {
            Users.Add(new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), RequirePassword = true });
            SaveChanges();
        }

        if (!ApplicationSettings.Any())
        {
            ApplicationSettings.Add(new ApplicationSettings
            {
                Id = 1,
                ScriptExecutionTimeoutMinutes = 2
            });
            SaveChanges();
        }

        if (!Origenes.Any())
        {
            Origenes.AddRange(
                new Origen { Nombre = "Documentos", Ruta = "C:\\Users\\Usuario\\Documents", Descripcion = "Carpeta de documentos del usuario", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now },
                new Origen { Nombre = "Escritorio", Ruta = "C:\\Users\\Usuario\\Desktop", Descripcion = "Escritorio", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now },
                new Origen { Nombre = "Proyectos", Ruta = "C:\\Proyectos", Descripcion = "Carpeta de proyectos", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now }
            );
            SaveChanges();
        }
               

        if (!ScriptConfigurations.Any())
        {
            ScriptConfigurations.AddRange(
                new ScriptConfiguration { Nombre = "Notificar inicio", ScriptPath = "C:\\Scripts\\notify_start.ps1", Arguments = "", Tipo = ".bar" },
                new ScriptConfiguration { Nombre = "Limpiar temporales", ScriptPath = "C:\\Scripts\\clean_temp.ps1", Arguments = "asa", Tipo = ".js" },
                new ScriptConfiguration { Nombre = "Notificar fin", ScriptPath = "C:\\Scripts\\notify_end.ps1", Arguments = "weewe", Tipo = ".ps1" }
            );
            SaveChanges();
        }
    }
}
