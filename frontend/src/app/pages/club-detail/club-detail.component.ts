import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ClubDetail, ClubMember, Lobby } from '../../core/models/models';
import { ClubService } from '../../core/services/club.service';
import { LobbyService } from '../../core/services/lobby.service';
import { ChampionPickerComponent } from '../../shared/champion-picker/champion-picker.component';

@Component({
  selector: 'app-club-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, ChampionPickerComponent],
  template: `
    <div class="page fade-up">
      <a routerLink="/clubs" class="back">‹ All clubs</a>

      @if (club(); as club) {
        <div class="page-header">
          <div>
            <div class="eyebrow">Club · you are {{ club.myRole }}</div>
            <h1>{{ club.name }}</h1>
            <div class="muted small">
              Invite code <span class="code">{{ club.inviteCode }}</span>
              <button class="btn-ghost btn-sm copy" (click)="copyInvite(club.inviteCode)">{{ copied() ? 'Copied!' : 'Copy' }}</button>
            </div>
          </div>
          <button class="btn-primary" (click)="createLobby()" [disabled]="busy()">🎲 Start a new lobby</button>
        </div>
      }

      <div class="grid layout">
        <div class="stack">
          <div class="card">
            <h2>Lobbies</h2>
            @if (lobbies().length === 0) {
              <div class="empty">No lobbies yet. Start one and get everyone to join.</div>
            } @else {
              <ul class="clean lobby-list">
                @for (lobby of lobbies(); track lobby.id) {
                  <li>
                    <a [routerLink]="['/lobbies', lobby.id]" class="lobby-row">
                      <span class="badge" [class.blue]="lobby.status === 'Open'" [class.gold]="lobby.status === 'Rolled'">{{ lobby.status }}</span>
                      <span class="muted">{{ lobby.createdAt | date: 'MMM d, HH:mm' }}</span>
                      <span class="chev">›</span>
                    </a>
                  </li>
                }
              </ul>
            }
          </div>

          <div class="card">
            <h2>Banned champions</h2>
            @if (club()?.canManage) {
              <p class="muted small">Click a champion to ban / unban it from this club's rolls.</p>
            } @else {
              <p class="muted small">Only the club's mods and admins can change the ban list.</p>
            }
            <app-champion-picker
              [selected]="bans()"
              [readonly]="!club()?.canManage"
              (selectedChange)="bans.set($event); bansDirty.set(true)" />
            @if (club()?.canManage) {
              <div class="row save-row">
                <button class="btn-primary" (click)="saveBans()" [disabled]="!bansDirty() || busy()">Save bans</button>
                @if (bansSaved()) {
                  <span class="muted small">Saved ✓</span>
                }
              </div>
            }
          </div>
        </div>

        <div class="stack">
          @if (club()?.canManage && pendingRequests().length > 0) {
            <div class="card highlight">
              <h2>Pending join requests</h2>
              <ul class="clean member-list">
                @for (request of pendingRequests(); track request.userId) {
                  <li>
                    <img class="avatar avatar-sm" [src]="request.avatarUrl || fallbackAvatar" [alt]="request.discordUsername" />
                    <span class="grow">{{ request.discordUsername }}</span>
                    <button class="btn-accent btn-sm" (click)="approve(request.userId)">Approve</button>
                  </li>
                }
              </ul>
            </div>
          }

          <div class="card">
            <h2>Members <span class="muted">({{ members().length }})</span></h2>
            <ul class="clean member-list">
              @for (member of members(); track member.userId) {
                <li>
                  <img class="avatar avatar-sm" [src]="member.avatarUrl || fallbackAvatar" [alt]="member.discordUsername" />
                  <span class="grow">{{ member.discordUsername }}</span>
                  <span class="badge" [class.gold]="member.role !== 'Member'">{{ member.role }}</span>
                </li>
              }
            </ul>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: `
    .back {
      display: inline-block;
      margin-bottom: 1rem;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.12em;
    }

    .layout {
      grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);

      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }

    .small {
      font-size: 0.8rem;
    }

    .copy {
      margin-left: 0.5rem;
    }

    .lobby-list li + li {
      border-top: 1px solid var(--border);
    }

    .lobby-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.65rem 0.25rem;
      color: inherit;

      .chev {
        margin-left: auto;
        color: var(--gold-4);
        font-size: 1.3rem;
      }

      &:hover {
        color: var(--gold-1);
        .chev {
          color: var(--gold-2);
        }
      }
    }

    .member-list li {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.45rem 0;
    }

    .member-list li + li {
      border-top: 1px solid var(--border);
    }

    .grow {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .highlight {
      border-color: var(--blue-3);
      box-shadow: var(--shadow), var(--glow-blue);
    }

    .save-row {
      margin-top: 1rem;
    }
  `,
})
export class ClubDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly clubService = inject(ClubService);
  private readonly lobbyService = inject(LobbyService);

  protected clubId = '';
  protected readonly club = signal<ClubDetail | null>(null);
  protected readonly members = signal<ClubMember[]>([]);
  protected readonly pendingRequests = signal<ClubMember[]>([]);
  protected readonly lobbies = signal<Lobby[]>([]);
  protected readonly bans = signal<number[]>([]);
  protected readonly bansDirty = signal(false);
  protected readonly bansSaved = signal(false);
  protected readonly busy = signal(false);
  protected readonly copied = signal(false);

  protected readonly fallbackAvatar = 'https://cdn.discordapp.com/embed/avatars/0.png';

  ngOnInit(): void {
    this.clubId = this.route.snapshot.paramMap.get('clubId') ?? '';

    this.clubService.getClub(this.clubId).subscribe((club) => {
      this.club.set(club);
      if (club.canManage) this.reloadJoinRequests();
    });
    this.clubService.getMembers(this.clubId).subscribe((m) => this.members.set(m));
    this.clubService.getLobbies(this.clubId).subscribe((l) => this.lobbies.set(l));
    this.clubService.getBannedChampions(this.clubId).subscribe((ids) => this.bans.set(ids));
  }

  private reloadJoinRequests(): void {
    this.clubService.getJoinRequests(this.clubId).subscribe((r) => this.pendingRequests.set(r));
  }

  approve(userId: string): void {
    this.clubService.approveJoinRequest(this.clubId, userId).subscribe(() => {
      this.reloadJoinRequests();
      this.clubService.getMembers(this.clubId).subscribe((m) => this.members.set(m));
    });
  }

  saveBans(): void {
    this.busy.set(true);
    this.clubService.setBannedChampions(this.clubId, this.bans()).subscribe({
      next: () => {
        this.bansDirty.set(false);
        this.bansSaved.set(true);
        setTimeout(() => this.bansSaved.set(false), 2500);
      },
      complete: () => this.busy.set(false),
      error: () => this.busy.set(false),
    });
  }

  createLobby(): void {
    this.busy.set(true);
    this.lobbyService.createLobby(this.clubId).subscribe({
      next: (lobby) => this.router.navigate(['/lobbies', lobby.id]),
      error: () => this.busy.set(false),
    });
  }

  copyInvite(code: string): void {
    navigator.clipboard?.writeText(code).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1500);
    });
  }
}
