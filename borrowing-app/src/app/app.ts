// app.ts
// One small job: the top bar of the whole app.
// It reads the logged-in user from localStorage, shows their
// name, and gives them two links (Items, Requests) plus Log out.
// The pages themselves appear below, in <router-outlet>.

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { User } from './api';
import { RealtimeService } from './realtime';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
})
export class App implements OnInit {
  constructor(private router: Router, public realtime: RealtimeService) {}

  ngOnInit(): void {
    // Open the live SignalR connection once, when the app starts.
    // It stays open for the whole visit, on every page.
    this.realtime.start();
  }

  // Read the saved user, or null when nobody is logged in.
  currentUser(): User | null {
    const saved = localStorage.getItem('user');
    return saved ? JSON.parse(saved) : null;
  }

  // Log out: delete the saved user, then go to the login page.
  logout(): void {
    localStorage.removeItem('user');
    this.router.navigate(['/login']);
  }
}
