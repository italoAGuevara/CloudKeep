import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { unwrapApiDetails } from '../utils/api-response.util';
import { messageFromHttpError } from '../utils/http-error.util';
import { ToastService } from './toast.service';

export interface OrigenRow {
  id: number;
  name: string;
  path: string;
  description: string;
  filtrosExclusiones: string;
}

interface OrigenApiDto {
  id: number;
  nombre: string;
  ruta: string;
  descripcion: string;
  filtrosExclusiones: string;
}

interface RutaValidaApiDto {
  ruta: string;
}

const API_ORIGENES = '/api/origenes';

@Injectable({
  providedIn: 'root',
})
export class OriginsService {
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);

  origins = signal<OrigenRow[]>([]);
  readonly loading = signal(false);

  loadAll(): void {
    this.loading.set(true);
    this.http.get<unknown>(API_ORIGENES).subscribe({
      next: (res) => {
        const raw = unwrapApiDetails<OrigenApiDto[]>(res);
        const list = Array.isArray(raw) ? raw : [];
        this.origins.set(list.map((o) => this.fromApi(o)).sort((a, b) => a.id - b.id));
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.show(messageFromHttpError(err), 'error');
      },
    });
  }

  getById(id: number): Observable<OrigenRow> {
    return this.http.get<unknown>(`${API_ORIGENES}/${id}`).pipe(
      map((res) => this.fromApi(unwrapApiDetails<OrigenApiDto>(res))),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  /** Igual que getById pero sin toast ni error al fallar (p. ej. asistente de trabajo). */
  getByIdQuiet(id: number): Observable<OrigenRow | null> {
    return this.http.get<unknown>(`${API_ORIGENES}/${id}`).pipe(
      map((res) => this.fromApi(unwrapApiDetails<OrigenApiDto>(res))),
      catchError(() => of(null))
    );
  }

  /** Valida en el servidor que la carpeta exista (Path.GetFullPath + Directory.Exists). */
  validarRuta(ruta: string): Observable<{ ruta: string }> {
    return this.http.post<unknown>(`${API_ORIGENES}/validar-ruta`, { ruta }).pipe(
      map((res) => unwrapApiDetails<RutaValidaApiDto>(res) as { ruta: string }),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  /** Obtiene o crea un origen con esa ruta (misma validación que validarRuta). */
  asegurarPorRuta(ruta: string, filtrosExclusiones?: string): Observable<OrigenRow> {
    return this.http.post<unknown>(`${API_ORIGENES}/asegurar-por-ruta`, { ruta, filtrosExclusiones }).pipe(
      map((res) => this.fromApi(unwrapApiDetails<OrigenApiDto>(res))),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  private fromApi(o: OrigenApiDto): OrigenRow {
    return {
      id: o.id,
      name: o.nombre,
      path: o.ruta ?? '',
      description: o.descripcion ?? '',
      filtrosExclusiones: o.filtrosExclusiones ?? '',
    };
  }
}
