// app.routes.ts
// The four pages of the app. No page is complicated:
//   /login      — log in or register.
//   /items      — list items, add one (admin), delete one (admin).
//   /borrow/:id — borrow one item: dates + purpose, then send.
//   /requests   — list requests, approve / reject / return.
// Anything else goes to /items.

import { Routes } from '@angular/router';
import { LoginComponent } from './login/login';
import { ItemsComponent } from './items/items';
import { BorrowComponent } from './borrow/borrow';
import { RequestsComponent } from './requests/requests';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'items', component: ItemsComponent },
  { path: 'borrow/:id', component: BorrowComponent },
  { path: 'requests', component: RequestsComponent },
  { path: '', redirectTo: '/items', pathMatch: 'full' },
  { path: '**', redirectTo: '/items' },
];
