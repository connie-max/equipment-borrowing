// items.ts
// One small job: show the items, and let an admin add or delete one.
//   - The list comes from GET /api/items.
//   - "Borrow" goes to its own page, /borrow/:id. The form used to
//     sit at the bottom of this page, so clicking Borrow looked
//     like nothing happened until you scrolled all the way down.
//     A page of its own fixes that.
//   - Admins also see the add-item form and the delete button.

import { Component, effect, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService, Item, User } from '../api';
import { RealtimeService } from '../realtime';

@Component({
  selector: 'app-items',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './items.html',
})
export class ItemsComponent implements OnInit {
  // The list is a signal: a box that holds the items. When the
  // list inside the box changes, Angular redraws the page by
  // itself. That is the fix for the empty Items page: in this
  // Angular version the page did NOT redraw after the API
  // answered, so "Showing 0 items" stayed even though the
  // 4 items had already arrived in memory.
  items = signal<Item[]>([]);
  // Who is logged in (read back from localStorage).
  user: User | null = null;
  message = '';

  // The add-item form (admin only).
  newName = '';
  newType = 'Equipment';
  newDescription = '';

  constructor(private api: ApiService, private router: Router, private realtime: RealtimeService) {
    // Live updates (SignalR): when the API announces a change
    // (an item added, a request approved, and so on), tick goes
    // up by 1, and this page loads the items again by itself.
    // tick starts at 0, so the first run does nothing: ngOnInit
    // below already loads the items once.
    effect(() => {
      if (this.realtime.tick() > 0) {
        this.loadItems();
      }
    });
  }

  ngOnInit(): void {
    // No user saved = not logged in. Go to the login page.
    const saved = localStorage.getItem('user');
    if (!saved) {
      this.router.navigate(['/login']);
      return;
    }
    this.user = JSON.parse(saved);
    this.loadItems();
  }

  // True when the logged-in user is an Admin.
  isAdmin(): boolean {
    return this.user?.role === 'Admin';
  }

  loadItems(): void {
    // Load the items. If it fails, show why, so the page is never blank with no clue.
    this.api.getItems().subscribe({
      next: (items) => { this.items.set(items); },
      error: () => { this.message = 'Could not load the items. Check the API is running.'; },
    });
  }

  // Admin only: add a new item. It starts as Available.
  addItem(): void {
    if (!this.newName.trim() || !this.newDescription.trim()) {
      this.message = 'Give the new item a name and a description first.';
      return;
    }
    this.api.addItem(this.newName, this.newType, this.newDescription).subscribe({
      next: () => {
        this.newName = '';
        this.newDescription = '';
        this.message = '';
        this.loadItems();
      },
      error: () => { this.message = 'Could not add the item. Try again.'; },
    });
  }

  // Admin only: delete an item.
  deleteItem(item: Item): void {
    this.api.deleteItem(item.id).subscribe({
      next: () => this.loadItems(),
      error: () => { this.message = 'That item cannot be deleted while it has a pending or approved request.'; },
    });
  }
}
