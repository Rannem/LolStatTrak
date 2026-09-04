import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminUser, AuditEntry, ClubOverview } from '../../core/models/models';
import { AdminService } from '../../core/services/admin.service';
import { AuthService } from '../../core/services/auth.service';
import { AvatarPipe } from '../../core/pipes/avatar.pipe';
import { AuditLogComponent } from '../../shared/audit-log/audit-log.component';

type Tab = 'pending' | 'users' | 'clubs' | 'audit';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [DatePipe, RouterLink, AuditLogComponent, AvatarPipe],
  template: `
    <div class="page fade-up">
      <div class="page-header">
        <div>
          <div class="eyebrow">Global administration</div>
          <h1>Admin</h1>
        </div>
      </div>

      <nav class="tabs">
        <button class="btn-ghost" [class.active]="tab() === 'pending'" (click)="tab.set('pending')">
          Sign-up requests
          @if (pending().length > 0) {
            <span class="count">{{ pending().length }}</span>
          }
        </button>
        <button class="btn-ghost" [class.active]="tab() === 'users'" (click)="tab.set('users')">Users</button>
        <button class="btn-ghost" [class.active]="tab() === 'clubs'" (click)="tab.set('clubs')">Clubs</button>
        <button class="btn-ghost" [class.active]="tab() === 'audit'" (click)="tab.set('audit'); loadAudit()">Audit log</button>
      </nav>

      @if (error()) {
        <div class="card error">{{ error() }}</div>
      }

      @switch (tab()) {
        @case ('pending') {
          <div class="card">
            <h2>Waiting to get in</h2>
            <p class="muted small">New Discord sign-ups land here. Approve to let them use the app, reject to lock them out.</p>
            @if (pending().length === 0) {
              <div class="empty">No pending requests 🎉</div>
            } @else {
              <ul class="clean list">
                @for (u of pending(); track u.id) {
                  <li>
                    <img class="avatar" [src]="u.avatarUrl | avatar: 64" [alt]="u.discordUsername" />
                    <div class="grow">
                      <div>{{ u.discordUsername }}</div>
                      <div class="dim small">registered {{ u.createdAt | date: 'MMM d, HH:mm' }}</div>
                    </div>
                    <button class="btn-accent btn-sm" (click)="approve(u)">Approve</button>
                    <button class="btn-danger btn-sm" (click)="reject(u)">Reject</button>
                  </li>
                }
              </ul>
            }
          </div>
        }

        @case ('users') {
          <div class="card">
            <h2>All users <span class="muted">({{ users().length }})</span></h2>
            <ul class="clean list">
              @for (u of users(); track u.id) {
                <li>
                  <img class="avatar avatar-sm" [src]="u.avatarUrl | avatar: 32" [alt]="u.discordUsername" />
                  <div class="grow">
                    <span>{{ u.discordUsername }}</span>
                    @if (u.riotGameName) {
                      <span class="dim small"> · {{ u.riotGameName }}#{{ u.riotTagLine }}</span>
                    }
                  </div>
                  @if (u.isGlobalAdmin) {
                    <span class="badge gold">Global admin</span>
                  }
                  <span class="badge" [class.blue]="u.accessStatus === 'Approved'" [class.red]="u.accessStatus === 'Rejected'">{{ u.accessStatus }}</span>
                  @if (!u.isGlobalAdmin) {
                    @if (u.accessStatus !== 'Approved') {
                      <button class="btn-accent btn-sm" (click)="approve(u)">Approve</button>
                    } @else {
                      <button class="btn-ghost btn-sm" (click)="reject(u)">Revoke</button>
                    }
                    @if (u.id !== auth.user()?.id) {
                      <button class="btn-danger btn-sm" (click)="deleteUser(u)">Delete</button>
                    }
                  }
                </li>
              }
            </ul>
          </div>
        }

        @case ('clubs') {
          <div class="card">
            <h2>All clubs <span class="muted">({{ clubs().length }})</span></h2>
            @if (clubs().length === 0) {
              <div class="empty">No clubs yet.</div>
            } @else {
              <ul class="clean list">
                @for (c of clubs(); track c.id) {
                  <li>
                    <div class="grow">
                      <a [routerLink]="['/clubs', c.id]"><strong>{{ c.name }}</strong></a>
                      <div class="dim small">
                        owner {{ c.ownerUsername }} · {{ c.memberCount }} members · {{ c.matchCount }} matches ·
                        created {{ c.createdAt | date: 'MMM d, y' }}
                      </div>
                    </div>
                    <button class="btn-danger btn-sm" (click)="deleteClub(c)">Delete</button>
                  </li>
                }
              </ul>
            }
          </div>
        }

        @case ('audit') {
          <div class="card">
            <h2>Global audit log</h2>
            <app-audit-log [entries]="audit()" [showClub]="true" />
          </div>
        }
      }
    </div>
  `,
  styles: `
    .tabs {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
      margin-bottom: 1.25rem;

      .active {
        border-color: var(--gold-3);
        color: var(--gold-1);
        background: rgba(200, 155, 60, 0.12);
      }
    }
    .count {
      display: inline-grid;
      place-items: center;
      min-width: 1.3rem;
      height: 1.3rem;
      padding: 0 0.35rem;
      border-radius: 999px;
      background: var(--danger);
      color: #fff;
      font-size: 0.65rem;
    }
    .small {
      font-size: 0.78rem;
    }
    .list li {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.6rem 0;
      flex-wrap: wrap;
    }
    .list li + li {
      border-top: 1px solid var(--border);
    }
    .grow {
      flex: 1;
      min-width: 160px;
    }
    .error {
      border-color: var(--danger);
      color: var(--danger);
      margin-bottom: 1.25rem;
    }
  `,
})
export class AdminComponent implements OnInit {
  private readonly admin = inject(AdminService);
  protected readonly auth = inject(AuthService);

  protected readonly tab = signal<Tab>('pending');
  protected readonly pending = signal<AdminUser[]>([]);
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly clubs = signal<ClubOverview[]>([]);
  protected readonly audit = signal<AuditEntry[]>([]);
  protected readonly error = signal('');

  ngOnInit(): void {
    this.reloadUsers();
    this.admin.getClubs().subscribe((c) => this.clubs.set(c));
  }

  private reloadUsers(): void {
    this.admin.getPendingUsers().subscribe((p) => this.pending.set(p));
    this.admin.getUsers().subscribe((u) => this.users.set(u));
  }

  protected loadAudit(): void {
    this.admin.getAudit().subscribe((a) => this.audit.set(a));
  }

  approve(u: AdminUser): void {
    this.admin.approveUser(u.id).subscribe({ next: () => this.reloadUsers(), error: (e) => this.fail(e) });
  }

  reject(u: AdminUser): void {
    if (!confirm(`Reject ${u.discordUsername}? They'll be locked out of the app.`)) return;
    this.admin.rejectUser(u.id).subscribe({ next: () => this.reloadUsers(), error: (e) => this.fail(e) });
  }

  deleteUser(u: AdminUser): void {
    if (!confirm(`Permanently delete ${u.discordUsername} and all their stats? This can't be undone.`)) return;
    this.admin.deleteUser(u.id).subscribe({ next: () => this.reloadUsers(), error: (e) => this.fail(e) });
  }

  deleteClub(c: ClubOverview): void {
    if (!confirm(`Delete club "${c.name}" including all lobbies and matches? This can't be undone.`)) return;
    this.admin.deleteClub(c.id).subscribe({
      next: () => this.admin.getClubs().subscribe((cl) => this.clubs.set(cl)),
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown): void {
    this.error.set((e as { error?: { title?: string } })?.error?.title ?? 'That didn\u2019t work. Try again.');
  }
}
