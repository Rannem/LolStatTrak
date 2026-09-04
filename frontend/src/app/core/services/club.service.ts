import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Club, ClubDetail, ClubMember, Lobby } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ClubService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/clubs`;

  getMyClubs() {
    return this.http.get<Club[]>(this.base, { withCredentials: true });
  }

  getClub(clubId: string) {
    return this.http.get<ClubDetail>(`${this.base}/${clubId}`, { withCredentials: true });
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
}
