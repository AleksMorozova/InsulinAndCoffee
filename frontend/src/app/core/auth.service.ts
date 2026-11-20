import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

export type UserRole = 'User' | 'Admin';
export interface AuthUser { id: string; username: string; role: UserRole; createdAt: string; }
export interface AuthToken { accessToken: string; expiresAt: string; user: AuthUser; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly tokenKey = 'insulin-and-coffee.access-token';
  private static readonly userKey = 'insulin-and-coffee.user';
  private static readonly expiryKey = 'insulin-and-coffee.expires-at';
  private readonly userState = signal<AuthUser | null>(this.loadUser());
  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = () => !!this.token && !!this.userState();
  readonly isAdmin = computed(() => this.userState()?.role === 'Admin');

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null {
    const expiry = localStorage.getItem(AuthService.expiryKey);
    if (expiry && !(Date.parse(expiry) > Date.now())) return null;
    return localStorage.getItem(AuthService.tokenKey);
  }

  login(username: string, password: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/login`, { username, password }).pipe(
      tap(result => this.storeSession(result))
    );
  }

  loginWithGoogle(credential: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/google`, { credential }).pipe(
      tap(result => this.storeSession(result))
    );
  }

  register(username: string, password: string) {
    return this.http.post<AuthUser>(`${environment.apiUrl}/auth/register`, { username, password });
  }

  logout(redirect = true): void {
    localStorage.removeItem(AuthService.tokenKey);
    localStorage.removeItem(AuthService.userKey);
    localStorage.removeItem(AuthService.expiryKey);
    this.userState.set(null);
    if (redirect) void this.router.navigateByUrl('/login');
  }

  private storeSession(result: AuthToken) {
    if (!result?.accessToken?.trim() || !(Date.parse(result.expiresAt) > Date.now()) ||
        !result.user?.id || !result.user.username || !['Admin', 'User'].includes(result.user.role)) {
      throw new Error('Invalid authentication response');
    }

    localStorage.setItem(AuthService.tokenKey, result.accessToken);
    localStorage.setItem(AuthService.expiryKey, result.expiresAt);
    localStorage.setItem(AuthService.userKey, JSON.stringify(result.user));
    this.userState.set(result.user);
  }

  private loadUser(): AuthUser | null {
    const value = localStorage.getItem(AuthService.userKey);
    if (!value) return null;
    try {
      const user = JSON.parse(value) as AuthUser;
      return user.id && (user.role === 'User' || user.role === 'Admin') ? user : null;
    } catch { return null; }
  }
}
