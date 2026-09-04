import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
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
export class LoginComponent implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    // If the session cookie is already valid (e.g. user navigated back here after
    // logging in), skip straight to the app instead of showing the login button again.
    this.auth.checkSession().subscribe(() => {
      if (this.auth.isAuthenticated()) {
        this.router.navigateByUrl('/clubs');
      }
    });
  }
}
