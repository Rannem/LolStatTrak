import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ClubService } from '../../core/services/club.service';

/**
 * Landing page for a shareable invite link (`/invite/:code`). By the time this component
 * renders, `authGuard` has already ensured the visitor is logged in, approved and has a Riot
 * account linked — bouncing them through /login, /pending and /profile (each carrying this
 * URL as `returnUrl`) as needed first. All that's left here is to redeem the code and land
 * them straight in the club.
 */
@Component({
  selector: 'app-invite',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="wrap">
      <div class="card box fade-up">
        @if (error(); as message) {
          <h1>Couldn't join that club</h1>
          <p class="muted">{{ message }}</p>
          <a class="btn-primary" routerLink="/clubs">Back to clubs</a>
        } @else {
          <div class="spinner" aria-hidden="true"></div>
          <h1>Joining club…</h1>
          <p class="muted">Hang tight, this only takes a second.</p>
        }
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
    h1 {
      font-size: 1.4rem;
      margin: 0;
    }
    .spinner {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      border: 3px solid var(--gold-4);
      border-top-color: var(--gold-1);
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `,
})
export class InviteComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly clubService = inject(ClubService);

  protected readonly error = signal('');

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (!code) {
      this.error.set('That invite link is missing its code.');
      return;
    }

    this.clubService.joinByInvite(code).subscribe({
      next: (club) => this.router.navigate(['/clubs', club.id]),
      error: (e) => this.error.set(e?.error?.title ?? 'No club found with that invite code.'),
    });
  }
}
