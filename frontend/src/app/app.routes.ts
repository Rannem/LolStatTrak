import { Routes } from '@angular/router';
import { authGuard, globalAdminGuard, inviteGuard, pendingGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'clubs' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'pending',
    canActivate: [pendingGuard],
    loadComponent: () => import('./pages/pending/pending.component').then((m) => m.PendingComponent),
  },
  {
    path: 'clubs',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/clubs/clubs.component').then((m) => m.ClubsComponent),
  },
  {
    path: 'invite/:code',
    canActivate: [inviteGuard],
    loadComponent: () => import('./pages/invite/invite.component').then((m) => m.InviteComponent),
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
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/profile/profile.component').then((m) => m.ProfileComponent),
  },
  {
    path: 'admin',
    canActivate: [globalAdminGuard],
    loadComponent: () => import('./pages/admin/admin.component').then((m) => m.AdminComponent),
  },
  { path: '**', redirectTo: 'clubs' },
];
