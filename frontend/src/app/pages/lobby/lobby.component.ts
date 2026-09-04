import { NgTemplateOutlet } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subscription } from 'rxjs';
import { CorrelationResult, Lobby, LobbyPlayer, Team, gameModeLabel } from '../../core/models/models';
import { AuthService } from '../../core/services/auth.service';
import { ChampionService } from '../../core/services/champion.service';
import { LobbyHubService } from '../../core/services/lobby-hub.service';
import { LobbyService } from '../../core/services/lobby.service';
import { AvatarPipe } from '../../core/pipes/avatar.pipe';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [RouterLink, NgTemplateOutlet, AvatarPipe],
  template: `
    <div class="page fade-up">
      @if (lobby(); as lobby) {
        <a [routerLink]="['/clubs', lobby.clubId]" class="back">‹ Back to club</a>
      }

      <div class="page-header">
        <div>
          <div class="eyebrow">{{ modeLabel(lobby()?.gameMode) || 'Custom game' }}{{ lobby() && !lobby()!.assignChampions ? ' · teams only' : '' }}</div>
          <h1>Lobby</h1>
          <div class="row small muted">
            <span class="badge" [class.blue]="lobby()?.status === 'Open'" [class.gold]="lobby()?.status === 'Rolled'" [class.green]="lobby()?.status === 'Played'">{{ lobby()?.status ?? '…' }}</span>
            <span>{{ players().length }} player{{ players().length === 1 ? '' : 's' }}</span>
            <span class="live" [class.on]="connected()">● {{ connected() ? 'live' : 'connecting' }}</span>
          </div>
        </div>

        <div class="row">
          @if (!amIn() && !isPlayed()) {
            <button class="btn-accent" (click)="join()" [disabled]="busy()">Join lobby</button>
          }
          @if (!isPlayed()) {
            <button class="btn-primary" (click)="roll()" [disabled]="busy() || players().length === 0">
              {{ rolling() ? '⏳' : '🎲' }} {{ isRolled() ? 'Re-roll' : (assignChampions() ? 'Roll teams & champions' : 'Roll teams') }}
            </button>
          }
          @if (isRolled() && !isPlayed()) {
            <button class="btn-ghost" (click)="markPlayed()" [disabled]="busy()">Mark as played</button>
          }
          @if (isPlayed() && !matchFound()) {
            <button class="btn-ghost" (click)="syncStats()" [disabled]="busy()">↻ Sync stats</button>
          }
        </div>
      </div>

      @if (error()) {
        <div class="card error">{{ error() }}</div>
      }
      @if (notice()) {
        <div class="card notice">{{ notice() }}</div>
      }

      @if (isRolled()) {
        <div class="teams">
          <section class="team blue">
            <header><span class="team-name">Blue Team</span><span class="muted">{{ team('Blue').length }}</span></header>
            <div class="team-players">
              @for (p of team('Blue'); track p.userId) {
                <ng-container *ngTemplateOutlet="playerCard; context: { $implicit: p }" />
              }
            </div>
          </section>

          <div class="vs">VS</div>

          <section class="team red">
            <header><span class="team-name">Red Team</span><span class="muted">{{ team('Red').length }}</span></header>
            <div class="team-players">
              @for (p of team('Red'); track p.userId) {
                <ng-container *ngTemplateOutlet="playerCard; context: { $implicit: p }" />
              }
            </div>
          </section>
        </div>
      } @else {
        <div class="card">
          <h2>Waiting for players</h2>
          @if (players().length === 0) {
            <div class="empty">Nobody's in yet. Share this page's link with your club and hit Join.</div>
          } @else {
            <ul class="clean waiting">
              @for (p of players(); track p.userId) {
                <li>
                  <img class="avatar" [src]="p.avatarUrl | avatar: 64" [alt]="p.discordUsername" />
                  <span>{{ p.discordUsername }}</span>
                  @if (p.userId === auth.user()?.id) {
                    <span class="badge blue">you</span>
                  }
                </li>
              }
            </ul>
          }
        </div>
      }
    </div>

    <ng-template #playerCard let-p>
      @let champ = champions.get(p.assignedChampionId);
      <div class="player-card" [class.no-champ]="!champ" [style.background-image]="champ ? 'url(' + champ.splashUrl + ')' : ''">
        <div class="player-card-shade"></div>
        <div class="player-card-body">
          <div class="who">
            <img class="avatar avatar-sm" [src]="p.avatarUrl | avatar: 32" [alt]="p.discordUsername" />
            <span class="pname">{{ p.discordUsername }}</span>
            @if (p.userId === auth.user()?.id) {
              <span class="badge blue">you</span>
            }
            @if (!p.riotGameName) {
              <a routerLink="/profile" class="badge unlinked" title="No Riot account linked — stats won't be tracked">no riot id</a>
            }
          </div>
          @if (champ) {
            <div class="champ">
              <img class="champ-icon" [src]="champ.iconUrl" [alt]="champ.name" decoding="async" fetchpriority="high" />
              <div>
                <div class="champ-name">{{ champ.name }}</div>
                <div class="champ-title">{{ champ.title }}</div>
              </div>
            </div>
          } @else if (p.assignedChampionId) {
            <div class="champ"><div class="champ-name muted">Champion #{{ p.assignedChampionId }}</div></div>
          } @else if (p.riotGameName) {
            <div class="riot-id muted">{{ p.riotGameName }}<span class="dim">#{{ p.riotTagLine }}</span></div>
          }
        </div>
      </div>
    </ng-template>
  `,
  styles: `
    .back {
      display: inline-block;
      margin-bottom: 1rem;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.12em;
    }

    .small {
      font-size: 0.8rem;
    }

    .live {
      color: var(--text-dim);
      &.on {
        color: var(--success);
      }
    }

    .error {
      border-color: var(--danger);
      color: var(--danger);
      margin-bottom: 1.25rem;
    }

    .notice {
      border-color: var(--gold-4);
      color: var(--gold-1);
      margin-bottom: 1.25rem;
    }

    .badge.green {
      color: var(--success);
      border-color: rgba(60, 200, 120, 0.5);
    }

    .waiting li {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 0;
    }

    .waiting li + li {
      border-top: 1px solid var(--border);
    }

    .teams {
      display: grid;
      grid-template-columns: 1fr auto 1fr;
      gap: 1.25rem;
      align-items: start;

      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }

    .vs {
      align-self: center;
      font-family: 'Cinzel', serif;
      font-weight: 700;
      font-size: 1.6rem;
      color: var(--gold-3);
      text-shadow: var(--glow-gold);
      padding: 0 0.5rem;

      @media (max-width: 860px) {
        text-align: center;
      }
    }

    .team {
      border-radius: var(--radius-lg);
      padding: 1rem;
      background: var(--surface);
      border: 1px solid var(--border);
      box-shadow: var(--shadow);

      header {
        display: flex;
        justify-content: space-between;
        align-items: baseline;
        margin-bottom: 0.9rem;
        padding-bottom: 0.5rem;
        border-bottom: 2px solid;
      }

      .team-name {
        font-family: 'Cinzel', serif;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.14em;
        font-size: 0.95rem;
      }

      &.blue {
        border-color: rgba(30, 144, 255, 0.45);
        header {
          border-color: var(--blue-team);
        }
        .team-name {
          color: #7fc0ff;
        }
      }

      &.red {
        border-color: rgba(232, 64, 87, 0.45);
        header {
          border-color: var(--red-team);
        }
        .team-name {
          color: #ff8a9b;
        }
      }
    }

    .team-players {
      display: grid;
      gap: 0.75rem;
    }

    .player-card {
      position: relative;
      min-height: 150px;
      border-radius: var(--radius);
      overflow: hidden;
      border: 1px solid var(--gold-4);
      background-color: var(--bg-3);
      background-size: cover;
      background-position: right 25%;
      transition: transform 0.15s, box-shadow 0.15s;

      &:hover {
        transform: translateY(-2px);
        box-shadow: var(--glow-gold);
      }

      &.no-champ {
        min-height: 0;
        background: linear-gradient(135deg, var(--bg-3), var(--surface));
      }
    }

    .riot-id {
      font-size: 0.8rem;
    }

    .player-card-shade {
      position: absolute;
      inset: 0;
      background: linear-gradient(90deg, rgba(1, 10, 19, 0.95) 0%, rgba(1, 10, 19, 0.7) 45%, rgba(1, 10, 19, 0.05) 100%);
    }

    .player-card-body {
      position: relative;
      padding: 0.9rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .who {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: var(--text-muted);
    }

    .pname {
      font-weight: 600;
      color: var(--text);
    }

    .unlinked {
      color: var(--danger);
      border-color: rgba(232, 64, 87, 0.5);
      font-size: 0.6rem;
    }

    .champ {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .champ-icon {
      width: 48px;
      height: 48px;
      border-radius: 6px;
      border: 2px solid var(--gold-3);
    }

    .champ-name {
      font-family: 'Cinzel', serif;
      font-weight: 700;
      font-size: 1.1rem;
      color: var(--gold-1);
    }

    .champ-title {
      font-size: 0.72rem;
      color: var(--gold-2);
      text-transform: capitalize;
    }
  `,
})
export class LobbyComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly lobbyService = inject(LobbyService);
  private readonly hub = inject(LobbyHubService);
  protected readonly auth = inject(AuthService);
  protected readonly champions = inject(ChampionService);

  protected lobbyId = '';
  protected readonly lobby = signal<Lobby | null>(null);
  protected readonly players = signal<LobbyPlayer[]>([]);
  protected readonly connected = signal(false);
  protected readonly busy = signal(false);
  protected readonly rolling = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly matchFound = signal(false);
  protected readonly modeLabel = gameModeLabel;

  protected readonly isRolled = computed(() => this.players().some((p) => p.assignedTeam != null));
  protected readonly isPlayed = computed(() => this.lobby()?.status === 'Played');
  protected readonly assignChampions = computed(() => this.lobby()?.assignChampions ?? true);
  protected readonly amIn = computed(() => this.players().some((p) => p.userId === this.auth.user()?.id));

  private subs = new Subscription();

  async ngOnInit(): Promise<void> {
    this.lobbyId = this.route.snapshot.paramMap.get('lobbyId') ?? '';
    this.champions.ensureLoaded();

    this.lobbyService.get(this.lobbyId).subscribe((state) => {
      this.lobby.set(state.lobby);
      this.setPlayers(state.players);
      this.matchFound.set(!!state.matchId);
    });

    this.subs.add(this.hub.lobbyRolled$.subscribe((players) => {
      this.setPlayers(players);
      this.lobby.update((l) => (l ? { ...l, status: 'Rolled' } : l));
    }));
    this.subs.add(this.hub.playerJoined$.subscribe((evt) => {
      if (evt.lobbyId === this.lobbyId) this.setPlayers(evt.players);
    }));
    this.subs.add(this.hub.lobbyPlayed$.subscribe((evt) => {
      if (evt.lobbyId === this.lobbyId) this.applyCorrelation(evt.result);
    }));

    try {
      await this.hub.connectAndJoin(this.lobbyId);
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  async ngOnDestroy(): Promise<void> {
    this.subs.unsubscribe();
    await this.hub.leave(this.lobbyId);
  }

  protected team(team: Team): LobbyPlayer[] {
    return this.players().filter((p) => p.assignedTeam === team);
  }

  join(): void {
    // Optimistic: show myself in the list immediately; the server response / hub event reconciles.
    const me = this.auth.user();
    const before = this.players();
    if (me && !this.amIn()) {
      this.players.update((ps) => [
        ...ps,
        { lobbyId: this.lobbyId, userId: me.id, discordUsername: me.discordUsername, avatarUrl: me.avatarUrl, assignedTeam: null, assignedChampionId: null },
      ]);
    }
    this.run(
      this.lobbyService.join(this.lobbyId),
      (players) => this.setPlayers(players),
      () => this.players.set(before),
    );
  }

  roll(): void {
    this.rolling.set(true);
    this.run(
      this.lobbyService.roll(this.lobbyId),
      (players) => {
        this.setPlayers(players);
        this.lobby.update((l) => (l ? { ...l, status: 'Rolled' } : l));
        this.rolling.set(false);
      },
      () => this.rolling.set(false),
    );
  }

  markPlayed(): void {
    this.run(this.lobbyService.markPlayed(this.lobbyId), (res) => this.applyCorrelation(res));
  }

  syncStats(): void {
    this.run(this.lobbyService.syncStats(this.lobbyId), (res) => this.applyCorrelation(res));
  }

  private applyCorrelation(res: CorrelationResult): void {
    this.lobby.update((l) => (l ? { ...l, status: 'Played' } : l));
    this.notice.set('');
    switch (res.outcome) {
      case 'Found':
        this.matchFound.set(true);
        this.notice.set('Game found — stats have been recorded. Check the club\u2019s Matches tab.');
        break;
      case 'NoLinkedPlayers':
        this.error.set('Nobody in this lobby has linked a Riot account, so the game can\u2019t be looked up. Link one under Profile, then hit Sync stats.');
        break;
      case 'NotFoundYet':
        this.notice.set(
          `No matching game in Riot\u2019s history yet (${res.linkedPlayers}/${res.totalPlayers} players linked). ` +
            'Riot usually needs a few minutes after the game ends — hit Sync stats again shortly.',
        );
        break;
      case 'RiotError':
        this.error.set(`Riot API error while looking up the game${res.detail ? ': ' + res.detail : ''}. Try Sync stats again in a minute.`);
        break;
    }
  }

  private run<T>(obs: Observable<T>, onNext: (v: T) => void, onError?: () => void): void {
    this.busy.set(true);
    this.error.set('');
    obs.subscribe({
      next: (v) => {
        onNext(v);
        this.busy.set(false);
      },
      error: (e: unknown) => {
        onError?.();
        const msg = (e as { error?: { title?: string; detail?: string } })?.error;
        this.error.set(msg?.detail ?? msg?.title ?? 'Something went wrong. Try again.');
        this.busy.set(false);
      },
    });
  }

  /** Sets players and warms the browser cache with their splash art so rolled cards appear together. */
  private setPlayers(players: LobbyPlayer[]): void {
    this.players.set(players);
    this.preloadSplashes(players);
  }

  private readonly preloaded = new Set<string>();

  private preloadSplashes(players: LobbyPlayer[]): void {
    const conn = (navigator as Navigator & { connection?: { saveData?: boolean } }).connection;
    if (conn?.saveData) return;
    for (const p of players) {
      const url = this.champions.get(p.assignedChampionId)?.splashUrl;
      if (!url || this.preloaded.has(url)) continue;
      this.preloaded.add(url);
      const img = new Image();
      img.decoding = 'async';
      img.src = url;
    }
  }
}
