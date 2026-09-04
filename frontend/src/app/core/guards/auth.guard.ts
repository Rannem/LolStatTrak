import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Confirms the session cookie is valid AND the account has been approved by a global admin.
 * Unauthenticated → /login, authenticated-but-pending/rejected → /pending.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.checkSession().pipe(
    map(() => {
      if (!auth.isAuthenticated()) return router.createUrlTree(['/login']);
      if (!auth.isApproved()) return router.createUrlTree(['/pending']);
      return true;
    }),
  );
};

/** Signed in but not yet approved — the only place such users may land. */
export const pendingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.checkSession().pipe(
    map(() => {
      if (!auth.isAuthenticated()) return router.createUrlTree(['/login']);
      if (auth.isApproved()) return router.createUrlTree(['/clubs']);
      return true;
    }),
  );
};

export const globalAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.checkSession().pipe(
    map(() => (auth.isGlobalAdmin() ? true : router.createUrlTree(['/clubs']))),
  );
};
