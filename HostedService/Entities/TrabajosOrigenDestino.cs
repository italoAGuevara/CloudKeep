namespace HostedService.Entities;

/// <summary>Empareja un origen y un destino; varios trabajos pueden reutilizar el mismo vínculo.</summary>
public class TrabajosOrigenDestino
{
    public int Id { get; set; }
    public int OrigenId { get; set; }
    public int DestinoId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;

    public Origen Origen { get; set; } = null!;
    public Destino Destino { get; set; } = null!;
    public ICollection<Trabajo> Trabajos { get; set; } = new List<Trabajo>();
}
