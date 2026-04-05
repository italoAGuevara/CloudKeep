import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, tap, throwError } from 'rxjs';
import { unwrapApiDetails } from '../utils/api-response.util';
import { messageFromHttpError } from '../utils/http-error.util';
import { ToastService } from './toast.service';

export interface Job {
  id: number;
  name: string;
  description: string;
  origenId: number;
  destinoId: number;
  /** Expresión cron (5 campos: minuto hora díaMes mes díaSemana). */
  schedule: string;
  enabled: boolean;
  scriptPreId: number | null;
  scriptPostId: number | null;
  preDetenerEnFallo: boolean;
  postDetenerEnFallo: boolean;
  procesando: boolean;
}

export interface CreateTrabajoPayload {
  nombre: string;
  descripcion: string;
  origenId: number;
  destinoId: number;
  scriptPreId: number | null;
  scriptPostId: number | null;
  preDetenerEnFallo?: boolean;
  postDetenerEnFallo?: boolean;
  cronExpression: string;
  activo?: boolean;
}

export interface EjecutarTrabajoResult {
  historialId: number;
  archivosCopiados: number;
  mensaje: string;
}

export type UpdateTrabajoPayload = Partial<{
  nombre: string;
  descripcion: string;
  origenId: number;
  destinoId: number;
  scriptPreId: number | null;
  scriptPostId: number | null;
  preDetenerEnFallo: boolean;
  postDetenerEnFallo: boolean;
  cronExpression: string;
  activo: boolean;
  procesando: boolean;
  estatusPrevio: string;
  /** Debe ser true al guardar desde el asistente para aplicar scripts (incluido dejarlos vacíos). */
  sincronizarScripts: boolean;
}>;

interface TrabajoApiDto {
  id: number;
  nombre: string;
  descripcion: string;
  trabajosOrigenDestinoId: number;
  origenId: number;
  destinoId: number;
  trabajosScriptsId: number;
  scriptPreId: number | null;
  scriptPostId: number | null;
  preDetenerEnFallo: boolean;
  postDetenerEnFallo: boolean;
  cronExpression: string;
  activo: boolean;
  procesando: boolean;
  estatusPrevio: string;
  fechaCreacion: string;
  fechaModificacion: string;
}

interface EjecutarTrabajoApiDto {
  historialId: number;
  archivosCopiados: number;
  mensaje: string;
}

const API_TRABAJOS = '/api/trabajos';

function fromApi(d: TrabajoApiDto): Job {
  return {
    id: d.id,
    name: d.nombre,
    description: d.descripcion ?? '',
    origenId: d.origenId,
    destinoId: d.destinoId,
    schedule: d.cronExpression ?? '',
    enabled: !!d.activo,
    scriptPreId: d.scriptPreId ?? null,
    scriptPostId: d.scriptPostId ?? null,
    preDetenerEnFallo: !!d.preDetenerEnFallo,
    postDetenerEnFallo: !!d.postDetenerEnFallo,
    procesando: !!d.procesando,
  };
}

@Injectable({
  providedIn: 'root',
})
export class JobsService {
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);

  jobs = signal<Job[]>([]);
  readonly loading = signal(false);

  loadAll(): void {
    this.loading.set(true);
    this.http.get<unknown>(API_TRABAJOS).subscribe({
      next: (res) => {
        const raw = unwrapApiDetails<TrabajoApiDto[]>(res);
        const list = Array.isArray(raw) ? raw : [];
        this.jobs.set(list.map(fromApi).sort((a, b) => a.id - b.id));
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.show(messageFromHttpError(err), 'error');
      },
    });
  }

  getById(id: number): Observable<Job> {
    return this.http.get<unknown>(`${API_TRABAJOS}/${id}`).pipe(
      map((res) => fromApi(unwrapApiDetails<TrabajoApiDto>(res))),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  create(payload: CreateTrabajoPayload): Observable<Job> {
    const body = {
      nombre: payload.nombre.trim(),
      descripcion: payload.descripcion.trim(),
      origenId: payload.origenId,
      destinoId: payload.destinoId,
      scriptPreId: payload.scriptPreId,
      scriptPostId: payload.scriptPostId,
      preDetenerEnFallo: payload.preDetenerEnFallo ?? false,
      postDetenerEnFallo: payload.postDetenerEnFallo ?? false,
      cronExpression: payload.cronExpression.trim(),
      activo: payload.activo ?? true,
    };
    return this.http.post<unknown>(API_TRABAJOS, body).pipe(
      map((res) => fromApi(unwrapApiDetails<TrabajoApiDto>(res))),
      tap((created) => {
        this.jobs.update((list) => [...list, created].sort((a, b) => a.id - b.id));
        this.toast.show('Trabajo creado', 'success');
      }),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  update(id: number, payload: UpdateTrabajoPayload): Observable<Job> {
    return this.http.put<unknown>(`${API_TRABAJOS}/${id}`, payload).pipe(
      map((res) => fromApi(unwrapApiDetails<TrabajoApiDto>(res))),
      tap((updated) => {
        this.jobs.update((list) => list.map((j) => (j.id === updated.id ? updated : j)));
        this.toast.show('Trabajo actualizado', 'success');
      }),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      })
    );
  }

  /** Ejecuta la copia manual (origen → S3 o Google Drive según el destino del trabajo). */
  runManual(id: number): Observable<EjecutarTrabajoResult> {
    return this.http.post<unknown>(`${API_TRABAJOS}/${id}/ejecutar`, {}).pipe(
      map((res) => unwrapApiDetails<EjecutarTrabajoApiDto>(res)),
      map((d) => ({
        historialId: d.historialId,
        archivosCopiados: d.archivosCopiados,
        mensaje: d.mensaje,
      })),
      tap((r) => {
        this.toast.show(r.mensaje, 'success');
        this.loadAll();
      }),
      catchError((err) => {
        this.toast.show(messageFromHttpError(err), 'error');
        this.loadAll();
        return throwError(() => err);
      })
    );
  }

  deleteById(id: number): Observable<void> {
    return this.http.delete(`${API_TRABAJOS}/${id}`).pipe(
      catchError((err: unknown) => {
        this.toast.show(messageFromHttpError(err), 'error');
        return throwError(() => err);
      }),
      tap(() => {
        this.jobs.update((list) => list.filter((j) => j.id !== id));
        this.toast.show('Trabajo eliminado', 'warning');
      }),
      map(() => void 0)
    );
  }
}
