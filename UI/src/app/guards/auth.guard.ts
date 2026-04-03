import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  //return true;

  if (auth.requireAuth() === false) {
    return true;
  }
  if (auth.isLoggedIn()) {
    return true;
  }
  console.log('No está autenticado');
  router.navigate(['/login']);
  return false;
};
