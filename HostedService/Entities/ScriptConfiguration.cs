using HostedService.Enums;

namespace HostedService.Entities
{
    public class ScriptConfiguration
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ScriptPath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public DateTime? UpdatedOn { get; set; }

        /// <summary>Tipo de script: .ps1, .bat o .js</summary>
        public string Tipo { get; set; } = string.Empty;

        public List<RelationJobsAndScript>? Jobs { get; set; }                
    }
}
