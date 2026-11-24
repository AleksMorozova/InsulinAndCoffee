import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { authTimingInterceptor } from './auth-timing.interceptor';

describe('Authentication timing', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [
    provideHttpClient(withInterceptors([authTimingInterceptor])), provideHttpClientTesting()
  ] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  for (const [path, stage] of [['auth/login', 'login-http'], ['auth/me', 'profile-http'], ['settings', 'settings-http']]) {
    it(`measures ${stage} without storing credentials, response bodies or URLs`, () => {
      const name = 'iac.auth.' + stage;
      for (let i = 0; i < 2; i++) {
        TestBed.inject(HttpClient).post(`${environment.apiUrl}/${path}`, { password: 'synthetic-secret' }).subscribe();
        TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/${path}`).flush({ accessToken: 'synthetic-token' });
      }
      const entries = performance.getEntriesByName(name);
      expect(entries.length).toBe(1);
      expect(entries[0].duration).toBeGreaterThanOrEqual(0);
      expect(entries[0] instanceof PerformanceMeasure && entries[0].detail).toEqual({ outcome: 'Success' });
      const serialized = JSON.stringify(entries.map(entry => ({ name: entry.name,
        duration: entry.duration, detail: entry instanceof PerformanceMeasure ? entry.detail : null })));
      expect(serialized).not.toContain('synthetic-secret');
      expect(serialized).not.toContain('synthetic-token');
      expect(serialized).not.toContain(environment.apiUrl);
    });
  }

  it('records failures without retrying the login request', () => {
    TestBed.inject(HttpClient).post(`${environment.apiUrl}/auth/login`, {}).subscribe({ error: () => {} });
    TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/auth/login`).flush({}, { status: 401, statusText: 'Unauthorized' });
    const entry = performance.getEntriesByName('iac.auth.login-http')[0];
    expect(entry instanceof PerformanceMeasure && entry.detail).toEqual({ outcome: 'Error' });
  });
});
