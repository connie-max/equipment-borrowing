// api.ts
// One small job: talk to the API, so the pages do not have to.
// Every page uses this one service. It only knows two things:
//   1. Where the API lives: http://localhost:5035 (see
//      BorrowingApi/Properties/launchSettings.json, profile "http").
//   2. What each call sends and gets back.
//
// The three small boxes below (User, Item, BorrowRequest) match
// the C# models in BorrowingApi/Models. Same names, same fields.

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

// What the API sends back after login / register (no password in it).
export interface User {
  id: number;
  name: string;
  email: string;
  role: string; // "Admin" or "Borrower"
}

// One thing that can be borrowed.
export interface Item {
  id: number;
  name: string;
  type: string; // "Equipment" or "Room"
  description: string;
  status: string; // "Available" or "Borrowed"
}

// One borrow request. UserName and ItemName come from the API,
// so the page does not need a second call to show them.
export interface BorrowRequest {
  id: number;
  userId: number;
  userName: string;
  itemId: number;
  itemName: string;
  dateNeeded: string;
  returnDate: string;
  purpose: string;
  status: string; // Pending, Approved, Rejected, Returned
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  // The API address. Change it here once if the API moves.
  private baseUrl = 'http://localhost:5035';

  constructor(private http: HttpClient) {}

  // Who is calling? The logged-in user is saved in localStorage
  // after login. We send their id in the X-User-Id header on
  // every call, so the API can check the role in the database
  // before doing admin-only work (approve, delete, add item).
  // Login and register do not send it — there is no user yet.
  private callerHeaders() {
    const saved = localStorage.getItem('user');
    const id = saved ? JSON.parse(saved).id : 0;
    return { 'X-User-Id': String(id) };
  }

  // --- Login / register (POST /api/auth/...) ---

  login(email: string, password: string): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/api/auth/login`, { email, password });
  }

  register(name: string, email: string, password: string): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/api/auth/register`, {
      name, email, password, role: 'Borrower',
    });
  }

  // --- Items (GET / POST / DELETE /api/items) ---

  getItems(): Observable<Item[]> {
    return this.http.get<Item[]>(`${this.baseUrl}/api/items`, { headers: this.callerHeaders() });
  }

  // One item, by id. The Borrow page uses this to show the item
  // you are about to borrow.
  getItem(id: number): Observable<Item> {
    return this.http.get<Item>(`${this.baseUrl}/api/items/${id}`, { headers: this.callerHeaders() });
  }

  addItem(name: string, type: string, description: string): Observable<Item> {
    return this.http.post<Item>(`${this.baseUrl}/api/items`, { name, type, description }, { headers: this.callerHeaders() });
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/items/${id}`, { headers: this.callerHeaders() });
  }

  // --- Borrow requests (GET / POST /api/requests) ---

  getRequests(): Observable<BorrowRequest[]> {
    return this.http.get<BorrowRequest[]>(`${this.baseUrl}/api/requests`, { headers: this.callerHeaders() });
  }

  createRequest(userId: number, itemId: number, dateNeeded: string, returnDate: string, purpose: string): Observable<BorrowRequest> {
    return this.http.post<BorrowRequest>(`${this.baseUrl}/api/requests`, {
      userId, itemId, dateNeeded, returnDate, purpose,
    }, { headers: this.callerHeaders() });
  }

  // The three buttons: approve, reject, return.
  // Each one is just a POST to /api/requests/{id}/approve (and so on).
  approve(id: number): Observable<BorrowRequest> {
    return this.http.post<BorrowRequest>(`${this.baseUrl}/api/requests/${id}/approve`, {}, { headers: this.callerHeaders() });
  }

  reject(id: number): Observable<BorrowRequest> {
    return this.http.post<BorrowRequest>(`${this.baseUrl}/api/requests/${id}/reject`, {}, { headers: this.callerHeaders() });
  }

  returnItem(id: number): Observable<BorrowRequest> {
    return this.http.post<BorrowRequest>(`${this.baseUrl}/api/requests/${id}/return`, {}, { headers: this.callerHeaders() });
  }
}
