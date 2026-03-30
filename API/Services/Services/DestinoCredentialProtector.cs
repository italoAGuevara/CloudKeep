using API.Security;
using API.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace API.Services.Services;

public class DestinoCredentialProtector : IDestinoCredentialProtector
{
    private readonly IDataProtector _protector;

    public DestinoCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(DestinoCredentialProtection.Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);
}
