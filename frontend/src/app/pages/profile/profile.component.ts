import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AvatarPipe } from '../../core/pipes/avatar.pipe';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule, AvatarPipe],
  template: `
    <div class="page fade-up narrow">
      <div class="page-header">
        <div>
          <div class="eyebrow">Your account</div>
          <h1>Profile</h1>
        </div>
      </div>

      @if (setupMode() && !auth.user()?.riotLinked) {
        <div class="card setup-banner">
          <strong>One last step:</strong> link your Riot ID to unlock the rest of the site. This
          is how your custom game stats get attributed to you.
        </div>
      }

      @if (auth.user(); as user) {
        <div class="card identity">
          <img class="avatar big" [src]="user.avatarUrl | avatar: 128" [alt]="user.discordUsername" />
          <div>
            <div class="name">{{ user.discordUsername }}</div>
            <div class="row small">
              <span class="badge blue">Discord</span>
              @if (user.isGlobalAdmin) {
                <span class="badge gold">Global admin</span>
              }
            </div>
          </div>
        </div>

        <div class="card">
          <h2>Riot account</h2>
          @if (user.riotLinked) {
            <p class="muted small">
              Linked as <strong class="riot">{{ user.riotGameName }}<span class="dim">#{{ user.riotTagLine }}</span></strong>.
              Your custom games will be matched to this account when a lobby is marked as played.
            </p>
            <button class="btn-danger btn-sm" (click)="unlink()" [disabled]="busy()">Unlink</button>
          } @else {
            <p class="muted small">
              Link your Riot ID so match stats can be attributed to you. It's the name you see in the
              League client — e.g. <span class="code">Faker#KR1</span>.
            </p>
            <form class="riot-form" (ngSubmit)="link()">
              <div>
                <label>Game name</label>
                <input [(ngModel)]="gameName" name="gameName" placeholder="Faker" autocomplete="off" />
              </div>
              <div class="tag">
                <label>Tag</label>
                <input [(ngModel)]="tagLine" name="tagLine" placeholder="KR1" autocomplete="off" />
              </div>
              <button class="btn-primary" type="submit" [disabled]="busy() || !gameName.trim() || !tagLine.trim()">
                {{ busy() ? 'Looking up…' : 'Link' }}
              </button>
            </form>
          }
          @if (error()) {
            <p class="error small">{{ error() }}</p>
          }
          @if (success()) {
            <p class="ok small">{{ success() }}</p>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .narrow {
      max-width: 720px;
    }
    .identity {
      display: flex;
      align-items: center;
      gap: 1.25rem;
      margin-bottom: 1.25rem;
    }
    .big {
      width: 72px;
      height: 72px;
      border-width: 3px;
    }
    .name {
      font-family: 'Cinzel', serif;
      font-size: 1.4rem;
      font-weight: 700;
      color: var(--gold-1);
    }
    .small {
      font-size: 0.8rem;
    }
    .riot {
      color: var(--gold-1);
    }
    .riot-form {
      display: grid;
      grid-template-columns: 1fr 120px auto;
      gap: 0.75rem;
      align-items: end;

      @media (max-width: 560px) {
        grid-template-columns: 1fr;
      }
    }
    .error {
      color: var(--danger);
      margin: 0.75rem 0 0;
    }
    .ok {
      color: var(--success);
      margin: 0.75rem 0 0;
    }
    .setup-banner {
      border-color: var(--gold-3);
      color: var(--gold-1);
      margin-bottom: 1.25rem;
    }
  `,
})
export class ProfileComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected gameName = '';
  protected tagLine = '';
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly success = signal('');
  protected readonly setupMode = signal(false);

  private returnUrl = '';

  constructor() {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '';
    this.setupMode.set(!!this.returnUrl);
  }

  link(): void {
    this.busy.set(true);
    this.error.set('');
    this.success.set('');
    this.auth.linkRiotAccount(this.gameName, this.tagLine).subscribe({
      next: (u) => {
        this.success.set(`Linked ${u.riotGameName}#${u.riotTagLine}.`);
        this.gameName = '';
        this.tagLine = '';
        this.busy.set(false);
        if (this.returnUrl) this.router.navigateByUrl(this.returnUrl);
      },
      error: (e) => {
        this.error.set(e?.error?.title ?? 'Could not link that Riot account.');
        this.busy.set(false);
      },
    });
  }

  unlink(): void {
    this.busy.set(true);
    this.error.set('');
    this.success.set('');
    this.auth.unlinkRiotAccount().subscribe({
      next: () => this.busy.set(false),
      error: () => this.busy.set(false),
    });
  }
}
