import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Lets ApiService use HttpClient to call the API.
    provideHttpClient(),
    // The real fix for the empty Items page: Angular 22 runs
    // zoneless by default, so after the API answers, the page
    // never redraws — the 4 items sit in memory, the screen
    // still says "Showing 0 items". This line tells Angular
    // to use zone.js (imported in main.ts) and redraw the page
    // whenever data arrives. Simple, standard, one line.
    provideZoneChangeDetection(),
  ],
};
