// borrow.ts
// One small job: the Borrow page — its own page, on purpose.
// Before v5 the borrow form sat at the bottom of the Items page,
// so clicking Borrow looked like nothing happened until you
// scrolled all the way down. Now Borrow takes you here:
//   /borrow/2  — borrow the item with id 2.
// This page shows the item, asks for the dates and purpose,
// checks them, sends the request, then takes you to Requests
// to see it Pending.

import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService, Item, User } from '../api';

@Component({
  selector: 'app-borrow',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './borrow.html',
})
export class BorrowComponent implements OnInit {
  // The item being borrowed. A signal, like the lists in Items
  // and Requests: when the item arrives from the API, the page
  // redraws by itself. A plain field here would stay blank —
  // the same bug the Items page had in v3.
  item = signal<Item | null>(null);
  user: User | null = null;
  // The message is a signal too, for the same reason: it is set
  // after the API answers, so the page must redraw to show it.
  message = signal('');

  dateNeeded = '';
  returnDate = '';
  purpose = '';
  // Today, as yyyy-mm-dd. The date boxes cannot pick a day before
  // today, so a date in the past cannot even be chosen.
  today = new Date().toISOString().slice(0, 10);

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // No user saved = not logged in. Go to the login page.
    const saved = localStorage.getItem('user');
    if (!saved) {
      this.router.navigate(['/login']);
      return;
    }
    this.user = JSON.parse(saved);

    // Read the id from the address, for example /borrow/2.
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.router.navigate(['/items']);
      return;
    }

    // Load that one item. If it is gone or already borrowed,
    // say so instead of showing an empty form.
    this.api.getItem(id).subscribe({
      next: (item) => {
        if (item.status !== 'Available') {
          this.message.set('That item is already borrowed.');
          return;
        }
        this.item.set(item);
      },
      error: () => {
        this.message.set('That item does not exist.');
      },
    });
  }

  // Send the borrow request. The API makes it Pending.
  // First, the same checks the API does, so the user gets the
  // message straight away instead of waiting for an error:
  // every field filled in, date needed not in the past, and
  // return date after the date needed. The API checks again
  // anyway, so a wrong request cannot sneak in another way.
  sendRequest(): void {
    const item = this.item();
    if (!this.user || !item) return;
    if (!this.dateNeeded || !this.returnDate || !this.purpose.trim()) {
      this.message.set('Fill in the date needed, return date and purpose first.');
      return;
    }
    if (this.dateNeeded < this.today) {
      this.message.set('Date needed cannot be in the past.');
      return;
    }
    if (this.returnDate <= this.dateNeeded) {
      this.message.set('Return date must be after the date needed.');
      return;
    }
    this.api
      .createRequest(this.user.id, item.id, this.dateNeeded, this.returnDate, this.purpose)
      .subscribe({
        next: () => {
          // Sent. Go to Requests so the new Pending request is visible.
          this.router.navigate(['/requests']);
        },
        error: (err) => {
          // Show the API's own reason when it has one (for example
          // "That item already has a pending or approved request.").
          this.message.set(err?.error?.message || err?.error || 'Could not send the request. Check the dates and try again.');
        },
      });
  }
}
