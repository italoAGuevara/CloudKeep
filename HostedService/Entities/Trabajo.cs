namespace HostedService.Entities;

public class Trabajo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int TrabajosOrigenDestinoId { get; set; }
    public int TrabajosScriptsId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool Procesando { get; set; }
    public string EstatusPrevio { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;

    /// <summary>Filtro opcional: tamaño mínimo del archivo en bytes (inclusive).</summary>
    public long? CopiaTamanoMinBytes { get; set; }
    /// <summary>Filtro opcional: tamaño máximo del archivo en bytes (inclusive).</summary>
    public long? CopiaTamanoMaxBytes { get; set; }
    public DateTime? CopiaCreacionDesdeUtc { get; set; }
    public DateTime? CopiaCreacionHastaUtc { get; set; }
    public DateTime? CopiaActualizacionDesdeUtc { get; set; }
    public DateTime? CopiaActualizacionHastaUtc { get; set; }

    public TrabajosOrigenDestino TrabajosOrigenDestino { get; set; } = null!;
    public TrabajoScripts TrabajosScripts { get; set; } = null!;
}
