import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Keeps the cached session honest: a 401 means the cookie is gone (→ login), a 403 from an
 * app endpoint usually means approval status changed (→ re-check, which routes to /pending).
 */
export const authErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    tap({
      error: (err: unknown) => {
        const status = (err as { status?: number })?.status;
        if (status === 401 && !req.url.endsWith('/auth/me')) {
          auth.invalidate();
          router.navigateByUrl('/login');
        } else if (status === 403) {
          auth.markStale();
          auth.checkSession().subscribe(() => {
            if (auth.isAuthenticated() && !auth.isApproved()) router.navigateByUrl('/pending');
          });
        }
      },
    }),
  );
};
