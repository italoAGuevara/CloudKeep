using API.Entitys;
using HostedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace API
{
    public class AppDbContext : DbContext
    {
        public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
        public DbSet<User> Users => Set<User>();
        public DbSet<FileMetadata> FileMetadatas => Set<FileMetadata>();
        public DbSet<HistoryBackupExecutions> HistoryBackupExecutions => Set<HistoryBackupExecutions>();
        public DbSet<Origen> Origenes => Set<Origen>();
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

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite("Data Source=mibase.db");
            // No lanzar excepción si el modelo tiene cambios pendientes (hay que añadir una migración).
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

    // Asegurar usuario único y datos de ejemplo (orígenes, scripts, jobs)
    // Este método ejecuta migraciones y crea datos por defecto si no existen.
    public void EnsureSeedData()
    {
        // Aplicar migraciones
        this.Database.Migrate();

        if (!this.Users.Any())
        {
            this.Users.Add(new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), RequirePassword = true });
            this.SaveChanges();
        }

        if (!this.Origenes.Any())
        {
            this.Origenes.AddRange(
                new Origen { Name = "Documentos", Path = "C:\\Users\\Usuario\\Documents", Description = "Carpeta de documentos del usuario" },
                new Origen { Name = "Escritorio", Path = "C:\\Users\\Usuario\\Desktop", Description = "Escritorio" },
                new Origen { Name = "Proyectos", Path = "C:\\Proyectos", Description = "Carpeta de proyectos" }
            );
            this.SaveChanges();
        }

        if (!this.StorageProviders.Any())
        {
            this.StorageProviders.AddRange(
                new StorageProvider { Name = "Local", ConfigJsonSchema = "{}" },
                new StorageProvider { Name = "S3", ConfigJsonSchema = "{\"bucket\":\"\",\"region\":\"\"}" }
            );
            this.SaveChanges();
        }

        if (!this.UserStorages.Any())
        {
            var user = this.Users.AsNoTracking().First();
            this.UserStorages.AddRange(
                new UserStorages { IdUser = user.Id, CloudDestination = "Carpeta local respaldos", CredentialJson = "{\"path\":\"D:\\\\Backups\"}" },
                new UserStorages { IdUser = user.Id, CloudDestination = "S3 principal", CredentialJson = "{\"bucket\":\"mi-bucket\",\"region\":\"us-east-1\"}" }
            );
            this.SaveChanges();
        }

        if (!this.ScriptConfigurations.Any())
        {
            this.ScriptConfigurations.AddRange(
                new ScriptConfiguration { Name = "Notificar inicio", ScriptPath = "C:\\Scripts\\notify_start.ps1", Arguments = "", Tipo = ".bar" },
                new ScriptConfiguration { Name = "Limpiar temporales", ScriptPath = "C:\\Scripts\\clean_temp.ps1", Arguments = "asa", Tipo = ".js" },
                new ScriptConfiguration { Name = "Notificar fin", ScriptPath = "C:\\Scripts\\notify_end.ps1", Arguments = "weewe", Tipo = ".ps1" }
            );
            this.SaveChanges();
        }

        if (!this.BackupJobs.Any())
        {
            var origenDoc = this.Origenes.First(o => o.Name == "Documentos");
            var destinoLocal = this.UserStorages.First(u => u.CloudDestination == "Carpeta local respaldos");
            var scriptPre = this.ScriptConfigurations.First(s => s.Name == "Notificar inicio");
            var scriptPost = this.ScriptConfigurations.First(s => s.Name == "Notificar fin");

            var job = new BackupJob
            {
                Name = "Backup diario documentos",
                Description = "Respaldo de la carpeta Documentos a la carpeta local de respaldos.",
                UserStorageId = destinoLocal.Id,
                OrigenId = origenDoc.Id,
                CronExpression = "0 2 * * *",
                IsActive = true
            };
            this.BackupJobs.Add(job);
            this.SaveChanges();

            this.relationJobsAndScripts.Add(new RelationJobsAndScript { JobId = job.Id, ScriptId = scriptPre.Id, ExecutionOrder = 1, Pre = true, Post = false });
            this.relationJobsAndScripts.Add(new RelationJobsAndScript { JobId = job.Id, ScriptId = scriptPost.Id, ExecutionOrder = 2, Pre = false, Post = true });
            this.SaveChanges();
        }
    }
    }
}
