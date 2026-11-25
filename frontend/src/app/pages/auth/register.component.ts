import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card card">
      <div><span class="brand-mark">I&C</span></div>
      <h1>Create your account</h1>
      <p>Your meals, foods, supplies, and settings stay separate from every other account.</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <div class="auth-field">
          <label for="register-username">Username</label>
          <input id="register-username" formControlName="username" autocomplete="username" aria-describedby="username-help username-error" [attr.aria-invalid]="form.controls.username.touched && form.controls.username.invalid">
          <small id="username-help">Use 3–50 characters.</small>
          <div id="username-error" aria-live="polite">@if (form.controls.username.touched && form.controls.username.invalid) { <p class="form-error">Enter a username with 3–50 characters, for example coffee-fan.</p> }</div>
        </div>
        <div class="auth-field">
          <label for="register-password">Password</label>
          <input id="register-password" type="password" formControlName="password" autocomplete="new-password" aria-describedby="password-help password-error" [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid">
          <small id="password-help">Use 8–128 characters. Your password is case-sensitive.</small>
          <div id="password-error" aria-live="polite">@if (form.controls.password.touched && form.controls.password.invalid) { <p class="form-error">Enter a password with 8–128 characters.</p> }</div>
        </div>
        @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
        <button type="submit" [disabled]="loading">{{ loading ? 'Creating…' : 'Create account' }}</button>
      </form>
      <p>Already registered? <a class="text-link" routerLink="/login">Sign in</a></p>
    </section>
  `,
  styles: `.auth-field { display:grid; gap:.35rem; } .auth-field p { margin:0; }`
})
export class RegisterComponent {
  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]]
  });
  loading = false;
  error = '';

  constructor(private readonly fb: FormBuilder, private readonly auth: AuthService, private readonly router: Router) {}

  submit(): void {
    if (this.loading) return;
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    this.error = '';
    const { username, password } = this.form.getRawValue();
    this.auth.register(username, password).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => void this.router.navigate(['/login'], { queryParams: { registered: 'true' } }),
      error: (error: { message?: string }) => this.error = error.message || 'Registration failed.'
    });
  }
}
