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
        <label>Username<input formControlName="username" autocomplete="username"></label>
        <label>Password<input type="password" formControlName="password" autocomplete="new-password"></label>
        @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
        <button type="submit" [disabled]="form.invalid || loading">{{ loading ? 'Creating…' : 'Create account' }}</button>
      </form>
      <p>Already registered? <a class="text-link" routerLink="/login">Sign in</a></p>
    </section>
  `
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
    if (this.form.invalid) return;
    this.loading = true;
    this.error = '';
    const { username, password } = this.form.getRawValue();
    this.auth.register(username, password).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => void this.router.navigate(['/login'], { queryParams: { registered: 'true' } }),
      error: (error: { message?: string }) => this.error = error.message || 'Registration failed.'
    });
  }
}
