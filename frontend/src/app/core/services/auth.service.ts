import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Club } from '../models/models';

/**
 * Tracks whether the user is logged in by probing an authenticated endpoint on startup,
 * since auth state itself lives in the httpOnly session cookie set by the Discord callback.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _isAuthenticated = signal<boolean | null>(null); // null = not yet checked
  readonly isAuthenticated = computed(() => this._isAuthenticated() === true);
  readonly checked = computed(() => this._isAuthenticated() !== null);

  loginWithDiscord(): void {
    // Always land on /clubs after a successful login, regardless of which page the
    // button was clicked from (e.g. /login itself, which doesn't know how to react
    // to "now authenticated" on its own).
    const returnUrl = encodeURIComponent('/clubs');
    window.location.href = `${environment.apiBaseUrl}/auth/discord/login?returnUrl=${returnUrl}`;
  }

  logout() {
    return this.http.post(`${environment.apiBaseUrl}/auth/logout`, {}, { withCredentials: true }).pipe(
      tap(() => this._isAuthenticated.set(false)),
    );
  }

  /** Calls a cheap authenticated endpoint (my clubs) purely to confirm the session cookie is valid. */
  checkSession() {
    return this.http.get<Club[]>(`${environment.apiBaseUrl}/clubs`, { withCredentials: true }).pipe(
      tap(() => this._isAuthenticated.set(true)),
      catchError(() => {
        this._isAuthenticated.set(false);
        return of([]);
      }),
    );
  }
}
