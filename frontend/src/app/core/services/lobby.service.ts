import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { CorrelationResult, Lobby, LobbyGameMode, LobbyPlayer, LobbyState } from '../models/models';

@Injectable({ providedIn: 'root' })
export class LobbyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/lobbies`;

  createLobby(clubId: string, gameMode: LobbyGameMode, assignChampions: boolean) {
    return this.http.post<Lobby>(this.base, { clubId, gameMode, assignChampions }, { withCredentials: true });
  }

  get(lobbyId: string) {
    return this.http.get<LobbyState>(`${this.base}/${lobbyId}`, { withCredentials: true });
  }

  join(lobbyId: string) {
    return this.http.post<LobbyPlayer[]>(`${this.base}/${lobbyId}/join`, {}, { withCredentials: true });
  }

  addPlayer(lobbyId: string, userId: string) {
    return this.http.post<LobbyPlayer[]>(`${this.base}/${lobbyId}/players`, { userId }, { withCredentials: true });
  }

  removePlayer(lobbyId: string, userId: string) {
    return this.http.delete<LobbyPlayer[]>(`${this.base}/${lobbyId}/players/${userId}`, { withCredentials: true });
  }

  roll(lobbyId: string) {
    return this.http.post<LobbyPlayer[]>(`${this.base}/${lobbyId}/roll`, {}, { withCredentials: true });
  }

  markPlayed(lobbyId: string) {
    return this.http.post<CorrelationResult>(`${this.base}/${lobbyId}/mark-played`, {}, { withCredentials: true });
  }

  syncStats(lobbyId: string) {
    return this.http.post<CorrelationResult>(`${this.base}/${lobbyId}/sync-stats`, {}, { withCredentials: true });
  }
}
