import { AfterViewInit, Component, ElementRef, NgZone, ViewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { loginDestination } from '../../core/login-destination';
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
    private readonly route: ActivatedRoute) {}

  ngAfterViewInit(): void {
    if (!this.googleEnabled || !this.googleButton) return;
    this.googleIdentity.load().then(api => {
      api.initialize({ client_id: environment.googleClientId, callback: response => this.handleGoogle(response) });
      api.renderButton(this.googleButton!.nativeElement, { theme: 'outline', size: 'large', width: 360, text: 'signin_with' });
    }).catch(() => this.zone.run(() => this.error = 'Google sign-in is temporarily unavailable.'));
  }

  submit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.error = '';
    const { username, password } = this.form.getRawValue();
    this.auth.login(username, password).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => this.redirect(),
      error: () => { this.form.controls.password.reset(); this.error = 'Could not sign in. Check your username and password and try again.'; }
    });
  }

  private redirect() {
    const destination = loginDestination(this.router, this.route.snapshot.queryParamMap.get('returnUrl'));
    void this.router.navigateByUrl(destination, { replaceUrl: true });
  }

  private handleGoogle(response: GoogleCredentialResponse): void {
    this.zone.run(() => {
      if (this.loading) return;
      this.loading = true;
      this.error = '';
      this.auth.loginWithGoogle(response.credential).pipe(finalize(() => this.loading = false)).subscribe({
        next: () => this.redirect(),
        error: () => this.error = 'Google sign-in failed. Please try again.'
      });
    });
  }
}
