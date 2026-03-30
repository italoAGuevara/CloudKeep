
namespace HostedService.Entities
{
    public class RelationJobsAndScript
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int ScriptId { get; set; }
        public Trabajo? Trabajo { get; set; }
        public ScriptConfiguration? Script { get; set; }
        public int ExecutionOrder { get; set; }        
        public bool Pre { get; set; }        
        public bool Post { get; set; }
        public bool StopOnFailure { get; set; } 
    }
}
