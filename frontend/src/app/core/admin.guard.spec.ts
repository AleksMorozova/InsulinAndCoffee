import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { adminGuard } from './admin.guard';

@Component({ standalone: true, template: '' })
class EmptyComponent {}

describe('adminGuard', () => {
  const authenticated = signal(true);
  const admin = signal(false);

  beforeEach(() => {
    authenticated.set(true);
    admin.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'admin', component: EmptyComponent, canActivate: [adminGuard] },
          { path: 'access-denied', component: EmptyComponent },
          { path: 'login', component: EmptyComponent }
        ]),
        { provide: AuthService, useValue: { isAuthenticated: authenticated, isAdmin: admin } }
      ]
    });
  });

  it('redirects an authenticated regular user to access denied', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/admin');
    expect(router.url).toBe('/access-denied');
  });

  it('allows an authenticated administrator', async () => {
    admin.set(true);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/admin');
    expect(router.url).toBe('/admin');
  });

  it('redirects an unauthenticated user to login with a return URL', async () => {
    authenticated.set(false);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/admin');
    expect(router.url).toBe('/login?returnUrl=%2Fadmin');
  });
});
