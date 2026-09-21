import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withViewTransitions } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    // Detección de cambios optimizada con Zone.js
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Router con View Transitions API para animaciones suaves entre rutas
    provideRouter(routes, withViewTransitions()),
    // Animaciones de Angular Material (async para mejor rendimiento)
    provideAnimationsAsync(),
    // HttpClient con Fetch API (más moderno que XHR)
    provideHttpClient(withFetch()),
  ],
};
