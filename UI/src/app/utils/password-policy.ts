/** Misma política que API/Utility/PasswordPolicy.cs */
export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;

const hasUpper = /[A-Z]/;
const hasLower = /[a-z]/;
const hasDigit = /\d/;
const hasSpecial = /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?~`]/;

/** Si no es válida, devuelve el mensaje de error; si es válida, null. */
export function validateStrongPassword(password: string): string | null {
  const p = password ?? '';
  if (!p.trim()) {
    return 'La nueva contraseña es obligatoria.';
  }
  if (p.length < PASSWORD_MIN_LENGTH) {
    return `La contraseña debe tener al menos ${PASSWORD_MIN_LENGTH} caracteres.`;
  }
  if (p.length > PASSWORD_MAX_LENGTH) {
    return `La contraseña no puede superar ${PASSWORD_MAX_LENGTH} caracteres.`;
  }
  if (!hasUpper.test(p)) {
    return 'La contraseña debe incluir al menos una letra mayúscula (A-Z).';
  }
  if (!hasLower.test(p)) {
    return 'La contraseña debe incluir al menos una letra minúscula (a-z).';
  }
  if (!hasDigit.test(p)) {
    return 'La contraseña debe incluir al menos un dígito (0-9).';
  }
  if (!hasSpecial.test(p)) {
    return 'La contraseña debe incluir al menos un símbolo (por ejemplo: ! @ # $ % ^ & * _ - = + [ ] { }).';
  }
  return null;
}
