import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ScriptsService } from '../../services/scripts.service';
import { ToastService } from '../../services/toast.service';
import { JobsService, Job } from '../../services/jobs.service';
import { DestinationsService } from '../../services/destinations.service';

@Component({
  selector: 'app-jobs',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './jobs.component.html',
  styleUrl: './jobs.component.css',
})
export class JobsComponent implements OnInit {
  private router = inject(Router);
  private scriptsService = inject(ScriptsService);
  private toastService = inject(ToastService);
  private jobsService = inject(JobsService);
  private destinationsService = inject(DestinationsService);

  readonly Math = Math;

  availableScripts = this.scriptsService.scripts;
  jobs = this.jobsService.jobs;
  readonly jobsLoading = this.jobsService.loading;

  readonly pageSize = signal(10);
  readonly currentPage = signal(1);

  readonly totalItems = computed(() => this.jobs().length);
  readonly totalPages = computed(() => {
    const total = this.totalItems();
    const size = this.pageSize();
    return size <= 0 ? 1 : Math.ceil(total / size);
  });
  readonly paginatedJobs = computed(() => {
    const list = this.jobs();
    const size = this.pageSize();
    const page = this.currentPage();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });
  readonly hasPrevPage = computed(() => this.currentPage() > 1);
  readonly hasNextPage = computed(() => this.currentPage() < this.totalPages());

  ngOnInit(): void {
    this.jobsService.loadAll();
    this.scriptsService.loadAll();
    this.destinationsService.loadAll();
  }

  destinationLabel(job: Job): string {
    const d = this.destinationsService.destinations().find((x) => x.id === job.destinoId);
    return d?.name ?? `Destino #${job.destinoId}`;
  }

  runJob(job: Job): void {
    if (job.procesando) return;
    this.runningJobId.set(job.id);
    this.toastService.show(`Ejecutando «${job.name}»…`, 'info');
    this.jobsService.runManual(job.id).subscribe({
      next: () => {
        this.runningJobId.set(null);
      },
      error: () => {
        this.runningJobId.set(null);
      },
    });
  }

  setPage(page: number): void {
    const max = this.totalPages();
    this.currentPage.set(Math.max(1, Math.min(page, max)));
  }

  onPageSizeChange(value: number | string): void {
    this.pageSize.set(Number(value));
    this.setPage(1);
  }

  openCreate(): void {
    this.router.navigate(['/trabajos/nuevo']);
  }

  openEdit(job: Job): void {
    this.router.navigate(['/trabajos', job.id, 'editar']);
  }

  deleteJob(job: Job): void {
    if (!confirm(`¿Eliminar trabajo "${job.name}"?`)) return;
    this.jobsService.deleteById(job.id).subscribe();
  }

  togglePause(job: Job): void {
    this.jobsService.update(job.id, { activo: !job.enabled }).subscribe();
  }

  runningJobId = signal<number | null>(null);

  isRunning(job: Job): boolean {
    return this.runningJobId() === job.id || job.procesando;
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
    const allScripts = this.availableScripts();
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
}
