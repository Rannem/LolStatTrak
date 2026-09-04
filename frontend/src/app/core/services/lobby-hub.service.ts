import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CorrelationResult, Lobby, LobbyPlayer } from '../models/models';

/**
 * Wraps the LobbyHub SignalR connection. Two kinds of groups:
 * - lobby groups: live join/roll/played events for the lobby page
 * - club groups: lobby created/changed/deleted events for the club page
 */
@Injectable({ providedIn: 'root' })
export class LobbyHubService {
  private connection?: signalR.HubConnection;
  private starting?: Promise<void>;

  readonly playerJoined$ = new Subject<{ lobbyId: string; userId: string; players: LobbyPlayer[] }>();
  readonly lobbyRolled$ = new Subject<LobbyPlayer[]>();
  readonly lobbyPlayed$ = new Subject<{ lobbyId: string; result: CorrelationResult }>();
  readonly clubLobbyUpserted$ = new Subject<Lobby>();
  readonly clubLobbyDeleted$ = new Subject<{ clubId: string; lobbyId: string }>();

  async connectAndJoin(lobbyId: string): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke('JoinLobbyGroup', lobbyId);
  }

  async leave(lobbyId: string): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('LeaveLobbyGroup', lobbyId);
  }

  async joinClub(clubId: string): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke('JoinClubGroup', clubId);
  }

  async leaveClub(clubId: string): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('LeaveClubGroup', clubId);
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = undefined;
    this.starting = undefined;
  }

  private ensureConnected(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return Promise.resolve();
    if (this.starting) return this.starting;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubBaseUrl}/lobby`, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    this.connection.on('PlayerJoined', (payload) => this.playerJoined$.next(payload));
    this.connection.on('LobbyRolled', (payload) => this.lobbyRolled$.next(payload));
    this.connection.on('LobbyPlayed', (payload) => this.lobbyPlayed$.next(payload));
    this.connection.on('ClubLobbyUpserted', (payload) => this.clubLobbyUpserted$.next(payload));
    this.connection.on('ClubLobbyDeleted', (payload) => this.clubLobbyDeleted$.next(payload));

    this.starting = this.connection.start().finally(() => (this.starting = undefined));
    return this.starting;
  }
}
