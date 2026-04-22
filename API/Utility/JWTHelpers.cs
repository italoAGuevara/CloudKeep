using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API.Utility
{
    public static class JWTHelpers
    {
        public static string GenerateToken(string username)
        {
            // 1. Definir la llave secreta (mínimo 32 caracteres para HS256)
            var secretKey = "Tu_Llave_Super_Secreta_De_Al_Menos_32_Chars!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // 2. Definir las credenciales de firma
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Crear los Claims (la información que lleva el token)
            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("role", "admin") // Ejemplo de claim personalizado
    };

            // 4. Configurar el Token
            var token = new JwtSecurityToken(
                issuer: "CloudKeep",
                audience: "CloudKeep",
                claims: claims,
                expires: DateTime.Now.AddHours(2), // Duración
                signingCredentials: creds
            );

            // 5. Escribir el token como string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
