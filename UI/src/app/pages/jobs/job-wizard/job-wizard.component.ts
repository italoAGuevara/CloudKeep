import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { map, switchMap, finalize } from 'rxjs/operators';
import { ScriptsService } from '../../../services/scripts.service';
import { ToastService } from '../../../services/toast.service';
import { DestinationsService } from '../../../services/destinations.service';
import { OriginsService } from '../../../services/origins.service';
import { JobsService } from '../../../services/jobs.service';
import type { TrabajoCopiaFiltrosApi } from '../../../services/jobs.service';

const WEEKDAY_LABELS = [
  { value: 0, label: 'D' },
  { value: 1, label: 'L' },
  { value: 2, label: 'M' },
  { value: 3, label: 'X' },
  { value: 4, label: 'J' },
  { value: 5, label: 'V' },
  { value: 6, label: 'S' },
];

/** Interpreta cron de 5 campos estándar para rellenar el formulario (casos simples). */
function applyCronToForm(
  cron: string,
  target: {
    formScheduleType: 'daily' | 'weekly' | 'monthly';
    formScheduleHour: number;
    formScheduleMinute: number;
    formScheduleWeekdays: number[];
    formScheduleDayOfMonth: number;
  }
): void {
  const parts = cron.trim().split(/\s+/).filter(Boolean);
  if (parts.length < 5) return;
  const [minS, hourS, dom, , dow] = parts;
  const m = parseInt(minS, 10);
  const h = parseInt(hourS, 10);
  if (!Number.isNaN(h) && h >= 0 && h <= 23) target.formScheduleHour = h;
  if (!Number.isNaN(m) && m >= 0 && m <= 59) target.formScheduleMinute = m;

  if (dow !== '*' && dow !== '?') {
    target.formScheduleType = 'weekly';
    const days = dow
      .split(',')
      .map((s) => parseInt(s.trim(), 10))
      .filter((n) => !Number.isNaN(n) && n >= 0 && n <= 6);
    if (days.length) target.formScheduleWeekdays = [...new Set(days)].sort((a, b) => a - b);
    return;
  }
  if (dom !== '*' && dom !== '?') {
    const d = parseInt(dom, 10);
    if (!Number.isNaN(d) && d >= 1 && d <= 31) {
      target.formScheduleType = 'monthly';
      target.formScheduleDayOfMonth = d;
    }
    return;
  }
  target.formScheduleType = 'daily';
}

const BYTES_PER_MIB = 1024 * 1024;

function bytesToMiBInput(v: number | null | undefined): string {
  if (v == null || !Number.isFinite(v)) return '';
  const x = v / BYTES_PER_MIB;
  if (x === 0) return '0';
  return x.toFixed(4).replace(/\.?0+$/, '');
}

function isoUtcToDatetimeLocalValue(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function localDatetimeToIsoUtc(s: string): string | null {
  const t = s?.trim();
  if (!t) return null;
  const d = new Date(t);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}

@Component({
  selector: 'app-job-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './job-wizard.component.html',
  styleUrl: './job-wizard.component.css',
})
export class JobWizardComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  readonly scriptsService = inject(ScriptsService);
  private toastService = inject(ToastService);
  readonly destinationsService = inject(DestinationsService);
  private originsService = inject(OriginsService);
  private jobsService = inject(JobsService);

  readonly saving = signal(false);
  readonly loadingJob = signal(false);
  readonly checkingOrigenPath = signal(false);
  /** La API confirmó que la carpeta existe (ruta canónica en el servidor). */
  readonly origenPathValidated = signal(false);

  readonly weekdayOptions = WEEKDAY_LABELS;
  readonly hours = Array.from({ length: 24 }, (_, i) => i);
  readonly daysOfMonth = Array.from({ length: 31 }, (_, i) => i + 1);

  destinations = this.destinationsService.destinations;
  availableScripts = this.scriptsService.scripts;

  readonly isEditMode = computed(() => this.editingJobId() !== null);
  editingJobId = signal<number | null>(null);

  steps = ['General', 'Destino', 'Fuente', 'Programación', 'Opciones'];
  currentStep = 0;

  formName = '';
  formDescription = '';
  formEnabled = true;
  formDestinoId: number | null = null;
  /** Ruta de carpeta local; la existencia se valida en el servidor (donde corre la API). */
  formOrigenRuta = '';
  /** Patrones excluidos separados por coma, punto y coma o salto de línea. */
  formOrigenExclusiones = '';

  formScheduleType: 'daily' | 'weekly' | 'monthly' = 'daily';
  formScheduleHour = 2;
  formScheduleMinute = 0;
  formScheduleWeekdays: number[] = [1];
  formScheduleDayOfMonth = 1;

  formScriptPreId: number | null = null;
  formScriptPostId: number | null = null;
  formPreDetenerEnFallo = false;
  formPostDetenerEnFallo = false;

  /** Tamaño mínimo en MiB (vacío = sin filtro). */
  formCopiaTamMinMb = '';
  formCopiaTamMaxMb = '';
  formCopiaCreacionDesde = '';
  formCopiaCreacionHasta = '';
  formCopiaActualizacionDesde = '';
  formCopiaActualizacionHasta = '';

  ngOnInit(): void {
    this.scriptsService.loadAll();
    this.destinationsService.loadAll();

    this.route.paramMap.subscribe((p) => {
      const idStr = p.get('id');
      if (idStr != null && idStr !== '') {
        const id = Number(idStr);
        if (Number.isFinite(id)) {
          this.editingJobId.set(id);
          this.loadJobForEdit(id);
          return;
        }
      }
      this.editingJobId.set(null);
      this.resetFormForCreate();
    });
  }

  private resetFormForCreate(): void {
    this.formName = '';
    this.formDescription = '';
    this.formEnabled = true;
    this.formDestinoId = null;
    this.formOrigenRuta = '';
    this.formOrigenExclusiones = '';
    this.origenPathValidated.set(false);
    this.formScheduleType = 'daily';
    this.formScheduleHour = 2;
    this.formScheduleMinute = 0;
    this.formScheduleWeekdays = [1];
    this.formScheduleDayOfMonth = 1;
    this.formScriptPreId = null;
    this.formScriptPostId = null;
    this.formPreDetenerEnFallo = false;
    this.formPostDetenerEnFallo = false;
    this.formCopiaTamMinMb = '';
    this.formCopiaTamMaxMb = '';
    this.formCopiaCreacionDesde = '';
    this.formCopiaCreacionHasta = '';
    this.formCopiaActualizacionDesde = '';
    this.formCopiaActualizacionHasta = '';
    this.currentStep = 0;
  }

  private loadJobForEdit(id: number): void {
    this.loadingJob.set(true);
    this.jobsService
      .getById(id)
      .pipe(switchMap((job) => this.originsService.getByIdQuiet(job.origenId).pipe(map((origen) => ({ job, origen })))))
      .subscribe({
        next: ({ job, origen }) => {
          this.formName = job.name;
          this.formDescription = job.description;
          this.formEnabled = job.enabled;
          this.formDestinoId = job.destinoId;
          this.formOrigenRuta = origen?.path ?? '';
          this.formOrigenExclusiones = origen?.filtrosExclusiones ?? '';
          this.origenPathValidated.set(!!origen?.path);
          this.formScriptPreId = job.scriptPreId;
          this.formScriptPostId = job.scriptPostId;
          this.formPreDetenerEnFallo = job.preDetenerEnFallo;
          this.formPostDetenerEnFallo = job.postDetenerEnFallo;
          this.formCopiaTamMinMb = bytesToMiBInput(job.copiaTamanoMinBytes);
          this.formCopiaTamMaxMb = bytesToMiBInput(job.copiaTamanoMaxBytes);
          this.formCopiaCreacionDesde = isoUtcToDatetimeLocalValue(job.copiaCreacionDesdeUtc);
          this.formCopiaCreacionHasta = isoUtcToDatetimeLocalValue(job.copiaCreacionHastaUtc);
          this.formCopiaActualizacionDesde = isoUtcToDatetimeLocalValue(job.copiaActualizacionDesdeUtc);
          this.formCopiaActualizacionHasta = isoUtcToDatetimeLocalValue(job.copiaActualizacionHastaUtc);
          applyCronToForm(job.schedule, this);
          this.loadingJob.set(false);
        },
        error: () => this.loadingJob.set(false),
      });
  }

  get currentStepName(): string {
    return this.steps[this.currentStep];
  }

  get formSchedule(): string {
    const m = this.formScheduleMinute;
    const h = this.formScheduleHour;
    if (this.formScheduleType === 'daily') {
      return `${m} ${h} * * *`;
    }
    if (this.formScheduleType === 'weekly') {
      const days = this.formScheduleWeekdays.length
        ? [...this.formScheduleWeekdays].sort((a, b) => a - b).join(',')
        : '0';
      return `${m} ${h} * * ${days}`;
    }
    const d = this.formScheduleDayOfMonth;
    return `${m} ${h} ${d} * *`;
  }

  private validateForSave(): string | null {
    if (!this.formName.trim()) return 'Indica un nombre para el trabajo.';
    if (!this.formDescription.trim()) return 'La descripción es obligatoria.';
    if (this.formDestinoId == null) return 'Selecciona un destino.';
    if (!this.formOrigenRuta.trim()) return 'Indica la ruta de la carpeta de respaldo.';
    return this.validateCopiaFiltrosForm();
  }

  private validateCopiaFiltrosForm(): string | null {
    return this.parseCopiaFiltrosForm();
  }

  /** Devuelve mensaje de error o null si los filtros opcionales son coherentes. */
  private parseCopiaFiltrosForm(): string | null {
    const parseMb = (raw: string): number | null | 'bad' => {
      const t = raw.trim();
      if (!t) return null;
      const n = Number(t.replace(',', '.'));
      if (!Number.isFinite(n) || n < 0) return 'bad';
      return Math.round(n * BYTES_PER_MIB);
    };

    const minR = parseMb(this.formCopiaTamMinMb);
    const maxR = parseMb(this.formCopiaTamMaxMb);
    if (minR === 'bad' || maxR === 'bad')
      return 'Los tamaños en MiB deben ser números mayores o iguales a cero.';
    const minB = minR;
    const maxB = maxR;
    if (minB != null && maxB != null && minB > maxB)
      return 'El tamaño mínimo (MiB) no puede ser mayor que el tamaño máximo (MiB).';

    const c0 = localDatetimeToIsoUtc(this.formCopiaCreacionDesde);
    const c1 = localDatetimeToIsoUtc(this.formCopiaCreacionHasta);
    if (this.formCopiaCreacionDesde.trim() && !c0) return 'Fecha/hora de creación «desde» no válida.';
    if (this.formCopiaCreacionHasta.trim() && !c1) return 'Fecha/hora de creación «hasta» no válida.';
    if (c0 && c1 && new Date(c0).getTime() > new Date(c1).getTime())
      return 'En fecha de creación, «desde» no puede ser posterior a «hasta».';

    const m0 = localDatetimeToIsoUtc(this.formCopiaActualizacionDesde);
    const m1 = localDatetimeToIsoUtc(this.formCopiaActualizacionHasta);
    if (this.formCopiaActualizacionDesde.trim() && !m0) return 'Fecha/hora de actualización «desde» no válida.';
    if (this.formCopiaActualizacionHasta.trim() && !m1) return 'Fecha/hora de actualización «hasta» no válida.';
    if (m0 && m1 && new Date(m0).getTime() > new Date(m1).getTime())
      return 'En fecha de actualización, «desde» no puede ser posterior a «hasta».';

    return null;
  }

  private copiaFiltrosApiFromForm(): TrabajoCopiaFiltrosApi {
    const parseMb = (raw: string): number | null => {
      const t = raw.trim();
      if (!t) return null;
      const n = Number(t.replace(',', '.'));
      if (!Number.isFinite(n) || n < 0) return null;
      return Math.round(n * BYTES_PER_MIB);
    };
    return {
      copiaTamanoMinBytes: parseMb(this.formCopiaTamMinMb),
      copiaTamanoMaxBytes: parseMb(this.formCopiaTamMaxMb),
      copiaCreacionDesdeUtc: localDatetimeToIsoUtc(this.formCopiaCreacionDesde),
      copiaCreacionHastaUtc: localDatetimeToIsoUtc(this.formCopiaCreacionHasta),
      copiaActualizacionDesdeUtc: localDatetimeToIsoUtc(this.formCopiaActualizacionDesde),
      copiaActualizacionHastaUtc: localDatetimeToIsoUtc(this.formCopiaActualizacionHasta),
    };
  }

  saveJob(): void {
    const err = this.validateForSave();
    if (err) {
      this.toastService.show(err, 'error');
      return;
    }

    const ruta = this.formOrigenRuta.trim();
    const filtrosExclusiones = this.formOrigenExclusiones.trim();
    const copiaFiltros = this.copiaFiltrosApiFromForm();
    const basePayload = {
      nombre: this.formName.trim(),
      descripcion: this.formDescription.trim(),
      destinoId: this.formDestinoId!,
      scriptPreId: this.formScriptPreId,
      scriptPostId: this.formScriptPostId,
      preDetenerEnFallo: this.formPreDetenerEnFallo,
      postDetenerEnFallo: this.formPostDetenerEnFallo,
      cronExpression: this.formSchedule,
      activo: this.formEnabled,
      ...copiaFiltros,
    };

    this.saving.set(true);
    const editId = this.editingJobId();

    this.originsService
      .asegurarPorRuta(ruta, filtrosExclusiones)
      .pipe(
        switchMap((origen) => {
          if (editId != null) {
            return this.jobsService.update(editId, {
              ...basePayload,
              origenId: origen.id,
              sincronizarScripts: true,
              sincronizarFiltrosCopia: true,
            });
          }
          return this.jobsService.create({
            ...basePayload,
            origenId: origen.id,
          });
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => this.router.navigate(['/trabajos']),
        error: () => {},
      });
  }

  nextStep(): void {
    if (this.currentStep === 2) {
      const r = this.formOrigenRuta.trim();
      if (!r) {
        this.toastService.show('Indica la ruta de la carpeta.', 'error');
        return;
      }
      if (this.origenPathValidated()) {
        this.advanceStepOrSave();
        return;
      }
      this.runValidarOrigenEnServidor(() => this.advanceStepOrSave());
      return;
    }
    this.advanceStepOrSave();
  }

  /** Avanza de paso o ejecuta guardado si ya estamos en el último. */
  private advanceStepOrSave(): void {
    if (this.currentStep < this.steps.length - 1) {
      this.currentStep++;
    } else {
      this.saveJob();
    }
  }

  onOrigenRutaChange(): void {
    this.origenPathValidated.set(false);
  }

  comprobarOrigenPath(): void {
    this.runValidarOrigenEnServidor(() => {
      this.toastService.show('La carpeta existe en el servidor.', 'success');
    });
  }

  /**
   * Valida la carpeta en la API (Path + Directory.Exists en el servidor).
   * @param onSuccess se ejecuta solo si la validación fue correcta.
   */
  private runValidarOrigenEnServidor(onSuccess: () => void): void {
    const r = this.formOrigenRuta.trim();
    if (!r) {
      this.toastService.show('Escribe la ruta de la carpeta.', 'error');
      return;
    }
    this.checkingOrigenPath.set(true);
    this.originsService.validarRuta(r).subscribe({
      next: (res) => {
        this.checkingOrigenPath.set(false);
        this.formOrigenRuta = res.ruta;
        this.origenPathValidated.set(true);
        onSuccess();
      },
      error: () => this.checkingOrigenPath.set(false),
    });
  }

  prevStep(): void {
    if (this.currentStep > 0) {
      this.currentStep--;
    }
  }

  goToStep(index: number): void {
    this.currentStep = index;
  }

  toggleWeekday(day: number): void {
    const i = this.formScheduleWeekdays.indexOf(day);
    if (i >= 0) {
      const next = this.formScheduleWeekdays.filter((_, idx) => idx !== i);
      if (next.length === 0) return;
      this.formScheduleWeekdays = next;
    } else {
      this.formScheduleWeekdays = [...this.formScheduleWeekdays, day].sort((a, b) => a - b);
    }
  }

  isWeekdaySelected(day: number): boolean {
    return this.formScheduleWeekdays.includes(day);
  }

  pad2(n: number): string {
    return String(n).padStart(2, '0');
  }

  /** Ajusta el minuto al rango 0–59 al escribir en el campo numérico. */
  onScheduleMinuteInput(value: unknown): void {
    if (value === '' || value === null || value === undefined) {
      this.formScheduleMinute = 0;
      return;
    }
    const n = typeof value === 'number' ? value : parseInt(String(value), 10);
    if (Number.isNaN(n)) {
      this.formScheduleMinute = 0;
      return;
    }
    this.formScheduleMinute = Math.min(59, Math.max(0, Math.trunc(n)));
  }

  cancel(): void {
    this.router.navigate(['/trabajos']);
  }
}
