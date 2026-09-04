import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LobbyPlayer } from '../../core/models/models';
import { LobbyHubService } from '../../core/services/lobby-hub.service';
import { LobbyService } from '../../core/services/lobby.service';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h1>Lobby</h1>

    <button (click)="join()">Join lobby</button>
    <button (click)="roll()">Roll teams &amp; champions</button>
    <button (click)="markPlayed()">Mark as played (fetch stats)</button>

    <table>
      <thead>
        <tr><th>User</th><th>Team</th><th>Champion</th></tr>
      </thead>
      <tbody>
        @for (player of players(); track player.userId) {
          <tr>
            <td>{{ player.userId }}</td>
            <td>{{ player.assignedTeam }}</td>
            <td>{{ player.assignedChampionId }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
})
export class LobbyComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly lobbyService = inject(LobbyService);
  private readonly hub = inject(LobbyHubService);

  protected lobbyId = '';
  protected readonly players = signal<LobbyPlayer[]>([]);

  async ngOnInit(): Promise<void> {
    this.lobbyId = this.route.snapshot.paramMap.get('lobbyId') ?? '';
    await this.hub.connectAndJoin(this.lobbyId);
    this.hub.lobbyRolled$.subscribe((players) => this.players.set(players));
  }

  async ngOnDestroy(): Promise<void> {
    await this.hub.leave(this.lobbyId);
  }

  join(): void {
    this.lobbyService.join(this.lobbyId).subscribe((players) => this.players.set(players));
  }

  roll(): void {
    this.lobbyService.roll(this.lobbyId).subscribe((players) => this.players.set(players));
  }

  markPlayed(): void {
    this.lobbyService.markPlayed(this.lobbyId).subscribe();
  }
}
