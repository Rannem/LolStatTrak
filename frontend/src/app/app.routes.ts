import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'clubs' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'clubs',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/clubs/clubs.component').then((m) => m.ClubsComponent),
  },
  {
    path: 'clubs/:clubId',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/club-detail/club-detail.component').then((m) => m.ClubDetailComponent),
  },
  {
    path: 'lobbies/:lobbyId',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/lobby/lobby.component').then((m) => m.LobbyComponent),
  },
];
