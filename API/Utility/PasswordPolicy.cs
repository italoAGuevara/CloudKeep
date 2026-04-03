using API.Exceptions;
using System.Text.RegularExpressions;

namespace API.Utility
{
    /// <summary>Reglas de contraseña para cambio de credenciales (alineadas con políticas fuertes habituales).</summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 12;
        public const int MaxLength = 128;

        /// <summary>Carácter especial ASCII típico en políticas empresariales.</summary>
        private static readonly Regex HasSpecial = new(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?~`]", RegexOptions.Compiled);

        private static readonly Regex HasUpper = new(@"[A-Z]", RegexOptions.Compiled);
        private static readonly Regex HasLower = new(@"[a-z]", RegexOptions.Compiled);
        private static readonly Regex HasDigit = new(@"\d", RegexOptions.Compiled);

        public static void ValidateNewPassword(string? newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new BadRequestException("La nueva contraseña es obligatoria.");

            if (newPassword.Length < MinLength)
                throw new BadRequestException($"La contraseña debe tener al menos {MinLength} caracteres.");

            if (newPassword.Length > MaxLength)
                throw new BadRequestException($"La contraseña no puede superar {MaxLength} caracteres.");

            if (!HasUpper.IsMatch(newPassword))
                throw new BadRequestException("La contraseña debe incluir al menos una letra mayúscula (A-Z).");

            if (!HasLower.IsMatch(newPassword))
                throw new BadRequestException("La contraseña debe incluir al menos una letra minúscula (a-z).");

            if (!HasDigit.IsMatch(newPassword))
                throw new BadRequestException("La contraseña debe incluir al menos un dígito (0-9).");

            if (!HasSpecial.IsMatch(newPassword))
                throw new BadRequestException(
                    "La contraseña debe incluir al menos un símbolo (por ejemplo: ! @ # $ % ^ & * _ - = + [ ] { }).");
        }
    }
}
