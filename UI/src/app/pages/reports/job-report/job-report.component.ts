import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { JobsService, Job } from '../../../services/jobs.service';
import {
  EjecucionHistorialItem,
  JobExecutionsReportService,
} from '../../../services/job-executions-report.service';
import { ScriptsService } from '../../../services/scripts.service';
import { DestinationsService } from '../../../services/destinations.service';
import { ReportBreadcrumbComponent, BreadcrumbItem } from '../../../layout/report-breadcrumb/report-breadcrumb.component';
import {
  LogAccionUsuarioItem,
  LogAccionesUsuarioService,
} from '../../../services/log-acciones-usuario.service';

@Component({
  selector: 'app-job-report',
  standalone: true,
  imports: [CommonModule, RouterLink, ReportBreadcrumbComponent, DatePipe, FormsModule],
  templateUrl: './job-report.component.html',
  styleUrl: './job-report.component.css',
})
export class JobReportComponent implements OnInit {
  private jobsService = inject(JobsService);
  private executionsReport = inject(JobExecutionsReportService);
  private scriptsService = inject(ScriptsService);
  private destinationsService = inject(DestinationsService);
  private logAccionesUsuario = inject(LogAccionesUsuarioService);

  breadcrumbItems: BreadcrumbItem[] = [{ label: 'Reporte de trabajos' }];
  jobs = this.jobsService.jobs;

  historial = signal<EjecucionHistorialItem[]>([]);
  historialTotal = signal(0);
  historialPage = signal(1);
  readonly historialPageSize = 25;
  historialLoading = signal(false);
  /** Filtro por trabajo: '' = todos */
  filtroTrabajoId: number | '' = '';
  /** Filtro por actor de ejecución: '' = todos */
  filtroEjecutadoPor: string = '';

  logAcciones = signal<LogAccionUsuarioItem[]>([]);
  logAccionesLoading = signal(false);
  private expandedJsonCells = signal<Set<string>>(new Set());
  /** Máximo permitido por el backend en una sola consulta. */
  readonly logAccionesLimite = 2000;

  ngOnInit(): void {
    this.jobsService.loadAll();
    this.scriptsService.loadAll();
    this.destinationsService.loadAll();
    this.cargarHistorialEjecuciones();
    this.cargarLogAccionesUsuario();
  }

  cargarLogAccionesUsuario(): void {
    this.logAccionesLoading.set(true);
    this.logAccionesUsuario.listar(this.logAccionesLimite).subscribe({
      next: (rows) => {
        this.logAcciones.set(rows);
        this.logAccionesLoading.set(false);
      },
      error: () => {
        this.logAcciones.set([]);
        this.logAccionesLoading.set(false);
      },
    });
  }

  cargarHistorialEjecuciones(): void {
    this.historialLoading.set(true);
    const tid = this.filtroTrabajoId;
    this.executionsReport
      .getHistorial({
        trabajoId: tid === '' ? null : tid,
        ejecutadoPor: this.filtroEjecutadoPor || null,
        page: this.historialPage(),
        pageSize: this.historialPageSize,
      })
      .subscribe({
        next: (list) => {
          this.historial.set(list.items);
          this.historialTotal.set(list.total);
          this.historialLoading.set(false);
        },
        error: () => {
          this.historial.set([]);
          this.historialTotal.set(0);
          this.historialLoading.set(false);
        },
      });
  }

  aplicarFiltroHistorial(): void {
    this.historialPage.set(1);
    this.cargarHistorialEjecuciones();
  }

  actoresHistorial(): string[] {
    return Array.from(
      new Set(
        this.historial()
          .map((h) => (h.ejecutadoPor ?? '').trim())
          .filter((v) => v.length > 0)
      )
    ).sort((a, b) => a.localeCompare(b, 'es'));
  }

  historialTotalPaginas(): number {
    return Math.max(1, Math.ceil(this.historialTotal() / this.historialPageSize));
  }

  historialPaginaAnterior(): void {
    if (this.historialPage() <= 1) return;
    this.historialPage.update((p) => p - 1);
    this.cargarHistorialEjecuciones();
  }

  historialPaginaSiguiente(): void {
    if (this.historialPage() >= this.historialTotalPaginas()) return;
    this.historialPage.update((p) => p + 1);
    this.cargarHistorialEjecuciones();
  }

  formatoDuracion(seg: number | null): string {
    if (seg === null || seg === undefined || Number.isNaN(seg)) return '—';
    if (seg < 60) return `${Math.round(seg)} s`;
    const m = Math.floor(seg / 60);
    const s = Math.round(seg % 60);
    return `${m} min ${s} s`;
  }

  estadoHistorialClass(estado: string): string {
    switch (estado) {
      case 'completado':
        return 'bg-success';
      case 'fallido':
        return 'bg-danger';
      case 'en_progreso':
        return 'bg-warning text-dark';
      default:
        return 'bg-secondary';
    }
  }

  estadoHistorialLabel(estado: string): string {
    switch (estado) {
      case 'completado':
        return 'Completado';
      case 'fallido':
        return 'Fallido';
      case 'en_progreso':
        return 'En progreso';
      case 'pendiente':
        return 'Pendiente';
      default:
        return estado;
    }
  }

  disparoLabel(disparo: string): string {
    return disparo === 'programada' ? 'Programada (cron)' : 'Manual';
  }

  truncarTexto(s: string | null, max: number): string {
    if (!s) return '—';
    return s.length <= max ? s : s.slice(0, max) + '…';
  }

  jsonCellKey(rowId: string, field: 'anterior' | 'nuevo'): string {
    return `${rowId}:${field}`;
  }

  isJsonCellExpanded(rowId: string, field: 'anterior' | 'nuevo'): boolean {
    return this.expandedJsonCells().has(this.jsonCellKey(rowId, field));
  }

  toggleJsonCell(rowId: string, field: 'anterior' | 'nuevo'): void {
    const key = this.jsonCellKey(rowId, field);
    this.expandedJsonCells.update((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  formatJsonForView(value: string | null): string {
    if (!value) return '—';
    try {
      const parsed = JSON.parse(value);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return value;
    }
  }

  destinationLabel(job: Job): string {
    const d = this.destinationsService.destinations().find((x) => x.id === job.destinoId);
    return d?.name ?? `Destino #${job.destinoId}`;
  }

  scheduleLabel(schedule: string): string {
    const parts = schedule.trim().split(/\s+/);
    if (parts.length < 5) return schedule;
    const [min, hour, dayMonth, , dayWeek] = parts;
    const h = parseInt(hour, 10);
    const m = parseInt(min, 10);
    const timeStr = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
    if (dayMonth !== '*' && dayWeek === '*') {
      return `Mensual, día ${dayMonth} a las ${timeStr}`;
    }
    if (dayWeek !== '*') {
      const names = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
      const days = dayWeek.split(',').map((s) => names[parseInt(s.trim(), 10)]).filter(Boolean);
      return `Semanal (${days.join(', ')}) a las ${timeStr}`;
    }
    return `Diario a las ${timeStr}`;
  }

  getJobScripts(job: Job): { name: string; when: 'pre' | 'post' }[] {
    const allScripts = this.scriptsService.scripts();
    const out: { name: string; when: 'pre' | 'post' }[] = [];
    if (job.scriptPreId != null) {
      const pre = allScripts.find((s) => s.id === String(job.scriptPreId));
      if (pre) out.push({ name: pre.name, when: 'pre' });
    }
    if (job.scriptPostId != null) {
      const post = allScripts.find((s) => s.id === String(job.scriptPostId));
      if (post) out.push({ name: post.name, when: 'post' });
    }
    return out;
  }

  countActive(): number {
    return this.jobs().filter((j) => j.enabled).length;
  }

  countPaused(): number {
    return this.jobs().filter((j) => !j.enabled).length;
  }
}
