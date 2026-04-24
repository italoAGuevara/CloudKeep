using HostedService.Enums;

namespace HostedService.Entities
{
    public class HistoryBackupExecutions
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public BackupStatus Status { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Manual (UI/API) o programada (cron).</summary>
        public JobExecutionTrigger Trigger { get; set; } = JobExecutionTrigger.Manual;

        /// <summary>Archivos copiados en destino; null si falló antes de completar la copia.</summary>
        public int? ArchivosCopiados { get; set; }

        /// <summary>Usuario que disparó la ejecución ("Sistema" si fue automática).</summary>
        public string EjecutadoPor { get; set; } = "Sistema";
    }
}
