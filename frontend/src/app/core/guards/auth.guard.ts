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

/**
 * Invite links only need a signed-in account — a valid invite is what approves a Pending user,
 * so they must be allowed through before approval. Rejected users still get bounced to /pending
 * (which explains they've been declined). The Riot-link requirement is applied afterwards by
 * `authGuard` when the invite page forwards them into the club.
 */
export const inviteGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map(() => {
      if (!auth.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      if (auth.user()?.accessStatus === 'Rejected' && !auth.isGlobalAdmin()) return router.createUrlTree(['/pending']);
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
