import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `
    <div class="login-wrap">
      <div class="login-card card fade-up">
        <div class="emblem">⚔</div>
        <p class="eyebrow">Custom ARAM companion</p>
        <h1>LolStat<em>Trak</em></h1>
        <p class="muted">
          Random teams, random champs, club-wide bans and automatic stat tracking for your
          friend group's custom ARAM nights.
        </p>

        <button class="btn-discord" (click)="auth.loginWithDiscord(returnUrl)">
          <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
            <path fill="currentColor" d="M20.317 4.37a19.79 19.79 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.74 19.74 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.3 12.3 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.84 19.84 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>
          </svg>
          Continue with Discord
        </button>

        <ul class="features clean">
          <li><span>🎲</span> Roll balanced-random teams &amp; champions</li>
          <li><span>🚫</span> Per-club champion ban lists</li>
          <li><span>📡</span> Live lobby updates for everyone</li>
          <li><span>📊</span> Match stats pulled straight from Riot</li>
        </ul>
      </div>
    </div>
  `,
  styles: `
    .login-wrap {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 2rem 1rem;
    }

    .login-card {
      width: min(460px, 100%);
      text-align: center;
      padding: 2.5rem 2rem;
      border-color: var(--gold-4);
    }

    .emblem {
      width: 72px;
      height: 72px;
      margin: 0 auto 1rem;
      display: grid;
      place-items: center;
      border-radius: 50%;
      font-size: 2rem;
      color: var(--blue-2);
      border: 2px solid var(--gold-3);
      background: radial-gradient(circle at 30% 30%, var(--blue-4), var(--bg-0));
      box-shadow: var(--glow-gold), inset 0 0 20px rgba(10, 200, 185, 0.3);
    }

    .eyebrow {
      text-transform: uppercase;
      letter-spacing: 0.22em;
      font-size: 0.7rem;
      color: var(--blue-2);
      margin-bottom: 0.25rem;
    }

    h1 {
      font-size: 2.4rem;
      margin-bottom: 0.75rem;

      em {
        font-style: normal;
        color: var(--gold-3);
      }
    }

    .btn-discord {
      width: 100%;
      justify-content: center;
      padding: 0.9rem 1.25rem;
      font-size: 0.85rem;
      background: linear-gradient(180deg, #5865f2 0%, #4752c4 100%);
      border-color: #7983f5;
      color: #fff;
      margin: 0.5rem 0 1.75rem;

      &:hover {
        box-shadow: 0 0 20px rgba(88, 101, 242, 0.55);
        border-color: #aab0ff;
      }
    }

    .features {
      text-align: left;
      display: grid;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: var(--text-muted);

      li {
        display: flex;
        gap: 0.6rem;
        align-items: center;
      }

      span {
        width: 1.5rem;
        text-align: center;
      }
    }
  `,
})
export class LoginComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected returnUrl = '/clubs';

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/clubs';

    // Skip straight to the app if the session cookie is already valid.
    this.auth.checkSession().subscribe(() => {
      if (this.auth.isAuthenticated()) {
        this.router.navigateByUrl(this.returnUrl);
      }
    });
  }
}
