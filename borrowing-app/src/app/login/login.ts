// login.ts
// One small job: log a user in, or register a new one.
// After login, the user is saved in localStorage (the browser's
// little storage box) under the key "user", so the other pages
// can read who is logged in. Logging out just deletes that key.

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../api';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
})
export class LoginComponent {
  // The boxes in the form. [(ngModel)] keeps them in sync.
  name = '';
  email = '';
  password = '';
  // false = login form, true = register form.
  isRegistering = false;
  // Shown when the API says no (wrong password, email taken).
  error = '';

  constructor(private api: ApiService, private router: Router) {}

  login(): void {
    this.error = '';
    this.api.login(this.email, this.password).subscribe({
      next: (user) => {
        // Save the user, then go to the items page.
        localStorage.setItem('user', JSON.stringify(user));
        this.router.navigate(['/items']);
      },
      error: () => {
        this.error = 'Wrong email or password.';
      },
    });
  }

  register(): void {
    this.error = '';
    this.api.register(this.name, this.email, this.password).subscribe({
      next: (user) => {
        localStorage.setItem('user', JSON.stringify(user));
        this.router.navigate(['/items']);
      },
      error: () => {
        this.error = 'That email is already registered, or a field is empty.';
      },
    });
  }
}
