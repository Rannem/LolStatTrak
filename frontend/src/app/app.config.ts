import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { PreloadAllModules, provideRouter, withPreloading, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
import { authErrorInterceptor } from './core/interceptors/auth-error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Preload every lazy route chunk once the app is idle so navigation never waits on a download.
    provideRouter(routes, withPreloading(PreloadAllModules), withViewTransitions()),
    provideHttpClient(withFetch(), withInterceptors([authErrorInterceptor])),
  ]
};
