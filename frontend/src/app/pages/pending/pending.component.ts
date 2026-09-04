import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AvatarPipe } from '../../core/pipes/avatar.pipe';

@Component({
  selector: 'app-pending',
  standalone: true,
  imports: [AvatarPipe],
  template: `
    <div class="wrap">
      <div class="card box fade-up">
        @if (auth.user(); as user) {
          <img class="avatar big" [src]="user.avatarUrl | avatar: 128" [alt]="user.discordUsername" />
          <h1>Hey, {{ user.discordUsername }}</h1>

          @if (user.accessStatus === 'Rejected') {
            <p class="muted">Your access request was declined by an admin. If you think that's a mistake, poke them on Discord.</p>
          } @else {
            <div class="badge gold pulse">⏳ Awaiting approval</div>
            <p class="muted">
              Your account is registered but an admin needs to let you in first.
              Give them a nudge on Discord — once approved you'll be able to join clubs and roll lobbies.
            </p>
            <button class="btn-accent" (click)="recheck()" [disabled]="checking">
              {{ checking ? 'Checking…' : 'Check again' }}
            </button>
          }
        }
        <button class="btn-ghost btn-sm" (click)="logout()">Log out</button>
      </div>
    </div>
  `,
  styles: `
    .wrap {
      min-height: calc(100vh - 60px);
      display: grid;
      place-items: center;
      padding: 2rem 1rem;
    }
    .box {
      width: min(460px, 100%);
      text-align: center;
      padding: 2.5rem 2rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
    }
    .big {
      width: 84px;
      height: 84px;
      border-width: 3px;
      box-shadow: var(--glow-gold);
    }
    h1 {
      font-size: 1.6rem;
      margin: 0;
    }
    .pulse {
      animation: pulse 1.8s ease-in-out infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.55; }
    }
  `,
})
export class PendingComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected checking = false;

  recheck(): void {
    this.checking = true;
    this.auth.checkSession().subscribe(() => {
      this.checking = false;
      if (this.auth.isApproved()) this.router.navigateByUrl('/clubs');
    });
  }

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigateByUrl('/login'));
  }
}
