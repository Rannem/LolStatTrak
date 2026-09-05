import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { User } from '../models/models';

/**
 * Tracks whether the user is logged in by probing /auth/me, since auth state itself lives in
 * the httpOnly session cookie set by the Discord callback. The result is cached so route
 * guards resolve synchronously; it's refreshed in the background on tab focus / a timer, and
 * invalidated by the HTTP interceptor on 401/403.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** How long a /me result is trusted before a guard forces a fresh check. */
  private static readonly FRESH_MS = 60_000;
  private static readonly BACKGROUND_REFRESH_MS = 5 * 60_000;

  private readonly _user = signal<User | null | undefined>(undefined); // undefined = not yet checked
  private checkedAt = 0;
  private inflight?: Observable<User | null>;

  readonly user = computed(() => this._user() ?? null);
  readonly isAuthenticated = computed(() => !!this._user());
  readonly checked = computed(() => this._user() !== undefined);
  readonly isApproved = computed(() => {
    const u = this._user();
    return !!u && (u.isGlobalAdmin || u.accessStatus === 'Approved');
  });
  readonly isGlobalAdmin = computed(() => !!this._user()?.isGlobalAdmin);

  constructor() {
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && this._user()) this.refreshInBackground();
      });
      setInterval(() => this._user() && this.refreshInBackground(), AuthService.BACKGROUND_REFRESH_MS);
    }
  }

  linkRiotAccount(gameName: string, tagLine: string) {
    return this.http
      .put<User>(`${environment.apiBaseUrl}/auth/riot-account`, { gameName, tagLine }, { withCredentials: true })
      .pipe(tap((user) => this._user.set(user)));
  }

  unlinkRiotAccount() {
    return this.http
      .delete<User>(`${environment.apiBaseUrl}/auth/riot-account`, { withCredentials: true })
      .pipe(tap((user) => this._user.set(user)));
  }

  loginWithDiscord(returnUrl = '/clubs'): void {
    // Defaults to /clubs after a successful login, but callers (e.g. /login carrying a
    // `returnUrl` query param from a guard redirect) can send the user back to wherever
    // they originally meant to go — like an invite link.
    window.location.href = `${environment.apiBaseUrl}/auth/discord/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  logout() {
    return this.http.post(`${environment.apiBaseUrl}/auth/logout`, {}, { withCredentials: true }).pipe(
      tap(() => this.invalidate()),
    );
  }

  /** Forget the cached session (e.g. after a 401); the next guard will re-probe /me. */
  invalidate(): void {
    this._user.set(null);
    this.checkedAt = 0;
  }

  /** Force the next `ensureSession` to hit the server (e.g. after a 403 — approval may have been revoked). */
  markStale(): void {
    this.checkedAt = 0;
  }

  /**
   * Resolves the current user, returning the cached value synchronously-ish (via `of`) when it
   * was checked recently. Guards use this so navigation doesn't wait on the network.
   */
  ensureSession(): Observable<User | null> {
    const fresh = this._user() !== undefined && Date.now() - this.checkedAt < AuthService.FRESH_MS;
    if (fresh) return of(this.user());
    return this.checkSession();
  }

  private refreshInBackground(): void {
    this.checkSession().subscribe();
  }

  /** Resolves the current user from the session cookie; null when not signed in. De-duplicates concurrent calls. */
  checkSession(): Observable<User | null> {
    if (this.inflight) return this.inflight;
    this.inflight = this.http.get<User>(`${environment.apiBaseUrl}/auth/me`, { withCredentials: true }).pipe(
      tap((user) => {
        this._user.set(user);
        this.checkedAt = Date.now();
      }),
      catchError(() => {
        this._user.set(null);
        this.checkedAt = Date.now();
        return of(null);
      }),
      finalize(() => (this.inflight = undefined)),
      shareReplay(1),
    );
    return this.inflight;
  }
}
