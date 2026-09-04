import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { User } from '../models/models';

/**
 * Tracks whether the user is logged in by probing /auth/me on startup,
 * since auth state itself lives in the httpOnly session cookie set by the Discord callback.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _user = signal<User | null | undefined>(undefined); // undefined = not yet checked
  readonly user = computed(() => this._user() ?? null);
  readonly isAuthenticated = computed(() => !!this._user());
  readonly checked = computed(() => this._user() !== undefined);

  loginWithDiscord(): void {
    // Always land on /clubs after a successful login, regardless of which page the
    // button was clicked from (e.g. /login itself, which doesn't know how to react
    // to "now authenticated" on its own).
    const returnUrl = encodeURIComponent('/clubs');
    window.location.href = `${environment.apiBaseUrl}/auth/discord/login?returnUrl=${returnUrl}`;
  }

  logout() {
    return this.http.post(`${environment.apiBaseUrl}/auth/logout`, {}, { withCredentials: true }).pipe(
      tap(() => this._user.set(null)),
    );
  }

  /** Resolves the current user from the session cookie; null when not signed in. */
  checkSession() {
    return this.http.get<User>(`${environment.apiBaseUrl}/auth/me`, { withCredentials: true }).pipe(
      tap((user) => this._user.set(user)),
      catchError(() => {
        this._user.set(null);
        return of(null);
      }),
    );
  }
}
