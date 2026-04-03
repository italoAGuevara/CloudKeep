import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './login.component.html',
    styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {
    private router = inject(Router);
    private authService = inject(AuthService);
    private cdr = inject(ChangeDetectorRef);

    password = '';
    showPassword = false;
    rememberPassword = false;
    error = '';
    loading = false;

    constructor() {
        if (!this.authService.requireAuth() || this.authService.isLoggedIn()) {
            this.router.navigate(['/']);
        }
    }

    ngOnInit(): void {
        const saved = this.authService.getSavedPasswordForLoginForm();
        if (saved) {
            this.password = saved;
            this.rememberPassword = true;
        }
    }

    login() {
        this.loading = true;
        this.error = '';

        this.authService.login(this.password, this.rememberPassword).pipe(
            finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            })
        ).subscribe({
            next: () => this.router.navigate(['/']),
            error: (err) => {
                this.loading = false;
                this.error = err?.error?.message ?? err?.error?.Message ?? (err?.status === 401 ? 'Contraseña incorrecta' : 'Error al iniciar sesión');
                this.cdr.detectChanges();
            },
        });
    }
}
