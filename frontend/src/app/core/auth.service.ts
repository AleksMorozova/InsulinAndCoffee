import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthUser { username: string; createdAt: string; }
export interface AuthToken { accessToken: string; expiresAt: string; user: AuthUser; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly tokenKey = 'insulin-and-coffee.access-token';
  private static readonly userKey = 'insulin-and-coffee.user';
  private readonly userState = signal<AuthUser | null>(this.loadUser());
  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => !!this.token && !!this.userState());

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null { return localStorage.getItem(AuthService.tokenKey); }

  login(username: string, password: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/login`, { username, password }).pipe(
      tap(result => this.store(result))
    );
  }

  loginWithGoogle(credential: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/google`, { credential }).pipe(
      tap(result => this.store(result))
    );
  }

  register(username: string, password: string) {
    return this.http.post<AuthUser>(`${environment.apiUrl}/auth/register`, { username, password });
  }

  logout(redirect = true): void {
    localStorage.removeItem(AuthService.tokenKey);
    localStorage.removeItem(AuthService.userKey);
    this.userState.set(null);
    if (redirect) void this.router.navigateByUrl('/login');
  }

  private store(result: AuthToken): void {
    localStorage.setItem(AuthService.tokenKey, result.accessToken);
    localStorage.setItem(AuthService.userKey, JSON.stringify(result.user));
    this.userState.set(result.user);
  }

  private loadUser(): AuthUser | null {
    const value = localStorage.getItem(AuthService.userKey);
    if (!value) return null;
    try { return JSON.parse(value) as AuthUser; } catch { return null; }
  }
}
