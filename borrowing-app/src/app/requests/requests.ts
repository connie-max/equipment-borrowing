// requests.ts
// One small job: show the borrow requests, and the three buttons.
//   - The list comes from GET /api/requests (newest first).
//   - Pending: an Admin sees Approve and Reject.
//   - Approved: the borrower (or an Admin) sees Return.
// After any button, the list is loaded again, so the new
// status — and the item's new status — show straight away.

import { Component, effect, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService, BorrowRequest, User } from '../api';
import { RealtimeService } from '../realtime';

@Component({
  selector: 'app-requests',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './requests.html',
})
export class RequestsComponent implements OnInit {
  // Same signal fix as Items: when the list changes, the page
  // redraws by itself, so the requests show straight away.
  requests = signal<BorrowRequest[]>([]);
  user: User | null = null;
  message = '';

  constructor(private api: ApiService, private router: Router, private realtime: RealtimeService) {
    // Live updates (SignalR): when the API announces a change
    // (a request approved, rejected or returned), tick goes up
    // by 1, and this page loads the requests again by itself.
    // tick starts at 0, so the first run does nothing: ngOnInit
    // below already loads the requests once.
    effect(() => {
      if (this.realtime.tick() > 0) {
        this.loadRequests();
      }
    });
  }

  ngOnInit(): void {
    const saved = localStorage.getItem('user');
    if (!saved) {
      this.router.navigate(['/login']);
      return;
    }
    this.user = JSON.parse(saved);
    this.loadRequests();
  }

  isAdmin(): boolean {
    return this.user?.role === 'Admin';
  }

  // This request belongs to the logged-in user.
  isMine(request: BorrowRequest): boolean {
    return this.user?.id === request.userId;
  }

  loadRequests(): void {
    this.api.getRequests().subscribe((requests) => this.requests.set(requests));
  }

  // Approve: Pending -> Approved, item -> Borrowed (done by the API).
  approve(request: BorrowRequest): void {
    this.api.approve(request.id).subscribe({
      next: () => { this.message = ''; this.loadRequests(); },
      error: (err) => { this.message = err?.error || 'Could not approve that request.'; },
    });
  }

  // Reject: Pending -> Rejected, item stays Available.
  reject(request: BorrowRequest): void {
    this.api.reject(request.id).subscribe({
      next: () => { this.message = ''; this.loadRequests(); },
      error: () => { this.message = 'Could not reject that request.'; },
    });
  }

  // Return: Approved -> Returned, item -> Available again.
  returnItem(request: BorrowRequest): void {
    this.api.returnItem(request.id).subscribe({
      next: () => { this.message = ''; this.loadRequests(); },
      error: () => { this.message = 'Could not return that request.'; },
    });
  }
}
