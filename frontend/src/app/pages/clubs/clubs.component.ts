import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Club } from '../../core/models/models';
import { ClubService } from '../../core/services/club.service';

@Component({
  selector: 'app-clubs',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="page fade-up">
      <div class="page-header">
        <div>
          <div class="eyebrow">Your groups</div>
          <h1>Clubs</h1>
        </div>
      </div>

      <div class="grid clubs-grid">
        @for (club of clubs(); track club.id) {
          <a class="card card-link club-card" [routerLink]="['/clubs', club.id]" (mouseenter)="prefetch(club.id)" (focus)="prefetch(club.id)">
            <div class="club-icon">{{ club.name.charAt(0).toUpperCase() }}</div>
            <div class="club-body">
              <h3>{{ club.name }}</h3>
              <div class="muted small">Invite code <span class="code">{{ club.inviteCode }}</span></div>
            </div>
            <span class="chev">›</span>
          </a>
        } @empty {
          <div class="card empty full">You're not in any clubs yet — create one or join with an invite code below.</div>
        }
      </div>

      <div class="grid grid-2 actions">
        <div class="card">
          <h2>Create a club</h2>
          <p class="muted small">You'll be the owner and can approve join requests and manage bans.</p>
          <form class="field" (ngSubmit)="createClub()">
            <input [(ngModel)]="newClubName" name="clubName" placeholder="e.g. Friday ARAM Squad" />
            <button class="btn-primary" type="submit" [disabled]="!newClubName.trim() || busy()">Create</button>
          </form>
        </div>

        <div class="card">
          <h2>Join by invite code</h2>
          <p class="muted small">Got a code from a friend? Paste it here to join instantly.</p>
          <form class="field" (ngSubmit)="joinByInvite()">
            <input [(ngModel)]="joinInviteCode" name="inviteCode" placeholder="Invite code" />
            <button class="btn-accent" type="submit" [disabled]="!joinInviteCode.trim() || busy()">Join</button>
          </form>
          @if (joinError()) {
            <p class="error small">{{ joinError() }}</p>
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    .clubs-grid {
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      margin-bottom: 2rem;
    }

    .full {
      grid-column: 1 / -1;
    }

    .club-card {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem 1.25rem;
    }

    .club-icon {
      width: 52px;
      height: 52px;
      flex-shrink: 0;
      display: grid;
      place-items: center;
      border-radius: 50%;
      font-family: 'Cinzel', serif;
      font-weight: 700;
      font-size: 1.4rem;
      color: var(--gold-1);
      border: 2px solid var(--gold-3);
      background: radial-gradient(circle at 30% 30%, var(--blue-4), var(--bg-0));
    }

    .club-body {
      flex: 1;
      min-width: 0;

      h3 {
        font-size: 1.05rem;
        margin-bottom: 0.15rem;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }

    .chev {
      font-size: 1.6rem;
      color: var(--gold-4);
    }

    .small {
      font-size: 0.8rem;
    }

    .error {
      color: var(--danger);
      margin: 0.75rem 0 0;
    }
  `,
})
export class ClubsComponent implements OnInit {
  private readonly clubService = inject(ClubService);

  protected readonly clubs = signal<Club[]>([]);
  protected readonly busy = signal(false);
  protected readonly joinError = signal('');
  protected newClubName = '';
  protected joinInviteCode = '';

  ngOnInit(): void {
    this.reload();
  }

  private reload(): void {
    this.clubService.getMyClubs().subscribe((clubs) => this.clubs.set(clubs));
  }

  prefetch(clubId: string): void {
    this.clubService.prefetchOverview(clubId);
  }

  createClub(): void {
    if (!this.newClubName.trim()) return;
    this.busy.set(true);
    this.clubService.createClub(this.newClubName.trim()).subscribe({
      next: () => {
        this.newClubName = '';
        this.reload();
      },
      complete: () => this.busy.set(false),
      error: () => this.busy.set(false),
    });
  }

  joinByInvite(): void {
    if (!this.joinInviteCode.trim()) return;
    this.busy.set(true);
    this.joinError.set('');
    this.clubService.joinByInvite(this.joinInviteCode.trim()).subscribe({
      next: () => {
        this.joinInviteCode = '';
        this.reload();
        this.busy.set(false);
      },
      error: () => {
        this.joinError.set('No club found with that invite code.');
        this.busy.set(false);
      },
    });
  }
}
