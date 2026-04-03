using API.Exceptions;
using API.Services.Interfaces;
using API.Utility;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace API.Services.Services
{
    public class LoginService : ILogin
    {
        private readonly AppDbContext _context;
        public LoginService(AppDbContext context) => _context = context;

        public async Task<bool> ChangePassword(string previouPassword, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync();
            if (user is null)
                throw new InternalServerException("No hay usuario configurado.");

            if (!BCrypt.Net.BCrypt.Verify(previouPassword, user.PasswordHash))
                throw new UnauthorizedException("La contraseña actual no es correcta.");

            PasswordPolicy.ValidateNewPassword(newPassword);

            if (string.Equals(previouPassword, newPassword, StringComparison.Ordinal))
                throw new BadRequestException("La nueva contraseña debe ser distinta de la actual.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> isTokenValid(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = "Tu_Llave_Super_Secreta_De_Al_Menos_32_Chars!"; 
            var key = Encoding.ASCII.GetBytes(secretKey);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false, // Ajustar según necesidad
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false; 
            }
        }

        public async Task<string> LoginUser(string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync();

            if (user is null)
            {
                throw new InternalServerException("No hay usuario configurado.");
            }

            bool verified = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (verified)
            {
                return JWTHelpers.GenerateToken("admin");
            }

            throw new UnauthorizedException("Contraseña incorrecta");
        }
    }
}
