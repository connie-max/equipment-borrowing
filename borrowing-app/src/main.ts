// zone.js — Angular needs this one line to know when data arrives
// from the API and redraw the page. Without it, the Items page
// loads the 4 items into memory but never shows them on screen:
// the API returns the data (200), yet "Showing 0 items" stays.
// That was the real bug in v1 and v2, not the table.
import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
