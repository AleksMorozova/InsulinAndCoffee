import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, switchMap, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export type UserRole = 'User' | 'Admin';
export interface AuthUser { id: string; username: string; role: UserRole; createdAt: string; }
export interface AuthToken { accessToken: string; expiresAt: string; user: AuthUser; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly tokenKey = 'insulin-and-coffee.access-token';
  private static readonly userKey = 'insulin-and-coffee.user';
  private readonly userState = signal<AuthUser | null>(this.loadUser());
  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => !!this.token && !!this.userState());
  readonly isAdmin = computed(() => this.userState()?.role === 'Admin');

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null { return localStorage.getItem(AuthService.tokenKey); }

  login(username: string, password: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/login`, { username, password }).pipe(
      switchMap(result => this.storeTokenAndLoadUser(result))
    );
  }

  loginWithGoogle(credential: string) {
    return this.http.post<AuthToken>(`${environment.apiUrl}/auth/google`, { credential }).pipe(
      switchMap(result => this.storeTokenAndLoadUser(result))
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

  private storeTokenAndLoadUser(result: AuthToken) {
    localStorage.setItem(AuthService.tokenKey, result.accessToken);
    return this.http.get<AuthUser>(`${environment.apiUrl}/auth/me`).pipe(
      tap(user => {
        localStorage.setItem(AuthService.userKey, JSON.stringify(user));
        this.userState.set(user);
      }),
      map(user => ({ ...result, user }))
    );
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
