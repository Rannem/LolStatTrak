import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AuditEntry, Club, ClubDetail, ClubMember, ClubOverviewPage, ClubRole, Lobby, MatchSummary } from '../models/models';
import { Observable, finalize, shareReplay, tap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ClubService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/clubs`;

  /** Short-lived cache so a hover-prefetch (or back navigation) renders the club page instantly. */
  private readonly overviewCache = new Map<string, { at: number; data: ClubOverviewPage }>();
  private readonly overviewInflight = new Map<string, Observable<ClubOverviewPage>>();
  private static readonly OVERVIEW_TTL_MS = 10_000;
  /** Cached data younger than this is used as-is without a revalidating refetch (hover → click). */
  private static readonly OVERVIEW_FRESH_MS = 3_000;

  getMyClubs() {
    return this.http.get<Club[]>(this.base, { withCredentials: true });
  }

  getClub(clubId: string) {
    return this.http.get<ClubDetail>(`${this.base}/${clubId}`, { withCredentials: true });
  }

  /** Everything the club page needs in one round-trip. Concurrent callers share one request. */
  getOverview(clubId: string): Observable<ClubOverviewPage> {
    const inflight = this.overviewInflight.get(clubId);
    if (inflight) return inflight;

    const req = this.http.get<ClubOverviewPage>(`${this.base}/${clubId}/overview`, { withCredentials: true }).pipe(
      tap((data) => this.overviewCache.set(clubId, { at: Date.now(), data })),
      finalize(() => this.overviewInflight.delete(clubId)),
      shareReplay(1),
    );
    this.overviewInflight.set(clubId, req);
    return req;
  }

  /** Returns a recent cached overview (if any) for instant first paint, plus whether it's fresh enough to skip revalidation. */
  peekOverview(clubId: string): { data: ClubOverviewPage; fresh: boolean } | null {
    const hit = this.overviewCache.get(clubId);
    if (!hit) return null;
    const age = Date.now() - hit.at;
    if (age >= ClubService.OVERVIEW_TTL_MS) return null;
    return { data: hit.data, fresh: age < ClubService.OVERVIEW_FRESH_MS };
  }

  /** Fire-and-forget warm-up, e.g. on hovering a club card. */
  prefetchOverview(clubId: string): void {
    if (this.peekOverview(clubId) || this.overviewInflight.has(clubId)) return;
    this.getOverview(clubId).subscribe({ error: () => undefined });
  }

  getMembers(clubId: string) {
    return this.http.get<ClubMember[]>(`${this.base}/${clubId}/members`, { withCredentials: true });
  }

  getLobbies(clubId: string) {
    return this.http.get<Lobby[]>(`${this.base}/${clubId}/lobbies`, { withCredentials: true });
  }

  createClub(name: string) {
    return this.http.post<Club>(this.base, { name }, { withCredentials: true });
  }

  joinByInvite(inviteCode: string) {
    return this.http.post<Club>(`${this.base}/join/invite`, { inviteCode }, { withCredentials: true });
  }

  requestJoin(clubId: string) {
    return this.http.post(`${this.base}/${clubId}/join-requests`, {}, { withCredentials: true });
  }

  getJoinRequests(clubId: string) {
    return this.http.get<ClubMember[]>(`${this.base}/${clubId}/join-requests`, { withCredentials: true });
  }

  approveJoinRequest(clubId: string, userId: string) {
    return this.http.post(`${this.base}/${clubId}/join-requests/${userId}/approve`, {}, { withCredentials: true });
  }

  getBannedChampions(clubId: string) {
    return this.http.get<number[]>(`${this.base}/${clubId}/banned-champions`, { withCredentials: true });
  }

  setBannedChampions(clubId: string, championIds: number[]) {
    return this.http.put(`${this.base}/${clubId}/banned-champions`, { championIds }, { withCredentials: true });
  }

  getMatches(clubId: string) {
    return this.http.get<MatchSummary[]>(`${this.base}/${clubId}/matches`, { withCredentials: true });
  }

  deleteMatch(clubId: string, matchId: string) {
    return this.http.delete(`${this.base}/${clubId}/matches/${matchId}`, { withCredentials: true });
  }

  deleteLobby(clubId: string, lobbyId: string) {
    return this.http.delete(`${this.base}/${clubId}/lobbies/${lobbyId}`, { withCredentials: true });
  }

  getAudit(clubId: string) {
    return this.http.get<AuditEntry[]>(`${this.base}/${clubId}/audit`, { withCredentials: true });
  }

  setMemberRole(clubId: string, userId: string, role: ClubRole) {
    return this.http.put(`${this.base}/${clubId}/members/${userId}/role`, { role }, { withCredentials: true });
  }

  removeMember(clubId: string, userId: string) {
    return this.http.delete(`${this.base}/${clubId}/members/${userId}`, { withCredentials: true });
  }

  deleteClub(clubId: string) {
    return this.http.delete(`${this.base}/${clubId}`, { withCredentials: true });
  }
}
