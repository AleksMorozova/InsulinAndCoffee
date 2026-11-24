import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { finalize, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { recordAuthTiming } from './auth-performance';

const stages = new Map([
    [`${environment.apiUrl}/auth/login`, 'login-http'],
    [`${environment.apiUrl}/auth/google`, 'google-http'],
    [`${environment.apiUrl}/auth/me`, 'profile-http'],
    [`${environment.apiUrl}/settings`, 'settings-http']
]);

export const authTimingInterceptor: HttpInterceptorFn = (request, next) => {
  const stage = stages.get(request.url);
  if (!stage) return next(request);
  const start = performance.now();
  let outcome = 'Cancelled';
  return next(request).pipe(
    tap({ next: response => { if (response instanceof HttpResponse) outcome = 'Success'; }, error: () => outcome = 'Error' }),
    finalize(() => recordAuthTiming(stage, start, outcome))
  );
};
