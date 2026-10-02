import { DOCUMENT } from '@angular/common';
import { AfterViewInit, Component, ElementRef, Inject, NgZone, ViewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { GoogleCredentialResponse, GoogleIdentityService } from '../../core/google-identity.service';
import { environment } from '../../../environments/environment';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card card">
      <div><span class="brand-mark">I&C</span></div>
      <h1>Welcome back</h1>
      <p>Sign in to your private meal history and settings.</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>Username<input formControlName="username" autocomplete="username"></label>
        <label>Password<input type="password" formControlName="password" autocomplete="current-password"></label>
        @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
        <button type="submit" [disabled]="form.invalid || loading">{{ loading ? 'Signing in…' : 'Sign in' }}</button>
      </form>
      @if (googleEnabled) {
        <div class="auth-divider"><span>or</span></div>
        <div #googleButton class="google-button" aria-label="Sign in with Google"></div>
      }
      <p>New here? <a class="text-link" routerLink="/register">Create an account</a></p>
    </section>
  `
})
export class LoginComponent implements AfterViewInit {
  @ViewChild('googleButton') googleButton?: ElementRef<HTMLElement>;
  readonly googleEnabled = !!environment.googleClientId;
  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.minLength(4), Validators.maxLength(128)]]
  });
  loading = false;
  error = '';

  constructor(
    private readonly fb: FormBuilder,
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly googleIdentity: GoogleIdentityService,
    private readonly zone: NgZone,
    @Inject(DOCUMENT) private readonly document: Document) {}

  ngAfterViewInit(): void {
    if (!this.googleEnabled || !this.googleButton) return;
    this.googleIdentity.load().then(api => {
      api.initialize({ client_id: environment.googleClientId, callback: response => this.handleGoogle(response) });
      api.renderButton(this.googleButton!.nativeElement, { theme: 'outline', size: 'large', width: 360, text: 'signin_with' });
    }).catch(() => this.zone.run(() => this.error = 'Google sign-in is temporarily unavailable.'));
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.error = '';
    const { username, password } = this.form.getRawValue();
    this.auth.login(username, password).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: () => this.error = 'Invalid username or password.'
    });
  }

  private handleGoogle(response: GoogleCredentialResponse): void {
    this.zone.run(() => {
      this.loading = true;
      this.error = '';
      this.auth.loginWithGoogle(response.credential).pipe(finalize(() => this.loading = false)).subscribe({
        next: () => this.document.location.replace('/dashboard'),
        error: () => this.error = 'Google sign-in failed. Please try again.'
      });
    });
  }
}
