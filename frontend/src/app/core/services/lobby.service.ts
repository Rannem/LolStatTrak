import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Lobby, LobbyPlayer, LobbyState } from '../models/models';

@Injectable({ providedIn: 'root' })
export class LobbyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/lobbies`;

  createLobby(clubId: string) {
    return this.http.post<Lobby>(this.base, { clubId }, { withCredentials: true });
  }

  get(lobbyId: string) {
    return this.http.get<LobbyState>(`${this.base}/${lobbyId}`, { withCredentials: true });
  }

  join(lobbyId: string) {
    return this.http.post<LobbyPlayer[]>(`${this.base}/${lobbyId}/join`, {}, { withCredentials: true });
  }

  roll(lobbyId: string) {
    return this.http.post<LobbyPlayer[]>(`${this.base}/${lobbyId}/roll`, {}, { withCredentials: true });
  }

  markPlayed(lobbyId: string) {
    return this.http.post<{ matchId: string | null }>(`${this.base}/${lobbyId}/mark-played`, {}, { withCredentials: true });
  }
}
