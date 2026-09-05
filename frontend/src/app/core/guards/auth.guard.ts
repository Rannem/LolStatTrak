import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Confirms the session cookie is valid, the account has been approved by a global admin, AND
 * a Riot account is linked. Unauthenticated → /login, pending/rejected → /pending, no Riot
 * link yet → /profile (setup is mandatory before using the rest of the app). In every case the
 * originally requested URL is carried along as `returnUrl` so the user lands back where they
 * meant to go (e.g. an invite link) once each step is satisfied.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map(() => {
      if (!auth.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      if (!auth.isApproved()) return router.createUrlTree(['/pending'], { queryParams: { returnUrl: state.url } });
      if (!auth.user()?.riotLinked && !state.url.startsWith('/profile')) {
        return router.createUrlTree(['/profile'], { queryParams: { returnUrl: state.url } });
      }
      return true;
    }),
  );
};

/** Signed in but not yet approved — the only place such users may land. */
export const pendingGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map(() => {
      if (!auth.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      if (auth.isApproved()) return router.createUrlTree(['/clubs']);
      return true;
    }),
  );
};

export const globalAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map(() => (auth.isGlobalAdmin() ? true : router.createUrlTree(['/clubs']))),
  );
};
