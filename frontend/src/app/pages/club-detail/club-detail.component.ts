import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuditEntry, ClubDetail, ClubMember, ClubRole, Lobby, MatchSummary } from '../../core/models/models';
import { AuthService } from '../../core/services/auth.service';
import { ChampionService } from '../../core/services/champion.service';
import { ClubService } from '../../core/services/club.service';
import { LobbyService } from '../../core/services/lobby.service';
import { AuditLogComponent } from '../../shared/audit-log/audit-log.component';
import { ChampionPickerComponent } from '../../shared/champion-picker/champion-picker.component';

type Tab = 'play' | 'matches' | 'members' | 'audit';

@Component({
  selector: 'app-club-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, ChampionPickerComponent, AuditLogComponent],
  template: `
    <div class="page fade-up">
      <a routerLink="/clubs" class="back">‹ All clubs</a>

      @if (club(); as club) {
        <div class="page-header">
          <div>
            <div class="eyebrow">Club · you are {{ club.myRole ?? 'a global admin' }}</div>
            <h1>{{ club.name }}</h1>
            <div class="muted small">
              Invite code <span class="code">{{ club.inviteCode }}</span>
              <button class="btn-ghost btn-sm copy" (click)="copyInvite(club.inviteCode)">{{ copied() ? 'Copied!' : 'Copy' }}</button>
            </div>
          </div>
          <button class="btn-primary" (click)="createLobby()" [disabled]="busy()">🎲 Start a new lobby</button>
        </div>

        <nav class="tabs">
          <button class="btn-ghost" [class.active]="tab() === 'play'" (click)="tab.set('play')">Play</button>
          <button class="btn-ghost" [class.active]="tab() === 'matches'" (click)="tab.set('matches')">
            Matches <span class="muted">{{ matches().length }}</span>
          </button>
          <button class="btn-ghost" [class.active]="tab() === 'members'" (click)="tab.set('members')">
            Members <span class="muted">{{ members().length }}</span>
            @if (pendingRequests().length > 0) {
              <span class="count">{{ pendingRequests().length }}</span>
            }
          </button>
          @if (club.canAdminister) {
            <button class="btn-ghost" [class.active]="tab() === 'audit'" (click)="tab.set('audit'); loadAudit()">Audit</button>
          }
        </nav>

        @if (error()) {
          <div class="card error">{{ error() }}</div>
        }

        @switch (tab()) {
          @case ('play') {
            <div class="grid layout">
              <div class="card">
                <h2>Lobbies</h2>
                @if (lobbies().length === 0) {
                  <div class="empty">No lobbies yet. Start one and get everyone to join.</div>
                } @else {
                  <ul class="clean rows">
                    @for (lobby of lobbies(); track lobby.id) {
                      <li>
                        <a [routerLink]="['/lobbies', lobby.id]" class="lobby-row">
                          <span class="badge" [class.blue]="lobby.status === 'Open'" [class.gold]="lobby.status === 'Rolled'">{{ lobby.status }}</span>
                          <span class="muted">{{ lobby.createdAt | date: 'MMM d, HH:mm' }}</span>
                          <span class="chev">›</span>
                        </a>
                        @if (club.canAdminister) {
                          <button class="btn-danger btn-sm" title="Delete lobby" (click)="deleteLobby(lobby)">✕</button>
                        }
                      </li>
                    }
                  </ul>
                }
              </div>

              <div class="card">
                <h2>Banned champions</h2>
                @if (club.canManage) {
                  <p class="muted small">Click a champion to ban / unban it from this club's rolls.</p>
                } @else {
                  <p class="muted small">Only the club's mods and admins can change the ban list.</p>
                }
                <app-champion-picker
                  [selected]="bans()"
                  [readonly]="!club.canManage"
                  (selectedChange)="bans.set($event); bansDirty.set(true)" />
                @if (club.canManage) {
                  <div class="row save-row">
                    <button class="btn-primary" (click)="saveBans()" [disabled]="!bansDirty() || busy()">Save bans</button>
                    @if (bansSaved()) {
                      <span class="muted small">Saved ✓</span>
                    }
                  </div>
                }
              </div>
            </div>
          }

          @case ('matches') {
            <div class="card">
              <h2>Tracked matches</h2>
              @if (matches().length === 0) {
                <div class="empty">
                  No matches yet. After a game, open the lobby and hit <em>Mark as played</em> — players need a
                  <a routerLink="/profile">linked Riot account</a> for stats to be found.
                </div>
              } @else {
                <div class="stack">
                  @for (m of matches(); track m.id) {
                    <article class="match">
                      <header>
                        <span class="dim small">{{ m.playedAt | date: 'MMM d, y · HH:mm' }}</span>
                        <span class="dim small mono">{{ m.riotMatchId }}</span>
                        @if (club.canAdminister) {
                          <button class="btn-danger btn-sm" (click)="deleteMatch(m)">Remove game</button>
                        }
                      </header>
                      <div class="match-teams">
                        @for (side of ['Blue', 'Red']; track side) {
                          <div class="side" [class.blue]="side === 'Blue'" [class.red]="side === 'Red'" [class.won]="teamWon(m, side)">
                            <div class="side-title">{{ side }} <span>{{ teamWon(m, side) ? 'Victory' : 'Defeat' }}</span></div>
                            @for (p of teamOf(m, side); track p.userId) {
                              @let champ = champions.get(p.championId);
                              <div class="pline">
                                @if (champ) {
                                  <img class="champ-icon" [src]="champ.iconUrl" [alt]="champ.name" [title]="champ.name" />
                                }
                                <span class="pname">{{ p.discordUsername }}</span>
                                <span class="kda">{{ p.kills }}/{{ p.deaths }}/{{ p.assists }}</span>
                              </div>
                            }
                          </div>
                        }
                      </div>
                    </article>
                  }
                </div>
              }
            </div>
          }

          @case ('members') {
            <div class="grid layout">
              <div class="card">
                <h2>Members <span class="muted">({{ members().length }})</span></h2>
                <ul class="clean rows">
                  @for (member of members(); track member.userId) {
                    <li class="member">
                      <img class="avatar avatar-sm" [src]="member.avatarUrl || fallbackAvatar" [alt]="member.discordUsername" />
                      <div class="grow">
                        <div>{{ member.discordUsername }}</div>
                        @if (member.riotGameName) {
                          <div class="dim small">{{ member.riotGameName }}#{{ member.riotTagLine }}</div>
                        } @else {
                          <div class="dim small">no Riot account linked</div>
                        }
                      </div>
                      <span class="badge" [class.gold]="member.role !== 'Member'">{{ member.role }}</span>
                      @if (member.role !== 'Owner' && member.userId !== auth.user()?.id) {
                        @if (club.canAdminister) {
                          @if (member.role === 'Member') {
                            <button class="btn-ghost btn-sm" (click)="setRole(member, 'Mod')">Make mod</button>
                          }
                          @if (member.role === 'Mod') {
                            <button class="btn-ghost btn-sm" (click)="setRole(member, 'Member')">Demote</button>
                          }
                        }
                        @if (club.isOwner) {
                          @if (member.role !== 'Admin') {
                            <button class="btn-ghost btn-sm" (click)="setRole(member, 'Admin')">Make admin</button>
                          } @else {
                            <button class="btn-ghost btn-sm" (click)="setRole(member, 'Mod')">Demote</button>
                          }
                        }
                        @if (club.canAdminister && (member.role !== 'Admin' || club.isOwner)) {
                          <button class="btn-danger btn-sm" (click)="removeMember(member)">Kick</button>
                        }
                      }
                    </li>
                  }
                </ul>
              </div>

              <div class="stack">
                @if (club.canManage) {
                  <div class="card" [class.highlight]="pendingRequests().length > 0">
                    <h2>Pending join requests</h2>
                    @if (pendingRequests().length === 0) {
                      <div class="empty">None right now.</div>
                    } @else {
                      <ul class="clean rows">
                        @for (request of pendingRequests(); track request.userId) {
                          <li class="member">
                            <img class="avatar avatar-sm" [src]="request.avatarUrl || fallbackAvatar" [alt]="request.discordUsername" />
                            <span class="grow">{{ request.discordUsername }}</span>
                            <button class="btn-accent btn-sm" (click)="approve(request.userId)">Approve</button>
                          </li>
                        }
                      </ul>
                    }
                  </div>
                }

                <div class="card">
                  <h2>Roles</h2>
                  <ul class="clean roles small muted">
                    <li><span class="badge">Member</span> join lobbies, roll, view stats</li>
                    <li><span class="badge gold">Mod</span> + approve join requests, edit bans</li>
                    <li><span class="badge gold">Admin</span> + remove games/lobbies, kick, promote mods, audit</li>
                    <li><span class="badge gold">Owner</span> + promote admins, delete the club</li>
                  </ul>
                </div>

                @if (club.isOwner) {
                  <div class="card danger-zone">
                    <h2>Danger zone</h2>
                    <button class="btn-danger" (click)="deleteClub()">Delete this club</button>
                  </div>
                } @else if (club.myRole) {
                  <div class="card">
                    <button class="btn-ghost btn-sm" (click)="leaveClub()">Leave club</button>
                  </div>
                }
              </div>
            </div>
          }

          @case ('audit') {
            <div class="card">
              <h2>Audit log</h2>
              <app-audit-log [entries]="audit()" />
            </div>
          }
        }
      }
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
    .layout {
      grid-template-columns: minmax(0, 3fr) minmax(280px, 2fr);
      align-items: start;

      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }
    .small {
      font-size: 0.8rem;
    }
    .mono {
      font-family: ui-monospace, Consolas, monospace;
    }
    .copy {
      margin-left: 0.5rem;
    }
    .rows li {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.45rem 0;
      flex-wrap: wrap;
    }
    .rows li + li {
      border-top: 1px solid var(--border);
    }
    .lobby-row {
      flex: 1;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.2rem 0.25rem;
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
    .grow {
      flex: 1;
      min-width: 120px;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .highlight {
      border-color: var(--blue-3);
      box-shadow: var(--shadow), var(--glow-blue);
    }
    .save-row {
      margin-top: 1rem;
    }
    .error {
      border-color: var(--danger);
      color: var(--danger);
      margin-bottom: 1.25rem;
    }
    .roles li {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      padding: 0.3rem 0;
    }
    .danger-zone {
      border-color: rgba(232, 64, 87, 0.5);
    }

    .match {
      border: 1px solid var(--border);
      border-radius: var(--radius);
      overflow: hidden;
      background: rgba(0, 0, 0, 0.25);

      header {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.5rem 0.9rem;
        border-bottom: 1px solid var(--border);
        flex-wrap: wrap;

        button {
          margin-left: auto;
        }
      }
    }
    .match-teams {
      display: grid;
      grid-template-columns: 1fr 1fr;

      @media (max-width: 640px) {
        grid-template-columns: 1fr;
      }
    }
    .side {
      padding: 0.6rem 0.9rem;
      border-left: 3px solid transparent;

      &.blue {
        border-left-color: var(--blue-team);
      }
      &.red {
        border-left-color: var(--red-team);
      }
      &.won {
        background: linear-gradient(90deg, rgba(30, 215, 96, 0.08), transparent);
      }
    }
    .side-title {
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.12em;
      color: var(--text-muted);
      margin-bottom: 0.4rem;

      span {
        margin-left: 0.4rem;
        color: var(--gold-2);
      }
    }
    .pline {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.2rem 0;
      font-size: 0.85rem;
    }
    .champ-icon {
      width: 28px;
      height: 28px;
      border-radius: 4px;
      border: 1px solid var(--gold-4);
    }
    .pname {
      flex: 1;
    }
    .kda {
      font-family: ui-monospace, Consolas, monospace;
      color: var(--gold-1);
    }
  `,
})
export class ClubDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly clubService = inject(ClubService);
  private readonly lobbyService = inject(LobbyService);
  protected readonly auth = inject(AuthService);
  protected readonly champions = inject(ChampionService);

  protected clubId = '';
  protected readonly tab = signal<Tab>('play');
  protected readonly club = signal<ClubDetail | null>(null);
  protected readonly members = signal<ClubMember[]>([]);
  protected readonly pendingRequests = signal<ClubMember[]>([]);
  protected readonly lobbies = signal<Lobby[]>([]);
  protected readonly matches = signal<MatchSummary[]>([]);
  protected readonly audit = signal<AuditEntry[]>([]);
  protected readonly bans = signal<number[]>([]);
  protected readonly bansDirty = signal(false);
  protected readonly bansSaved = signal(false);
  protected readonly busy = signal(false);
  protected readonly copied = signal(false);
  protected readonly error = signal('');

  protected readonly fallbackAvatar = 'https://cdn.discordapp.com/embed/avatars/0.png';

  ngOnInit(): void {
    this.clubId = this.route.snapshot.paramMap.get('clubId') ?? '';
    this.champions.ensureLoaded();

    this.clubService.getClub(this.clubId).subscribe({
      next: (club) => {
        this.club.set(club);
        if (club.canManage) this.reloadJoinRequests();
      },
      error: () => this.router.navigateByUrl('/clubs'),
    });
    this.reloadMembers();
    this.reloadLobbies();
    this.clubService.getMatches(this.clubId).subscribe((m) => this.matches.set(m));
    this.clubService.getBannedChampions(this.clubId).subscribe((ids) => this.bans.set(ids));
  }

  private reloadJoinRequests(): void {
    this.clubService.getJoinRequests(this.clubId).subscribe((r) => this.pendingRequests.set(r));
  }

  private reloadMembers(): void {
    this.clubService.getMembers(this.clubId).subscribe((m) => this.members.set(m));
  }

  private reloadLobbies(): void {
    this.clubService.getLobbies(this.clubId).subscribe((l) => this.lobbies.set(l));
  }

  protected loadAudit(): void {
    this.clubService.getAudit(this.clubId).subscribe((a) => this.audit.set(a));
  }

  protected teamOf(m: MatchSummary, side: string) {
    return m.participants.filter((p) => p.team === side);
  }

  protected teamWon(m: MatchSummary, side: string): boolean {
    return this.teamOf(m, side).some((p) => p.win);
  }

  approve(userId: string): void {
    this.clubService.approveJoinRequest(this.clubId, userId).subscribe(() => {
      this.reloadJoinRequests();
      this.reloadMembers();
    });
  }

  setRole(member: ClubMember, role: ClubRole): void {
    this.clubService.setMemberRole(this.clubId, member.userId, role).subscribe({
      next: () => this.reloadMembers(),
      error: (e) => this.fail(e),
    });
  }

  removeMember(member: ClubMember): void {
    if (!confirm(`Kick ${member.discordUsername} from the club?`)) return;
    this.clubService.removeMember(this.clubId, member.userId).subscribe({
      next: () => this.reloadMembers(),
      error: (e) => this.fail(e),
    });
  }

  leaveClub(): void {
    const me = this.auth.user();
    if (!me || !confirm('Leave this club?')) return;
    this.clubService.removeMember(this.clubId, me.id).subscribe({
      next: () => this.router.navigateByUrl('/clubs'),
      error: (e) => this.fail(e),
    });
  }

  deleteClub(): void {
    const name = this.club()?.name ?? 'this club';
    if (!confirm(`Delete "${name}" with all its lobbies and matches? This can't be undone.`)) return;
    this.clubService.deleteClub(this.clubId).subscribe({
      next: () => this.router.navigateByUrl('/clubs'),
      error: (e) => this.fail(e),
    });
  }

  deleteLobby(lobby: Lobby): void {
    if (!confirm('Delete this lobby?')) return;
    this.clubService.deleteLobby(this.clubId, lobby.id).subscribe({
      next: () => this.reloadLobbies(),
      error: (e) => this.fail(e),
    });
  }

  deleteMatch(m: MatchSummary): void {
    if (!confirm('Remove this tracked game and its stats from the club?')) return;
    this.clubService.deleteMatch(this.clubId, m.id).subscribe({
      next: () => this.matches.update((list) => list.filter((x) => x.id !== m.id)),
      error: (e) => this.fail(e),
    });
  }

  saveBans(): void {
    this.busy.set(true);
    this.clubService.setBannedChampions(this.clubId, this.bans()).subscribe({
      next: () => {
        this.bansDirty.set(false);
        this.bansSaved.set(true);
        setTimeout(() => this.bansSaved.set(false), 2500);
        this.busy.set(false);
      },
      error: (e) => {
        this.fail(e);
        this.busy.set(false);
      },
    });
  }

  createLobby(): void {
    this.busy.set(true);
    this.lobbyService.createLobby(this.clubId).subscribe({
      next: (lobby) => this.router.navigate(['/lobbies', lobby.id]),
      error: (e) => {
        this.fail(e);
        this.busy.set(false);
      },
    });
  }

  copyInvite(code: string): void {
    navigator.clipboard?.writeText(code).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1500);
    });
  }

  private fail(e: unknown): void {
    this.error.set((e as { error?: { title?: string } })?.error?.title ?? 'That didn\u2019t work. Try again.');
  }
}
