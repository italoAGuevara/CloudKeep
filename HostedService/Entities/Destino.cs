
namespace HostedService.Entities
{
    public class Destino
    {
        public int Id { get; set; }
        public string Nombre  { get; set; } = string.Empty;
        public string TipoDeDestino { get; set; } = string.Empty;
        public string Credenciales { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
    }
}
