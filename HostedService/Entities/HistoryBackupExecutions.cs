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
    } 
}
