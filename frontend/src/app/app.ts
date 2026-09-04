import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs';
import { AuthService } from './core/services/auth.service';
import { AvatarPipe } from './core/pipes/avatar.pipe';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, AvatarPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly swUpdate = inject(SwUpdate);

  protected readonly updateAvailable = signal(false);

  constructor() {
    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates
        .pipe(filter((e) => e.type === 'VERSION_READY'))
        .subscribe(() => this.updateAvailable.set(true));
    }
  }

  reload(): void {
    this.swUpdate.activateUpdate().then(() => document.location.reload());
  }

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigateByUrl('/login'));
  }
}
