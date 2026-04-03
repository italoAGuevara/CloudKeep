import { ChangeDetectorRef, Component, inject, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { validateStrongPassword } from '../../utils/password-policy';

@Component({
    selector: 'app-settings',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './settings.component.html',
    styleUrl: './settings.component.css'
})
export class SettingsComponent {
    private authService = inject(AuthService);
    private toastService = inject(ToastService);
    private cdr = inject(ChangeDetectorRef);
    private ngZone = inject(NgZone);

    /** Valor inicial desde el API (cargado en APP_INITIALIZER). */
    requireAuth = this.authService.requireAuth();
    oldPassword = '';
    newPassword = '';
    confirmPassword = '';

    message = '';
    messageType: 'success' | 'error' = 'success';
    changingPassword = false;

    saveAuthSettings() {
        this.authService.setRequireAuth(this.requireAuth).subscribe({
            next: () => {
                const msg = this.requireAuth ? 'Se requiere contraseña para entrar.' : 'Entrada sin contraseña activada.';
                setTimeout(() => this.toastService.show(msg, 'success'), 0);
            },
            error: () => {
                setTimeout(() => this.toastService.show('No se pudo guardar la configuración.', 'error'), 0);
            },
        });
    }

    changePassword() {
        if (this.newPassword !== this.confirmPassword) {
            this.showMessage('Las contraseñas no coinciden', 'error');
            return;
        }
        const policyError = validateStrongPassword(this.newPassword);
        if (policyError) {
            this.showMessage(policyError, 'error');
            return;
        }
        if (this.oldPassword === this.newPassword) {
            this.showMessage('La nueva contraseña debe ser distinta de la actual.', 'error');
            return;
        }

        this.changingPassword = true;
        this.message = '';

        this.authService.changePassword(this.oldPassword, this.newPassword).subscribe({
            next: () => {
                const msg = 'Contraseña actualizada correctamente';
                this.ngZone.runOutsideAngular(() => {
                    setTimeout(() => {
                        this.changingPassword = false;
                        this.showMessage(msg, 'success');
                        this.oldPassword = '';
                        this.newPassword = '';
                        this.confirmPassword = '';
                        this.cdr.detectChanges();
                        this.ngZone.run(() => this.toastService.show(msg, 'success'));
                    }, 0);
                });
            },
            error: (err) => {
                const apiMsg = err?.error?.message ?? err?.error?.Message;
                const finalMsg = apiMsg ?? (err?.status === 401
                    ? 'Sesión expirada o no autorizado. Inicia sesión de nuevo.'
                    : 'Error al cambiar la contraseña.');
                this.ngZone.runOutsideAngular(() => {
                    setTimeout(() => {
                        this.changingPassword = false;
                        this.showMessage(finalMsg, 'error');
                        this.cdr.detectChanges();
                        this.ngZone.run(() => this.toastService.show(finalMsg, 'error'));
                    }, 0);
                });
            },
        });
    }

    private showMessage(msg: string, type: 'success' | 'error') {
        this.message = msg;
        this.messageType = type;
        setTimeout(() => (this.message = ''), 5000);
    }
}
