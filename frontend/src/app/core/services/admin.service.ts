import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AdminUser, AuditEntry, ClubOverview } from '../models/models';

/** Global-admin-only endpoints: sign-up approvals, club oversight, global audit. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;
  private readonly opts = { withCredentials: true };

  getPendingUsers() {
    return this.http.get<AdminUser[]>(`${this.base}/users/pending`, this.opts);
  }

  getUsers() {
    return this.http.get<AdminUser[]>(`${this.base}/users`, this.opts);
  }

  approveUser(userId: string) {
    return this.http.post(`${this.base}/users/${userId}/approve`, {}, this.opts);
  }

  rejectUser(userId: string) {
    return this.http.post(`${this.base}/users/${userId}/reject`, {}, this.opts);
  }

  deleteUser(userId: string) {
    return this.http.delete(`${this.base}/users/${userId}`, this.opts);
  }

  getClubs() {
    return this.http.get<ClubOverview[]>(`${this.base}/clubs`, this.opts);
  }

  deleteClub(clubId: string) {
    return this.http.delete(`${this.base}/clubs/${clubId}`, this.opts);
  }

  getAudit() {
    return this.http.get<AuditEntry[]>(`${this.base}/audit`, this.opts);
  }
}
