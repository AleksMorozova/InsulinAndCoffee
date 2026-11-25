import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { routes } from './app.routes';
import { AuthService } from './core/auth.service';
import { ApiService } from './core/api.service';
import { MealAssistantComponent } from './pages/meal-assistant.component';
import { inResponsiveFrame } from './testing/responsive-frame';

@Component({ standalone: true, template: '<h1>Routed page</h1>' })
class LayoutPage {}

describe('Shared routed layout', () => {
  it('removes only the routing anchor across all route layouts and gives chat the available height', async () => {
    const paths = routes.map(route => route.path).filter((path): path is string => !!path && path !== '**');
    await TestBed.configureTestingModule({ imports: [AppComponent], providers: [
      provideRouter(paths.map(path => ({ path, component: path === 'meal-assistant' ? MealAssistantComponent : LayoutPage }))),
      { provide: AuthService, useValue: { isAuthenticated: () => true, isAdmin: () => false, user: () => ({ username: 'Test' }) } },
      { provide: ApiService, useValue: { getMealAssistantConversations: () => of([]) } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(AppComponent); fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    // TestBed uses a generated host tag; retain the production app-root selector.
    const root = document.createElement('app-root');
    host.parentNode?.insertBefore(root, host);
    try {
      for (const path of paths) {
        await TestBed.inject(Router).navigateByUrl('/' + path.replace(':id', 'test-meal'));
        fixture.detectChanges(); await fixture.whenStable();
        root.append(...Array.from(host.childNodes));
        await inResponsiveFrame(root, viewport => {
          const main = root.querySelector('main'); const outlet = root.querySelector('router-outlet');
          if (!main || !outlet || !outlet.nextElementSibling) throw new Error('Missing routed layout');
          expect(viewport.getComputedStyle(outlet).display).withContext(path).toBe('none');
          if (path === 'meal-assistant') {
            const page = outlet.nextElementSibling.getBoundingClientRect();
            expect(page.top - main.getBoundingClientRect().top).toBeLessThan(14);
            expect(page.bottom).toBeLessThanOrEqual(viewport.innerHeight);
            expect(page.height).toBeGreaterThan(500);
          } else expect(viewport.getComputedStyle(main).display).toBe('block');
        }, [[375, 812], [1440, 900]]);
        host.append(...Array.from(root.childNodes));
      }
    } finally { host.append(...Array.from(root.childNodes)); root.remove(); }
  });
});
