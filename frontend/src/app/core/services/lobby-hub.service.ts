import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LobbyPlayer } from '../models/models';

/** Wraps the LobbyHub SignalR connection: live join/roll events per lobby, for the current lobby view. */
@Injectable({ providedIn: 'root' })
export class LobbyHubService {
  private connection?: signalR.HubConnection;

  readonly playerJoined$ = new Subject<{ lobbyId: string; userId: string; players: LobbyPlayer[] }>();
  readonly lobbyRolled$ = new Subject<LobbyPlayer[]>();

  async connectAndJoin(lobbyId: string): Promise<void> {
    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${environment.hubBaseUrl}/lobby`, { withCredentials: true })
        .withAutomaticReconnect()
        .build();

      this.connection.on('PlayerJoined', (payload) => this.playerJoined$.next(payload));
      this.connection.on('LobbyRolled', (payload) => this.lobbyRolled$.next(payload));

      await this.connection.start();
    }

    await this.connection.invoke('JoinLobbyGroup', lobbyId);
  }

  async leave(lobbyId: string): Promise<void> {
    await this.connection?.invoke('LeaveLobbyGroup', lobbyId);
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = undefined;
  }
}
