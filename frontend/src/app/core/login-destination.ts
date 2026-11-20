import { PRIMARY_OUTLET, Router, UrlTree } from '@angular/router';

export function loginDestination(router: Router, returnUrl: string | null): UrlTree {
  const fallback = router.createUrlTree(['/calculator']);
  if (!returnUrl || !/^\/(?!\/)/.test(returnUrl) || /[\\\u0000-\u001f]/.test(returnUrl)) return fallback;

  try {
    const target = router.parseUrl(returnUrl);
    const primary = target.root.children[PRIMARY_OUTLET];
    if (!primary || Object.keys(target.root.children).length !== 1 || Object.keys(primary.children).length) return fallback;
    const path = primary.segments.map(segment => segment.path);
    const allowed = router.config.some(route => route.canActivate?.length && route.path !== 'login' &&
      route.path !== 'register' && route.path?.split('/').length === path.length &&
      route.path.split('/').every((part, index) => part.startsWith(':') || part === path[index]));
    return allowed ? target : fallback;
  } catch {
    return fallback;
  }
}
