import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Club } from '../../core/models/models';
import { ClubService } from '../../core/services/club.service';

@Component({
  selector: 'app-clubs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <h1>Your clubs</h1>

    <ul>
      @for (club of clubs(); track club.id) {
        <li>
          <a [routerLink]="['/clubs', club.id]">{{ club.name }}</a>
          (invite code: {{ club.inviteCode }})
        </li>
      }
    </ul>

    <h2>Create a club</h2>
    <input [(ngModel)]="newClubName" placeholder="Club name" />
    <button (click)="createClub()">Create</button>

    <h2>Join by invite code</h2>
    <input [(ngModel)]="joinInviteCode" placeholder="Invite code" />
    <button (click)="joinByInvite()">Join</button>
  `,
})
export class ClubsComponent implements OnInit {
  private readonly clubService = inject(ClubService);

  protected readonly clubs = signal<Club[]>([]);
  protected newClubName = '';
  protected joinInviteCode = '';

  ngOnInit(): void {
    this.reload();
  }

  private reload(): void {
    this.clubService.getMyClubs().subscribe((clubs) => this.clubs.set(clubs));
  }

  createClub(): void {
    if (!this.newClubName.trim()) return;
    this.clubService.createClub(this.newClubName).subscribe(() => {
      this.newClubName = '';
      this.reload();
    });
  }

  joinByInvite(): void {
    if (!this.joinInviteCode.trim()) return;
    this.clubService.joinByInvite(this.joinInviteCode).subscribe(() => {
      this.joinInviteCode = '';
      this.reload();
    });
  }
}
