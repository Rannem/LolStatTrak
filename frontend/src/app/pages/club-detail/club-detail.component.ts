import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ClubMember } from '../../core/models/models';
import { ClubService } from '../../core/services/club.service';
import { LobbyService } from '../../core/services/lobby.service';

@Component({
  selector: 'app-club-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h1>Club</h1>

    <button (click)="createLobby()">Start a new lobby</button>

    <h2>Pending join requests</h2>
    <ul>
      @for (request of pendingRequests(); track request.userId) {
        <li>
          {{ request.userId }}
          <button (click)="approve(request.userId)">Approve</button>
        </li>
      }
    </ul>

    <h2>Banned champions</h2>
    <input [(ngModel)]="bannedChampionIdsCsv" placeholder="e.g. 1,2,3" />
    <button (click)="saveBans()">Save</button>
  `,
})
export class ClubDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly clubService = inject(ClubService);
  private readonly lobbyService = inject(LobbyService);

  protected clubId = '';
  protected readonly pendingRequests = signal<ClubMember[]>([]);
  protected bannedChampionIdsCsv = '';

  ngOnInit(): void {
    this.clubId = this.route.snapshot.paramMap.get('clubId') ?? '';
    this.reloadJoinRequests();
    this.clubService.getBannedChampions(this.clubId).subscribe((ids) => {
      this.bannedChampionIdsCsv = ids.join(',');
    });
  }

  private reloadJoinRequests(): void {
    this.clubService.getJoinRequests(this.clubId).subscribe((requests) => this.pendingRequests.set(requests));
  }

  approve(userId: string): void {
    this.clubService.approveJoinRequest(this.clubId, userId).subscribe(() => this.reloadJoinRequests());
  }

  saveBans(): void {
    const ids = this.bannedChampionIdsCsv
      .split(',')
      .map((s) => parseInt(s.trim(), 10))
      .filter((n) => !isNaN(n));
    this.clubService.setBannedChampions(this.clubId, ids).subscribe();
  }

  createLobby(): void {
    this.lobbyService.createLobby(this.clubId).subscribe((lobby) => {
      window.location.href = `/lobbies/${lobby.id}`;
    });
  }
}
