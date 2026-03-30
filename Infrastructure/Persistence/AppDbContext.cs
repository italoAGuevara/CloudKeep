using HostedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FileMetadata> FileMetadatas => Set<FileMetadata>();
    public DbSet<HistoryBackupExecutions> HistoryBackupExecutions => Set<HistoryBackupExecutions>();
    public DbSet<Origen> Origenes => Set<Origen>();
    public DbSet<Destino> Destinos => Set<Destino>();
    public DbSet<RelationJobsAndScript> relationJobsAndScripts => Set<RelationJobsAndScript>();
    public DbSet<ScriptConfiguration> ScriptConfigurations => Set<ScriptConfiguration>();
    public DbSet<StorageProvider> StorageProviders => Set<StorageProvider>();
    public DbSet<UserStorages> UserStorages => Set<UserStorages>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BackupJob>(entity =>
        {
            entity.HasOne(b => b.Origen)
                .WithMany()
                .HasForeignKey(b => b.OrigenId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Origen>(entity =>
        {
            entity.HasIndex(o => o.Nombre).IsUnique();
        });

        modelBuilder.Entity<Destino>(entity =>
        {
            entity.HasIndex(d => d.Nombre).IsUnique();
        });

        modelBuilder.Entity<RelationJobsAndScript>(entity =>
        {
            entity.HasKey(rj => new { rj.ScriptId, rj.JobId });

            entity.HasOne(rj => rj.BackupJob)
                .WithMany(b => b.Scripts)
                .HasForeignKey(rj => rj.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rj => rj.Script)
                .WithMany(s => s.Jobs)
                .HasForeignKey(rj => rj.ScriptId)
                .OnDelete(DeleteBehavior.Cascade);
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

        if (!Origenes.Any())
        {
            Origenes.AddRange(
                new Origen { Nombre = "Documentos", Ruta = "C:\\Users\\Usuario\\Documents", Descripcion = "Carpeta de documentos del usuario", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now },
                new Origen { Nombre = "Escritorio", Ruta = "C:\\Users\\Usuario\\Desktop", Descripcion = "Escritorio", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now },
                new Origen { Nombre = "Proyectos", Ruta = "C:\\Proyectos", Descripcion = "Carpeta de proyectos", TamanoMaximo = "", FiltrosExclusiones = "", FechaCreacion = DateTime.Now }
            );
            SaveChanges();
        }

        if (!StorageProviders.Any())
        {
            StorageProviders.AddRange(
                new StorageProvider { Nombre = "Local", ConfigJsonSchema = "{}" },
                new StorageProvider { Nombre = "S3", ConfigJsonSchema = "{\"bucket\":\"\",\"region\":\"\"}" }
            );
            SaveChanges();
        }

        if (!UserStorages.Any())
        {
            var user = Users.AsNoTracking().First();
            UserStorages.AddRange(
                new UserStorages { IdUser = user.Id, CloudDestination = "Carpeta local respaldos", CredentialJson = "{\"ruta\":\"D:\\\\Backups\"}" },
                new UserStorages { IdUser = user.Id, CloudDestination = "S3 principal", CredentialJson = "{\"bucket\":\"mi-bucket\",\"region\":\"us-east-1\"}" }
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

        if (!BackupJobs.Any())
        {
            var origenDoc = Origenes.First(o => o.Nombre == "Documentos");
            var destinoLocal = UserStorages.First(u => u.CloudDestination == "Carpeta local respaldos");
            var scriptPre = ScriptConfigurations.First(s => s.Nombre == "Notificar inicio");
            var scriptPost = ScriptConfigurations.First(s => s.Nombre == "Notificar fin");

            var job = new BackupJob
            {
                Nombre = "Backup diario documentos",
                Descripcion = "Respaldo de la carpeta Documentos a la carpeta local de respaldos.",
                UserStorageId = destinoLocal.Id,
                OrigenId = origenDoc.Id,
                CronExpression = "0 2 * * *",
                IsActive = true
            };
            BackupJobs.Add(job);
            SaveChanges();

            relationJobsAndScripts.Add(new RelationJobsAndScript { JobId = job.Id, ScriptId = scriptPre.Id, ExecutionOrder = 1, Pre = true, Post = false });
            relationJobsAndScripts.Add(new RelationJobsAndScript { JobId = job.Id, ScriptId = scriptPost.Id, ExecutionOrder = 2, Pre = false, Post = true });
            SaveChanges();
        }
    }
}
