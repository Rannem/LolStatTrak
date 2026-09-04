import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `
    <div class="login">
      <h1>LolStatTrak</h1>
      <p>Sign in with Discord to view your clubs and roll ARAM lobbies.</p>
      <button (click)="auth.loginWithDiscord()">Login with Discord</button>
    </div>
  `,
})
export class LoginComponent {
  protected readonly auth = inject(AuthService);
}
