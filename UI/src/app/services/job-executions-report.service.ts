import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { unwrapApiDetails } from '../utils/api-response.util';

const API_BASE = '/api/reportes/ejecuciones';

export interface EjecucionHistorialItem {
  id: number;
  trabajoId: number;
  trabajoNombre: string;
  startTime: string;
  endTime: string;
  duracionSegundos: number | null;
  estado: string;
  archivosCopiados: number | null;
  disparo: string;
  ejecutadoPor: string;
  errorMessage: string | null;
}

export interface EjecucionHistorialList {
  items: EjecucionHistorialItem[];
  total: number;
}

@Injectable({
  providedIn: 'root',
})
export class JobExecutionsReportService {
  private http = inject(HttpClient);

  getHistorial(params: {
    trabajoId?: number | null;
    ejecutadoPor?: string | null;
    desdeUtc?: string | null;
    hastaUtc?: string | null;
    page?: number;
    pageSize?: number;
  }): Observable<EjecucionHistorialList> {
    let httpParams = new HttpParams();
    if (params.trabajoId != null && params.trabajoId > 0) {
      httpParams = httpParams.set('trabajoId', String(params.trabajoId));
    }
    if (params.ejecutadoPor) {
      httpParams = httpParams.set('ejecutadoPor', params.ejecutadoPor);
    }
    if (params.desdeUtc) httpParams = httpParams.set('desdeUtc', params.desdeUtc);
    if (params.hastaUtc) httpParams = httpParams.set('hastaUtc', params.hastaUtc);
    httpParams = httpParams.set('page', String(params.page ?? 1));
    httpParams = httpParams.set('pageSize', String(params.pageSize ?? 50));

    return this.http.get<unknown>(API_BASE, { params: httpParams }).pipe(
      map((res) => unwrapApiDetails<EjecucionHistorialList>(res))
    );
  }
}
