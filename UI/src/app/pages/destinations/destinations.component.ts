import { NgClass } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CreateDestinoPayload,
  DestinationsService,
  DestinoRow,
  DestinationType,
  tipoDestinoUiToApi,
  UpdateDestinoPayload,
} from '../../services/destinations.service';
import { ToastService } from '../../services/toast.service';

type S3ConfigMode = 'bucket_region' | 'credentials';

@Component({
  selector: 'app-destinations',
  standalone: true,
  imports: [FormsModule, NgClass],
  templateUrl: './destinations.component.html',
})
export class DestinationsComponent implements OnInit {
  readonly destSvc = inject(DestinationsService);
  private readonly toast = inject(ToastService);

  showModal = signal(false);
  editingDest = signal<DestinoRow | null>(null);
  saving = signal(false);

  formName = '';
  formType: DestinationType = 's3';
  formS3Mode: S3ConfigMode = 'bucket_region';
  formBucket = '';
  formRegion = 'us-east-1';
  formAccessKeyId = '';
  formSecretAccessKey = '';
  formFolderId = '';
  formGoogleServiceAccountEmail = '';
  formGooglePrivateKey = '';

  testingConnection = signal(false);
  testResult = signal<{ success: boolean; message: string } | null>(null);

  ngOnInit(): void {
    this.destSvc.loadAll();
  }

  openCreate(): void {
    this.editingDest.set(null);
    this.formName = '';
    this.formType = 's3';
    this.formS3Mode = 'bucket_region';
    this.formBucket = '';
    this.formRegion = 'us-east-1';
    this.formAccessKeyId = '';
    this.formSecretAccessKey = '';
    this.formFolderId = '';
    this.formGoogleServiceAccountEmail = '';
    this.formGooglePrivateKey = '';
    this.showModal.set(true);
    this.testResult.set(null);
  }

  openEdit(d: DestinoRow): void {
    this.editingDest.set(d);
    this.formName = d.name;
    this.formType = d.type;
    if (d.type === 's3' && (d.accessKeyId ?? '').trim()) {
      this.formS3Mode = 'credentials';
      this.formAccessKeyId = d.accessKeyId;
      this.formBucket = '';
      this.formRegion = 'us-east-1';
    } else {
      this.formS3Mode = 'bucket_region';
      this.formAccessKeyId = '';
      this.formBucket = d.type === 's3' ? (d.bucketName ?? '') : '';
      this.formRegion =
        d.type === 's3' && (d.region ?? '').trim() ? (d.region ?? '').trim() : 'us-east-1';
    }
    this.formSecretAccessKey = '';
    this.formFolderId = d.type === 'google_drive' ? d.idCarpeta : '';
    this.formGoogleServiceAccountEmail =
      d.type === 'google_drive' ? (d.serviceAccountEmail ?? '') : '';
    this.formGooglePrivateKey = '';
    this.showModal.set(true);
    this.testResult.set(null);
  }

  testConnection(): void {
    this.testResult.set(null);
    if (this.formType === 's3') {
      if (this.formS3Mode === 'bucket_region') {
        const bucket = this.formBucket?.trim();
        const region = this.formRegion?.trim();
        if (!bucket || !region) {
          this.testResult.set({
            success: false,
            message: 'Completa Bucket y Región.',
          });
          return;
        }
      } else {
        const accessKey = this.formAccessKeyId?.trim();
        const secretKey = this.formSecretAccessKey?.trim();
        if (!accessKey || !secretKey) {
          this.testResult.set({
            success: false,
            message: 'Completa Access Key ID y Secret Access Key.',
          });
          return;
        }
      }
    } else {
      const folderId = this.formFolderId?.trim();
      const email = this.formGoogleServiceAccountEmail?.trim();
      const key = this.formGooglePrivateKey?.trim();
      if (!folderId) {
        this.testResult.set({ success: false, message: 'Completa el ID de carpeta.' });
        return;
      }
      if (!email || !key) {
        this.testResult.set({
          success: false,
          message: 'Completa el correo de la Service Account y la clave privada.',
        });
        return;
      }
    }
    this.testingConnection.set(true);
    setTimeout(() => {
      this.testingConnection.set(false);
      if (this.formType === 's3') {
        this.testResult.set({
          success: true,
          message: 'Conexión con S3 verificada (simulado).',
        });
      } else {
        this.testResult.set({
          success: true,
          message: 'Conexión con Google Drive verificada (simulado).',
        });
      }
    }, 1200);
  }

  save(): void {
    const nombre = this.formName.trim();
    if (!nombre) {
      this.toast.show('El nombre es obligatorio.', 'error');
      return;
    }

    const edit = this.editingDest();
    if (!edit) {
      const createPayload = this.tryBuildCreatePayload(nombre);
      if (!createPayload) return;
      this.saving.set(true);
      this.destSvc
        .create(createPayload)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => this.closeModal(),
        });
      return;
    }

    if (this.formType === 'google_drive' && !this.formFolderId.trim()) {
      this.toast.show('IDCarpeta (ID de carpeta) es obligatorio para Google Drive.', 'error');
      return;
    }

    const updatePayload = this.tryBuildUpdatePayload(nombre, edit);
    if (!updatePayload) return;

    this.saving.set(true);
    this.destSvc
      .update(edit.id, updatePayload)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.closeModal(),
      });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingDest.set(null);
  }

  deleteDest(d: DestinoRow): void {
    if (!confirm(`¿Eliminar destino "${d.name}"?`)) return;
    this.destSvc.deleteById(d.id).subscribe();
  }

  typeLabel(type: DestinationType): string {
    return type === 's3' ? 'Amazon S3' : 'Google Drive';
  }

  typeBadgeClass(type: DestinationType): string {
    return type === 's3' ? 'bg-warning text-dark' : 'bg-info';
  }

  get modalTitle(): string {
    return this.editingDest() ? 'Editar destino' : 'Nuevo destino';
  }

  private tryBuildCreatePayload(nombre: string): CreateDestinoPayload | null {
    const tipoApi = tipoDestinoUiToApi(this.formType);
    if (this.formType === 'google_drive') {
      const idCarpeta = this.formFolderId.trim();
      const serviceAccountEmail = this.formGoogleServiceAccountEmail.trim();
      const privateKey = this.formGooglePrivateKey.trim();
      if (!idCarpeta) {
        this.toast.show('IDCarpeta (ID de carpeta) es obligatorio para Google Drive.', 'error');
        return null;
      }
      if (!serviceAccountEmail || !privateKey) {
        this.toast.show('Completa correo de la cuenta de servicio y clave privada para Google Drive.', 'error');
        return null;
      }
      return { nombre, tipoApi, idCarpeta, serviceAccountEmail, privateKey };
    }
    if (this.formS3Mode === 'bucket_region') {
      const bucketName = this.formBucket.trim();
      const region = this.formRegion.trim();
      if (!bucketName || !region) {
        this.toast.show('Completa bucket y región para S3.', 'error');
        return null;
      }
      return { nombre, tipoApi, bucketName, region };
    }
    const accessKeyId = this.formAccessKeyId.trim();
    const secretAccessKey = this.formSecretAccessKey.trim();
    if (!accessKeyId || !secretAccessKey) {
      this.toast.show('Completa Access Key ID y Secret Access Key para S3.', 'error');
      return null;
    }
    return { nombre, tipoApi, accessKeyId, secretAccessKey };
  }

  private tryBuildUpdatePayload(nombre: string, edit: DestinoRow): UpdateDestinoPayload | null {
    const payload: UpdateDestinoPayload = {
      nombre,
      tipoApi: tipoDestinoUiToApi(this.formType),
    };

    if (this.formType === 'google_drive') {
      payload.idCarpeta = this.formFolderId.trim();
      const email = this.formGoogleServiceAccountEmail.trim();
      const pk = this.formGooglePrivateKey.trim();
      if (email !== (edit.serviceAccountEmail ?? '')) payload.serviceAccountEmail = email;
      if (pk) payload.privateKey = pk;
      return payload;
    }

    if (this.formS3Mode === 'bucket_region') {
      const bucketName = this.formBucket.trim();
      const region = this.formRegion.trim();
      if (!bucketName || !region) {
        this.toast.show('Completa bucket y región para S3.', 'error');
        return null;
      }
      payload.bucketName = bucketName;
      payload.region = region;
      return payload;
    }

    const accessKeyId = this.formAccessKeyId.trim();
    if (!accessKeyId) {
      this.toast.show('Access Key ID es obligatorio en modo claves.', 'error');
      return null;
    }
    payload.accessKeyId = accessKeyId;
    const sk = this.formSecretAccessKey.trim();
    if (sk) payload.secretAccessKey = sk;
    return payload;
  }
}
