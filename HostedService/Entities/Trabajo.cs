namespace HostedService.Entities;

public class Trabajo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int TrabajosOrigenDestinoId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool Procesando { get; set; }
    public string EstatusPrevio { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;

    public TrabajosOrigenDestino TrabajosOrigenDestino { get; set; } = null!;
    public List<RelationJobsAndScript>? Scripts { get; set; }
}
