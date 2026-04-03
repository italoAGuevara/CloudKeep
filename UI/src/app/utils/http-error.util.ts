import { HttpErrorResponse } from '@angular/common/http';

/** El API (.NET + Newtonsoft) suele devolver `Message` en PascalCase. */
export function messageFromHttpError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const fromBody = messageFromErrorBody(err.error);
    if (fromBody) return fromBody;
    if (err.status === 0) return 'No hay conexión con el servidor. ¿Está la API en marcha?';
    if (err.status === 409) return 'Conflicto: el recurso está en uso.';
  }
  return 'No se pudo completar la operación.';
}

function messageFromErrorBody(body: unknown): string | null {
  if (body === null || body === undefined) return null;
  if (typeof body === 'string') {
    const t = body.trim();
    if (!t) return null;
    if (t.startsWith('{')) {
      try {
        const parsed = JSON.parse(t) as Record<string, unknown>;
        const msg = parsed['Message'] ?? parsed['message'];
        if (typeof msg === 'string' && msg.trim()) return msg.trim();
      } catch {
        /* cuerpo no JSON */
      }
    }
    return t;
  }
  if (typeof body === 'object') {
    const o = body as Record<string, unknown>;
    const msg = o['Message'] ?? o['message'];
    if (typeof msg === 'string' && msg.trim()) return msg.trim();
  }
  return null;
}
