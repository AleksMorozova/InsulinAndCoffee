import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { LoginComponent } from '../pages/auth/login.component';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { guestGuard } from './guest.guard';

@Component({ standalone: true, template: 'Protected page' })
class ProtectedPage {}

describe('Login navigation', () => {
  const keys = ['access-token', 'expires-at', 'user'].map(key => 'insulin-and-coffee.' + key);

  beforeEach(() => {
    keys.forEach(key => localStorage.removeItem(key));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([
          { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
          { path: 'calculator', component: ProtectedPage, canActivate: [authGuard] },
          { path: 'meal-assistant', component: ProtectedPage, canActivate: [authGuard] }
        ])
      ]
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    keys.forEach(key => localStorage.removeItem(key));
  });

  function submit(component: LoginComponent) {
    component.form.setValue({ username: 'tester', password: 'test-password' });
    component.submit();
    return TestBed.inject(HttpTestingController).expectOne(request => request.url.endsWith('/auth/login'));
  }

  function completeLogin(request: ReturnType<typeof submit>) {
    request.flush({
      accessToken: 'test-token', expiresAt: new Date(Date.now() + 60000).toISOString(),
      user: { id: 'test-id', username: 'tester', role: 'User' }
    });
  }

  for (const [url, expected] of [
    ['/login', '/calculator'],
    ['/meal-assistant?tab=history', '/meal-assistant?tab=history'],
    ['/login?returnUrl=https%3A%2F%2Fevil.example', '/calculator'],
    ['/login?returnUrl=%2Flogin', '/calculator']
  ]) {
    it('navigates after successful sign-in from ' + url, async () => {
      const harness = await RouterTestingHarness.create();
      const component = await harness.navigateByUrl(url, LoginComponent);
      completeLogin(submit(component));
      await harness.fixture.whenStable();

      const router = TestBed.inject(Router);
      const auth = TestBed.inject(AuthService);
      expect(auth.isAuthenticated()).toBeTrue();
      expect(router.url).toBe(expected);
      const restored = new AuthService(TestBed.inject(HttpClient), router);
      expect(restored.isAuthenticated()).toBeTrue();

      await router.navigateByUrl('/login');
      expect(router.url).toBe('/calculator');
      auth.logout();
      await harness.fixture.whenStable();
      expect(router.url).toBe('/login');
      expect(auth.isAuthenticated()).toBeFalse();
    });
  }

  it('keeps the username and stays on login after rejected credentials', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/login', LoginComponent);
    submit(component).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(component.form.value.username).toBe('tester');
    expect(component.form.value.password).toBe('');
  });

  it('redirects an expired session to login with the protected destination', async () => {
    localStorage.setItem(keys[0], 'expired-token');
    localStorage.setItem(keys[1], new Date(0).toISOString());
    localStorage.setItem(keys[2], JSON.stringify({ id: 'test-id', role: 'User' }));
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/meal-assistant', LoginComponent);
    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fmeal-assistant');
  });
});
