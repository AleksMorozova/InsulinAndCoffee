import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { loginDestination } from './login-destination';
import { AuthService } from './auth.service';

export const guestGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? loginDestination(inject(Router), route.queryParamMap.get('returnUrl'))
    : true;
};
