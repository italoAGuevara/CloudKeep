namespace HostedService.Backup;

/// <summary>
/// Error de negocio al copiar archivos hacia un destino en la nube (mensaje listo para el cliente).
/// </summary>
public sealed class DestinoCopyException : Exception
{
    public DestinoCopyException(string message)
        : base(message)
    {
    }
}
