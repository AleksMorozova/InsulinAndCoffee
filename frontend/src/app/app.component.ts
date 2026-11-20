import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './shared/toast-container/toast-container.component';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastContainerComponent],
  template: `
    @if (auth.isAuthenticated()) {
    <header class="app-header" (keydown.escape)="closeMenu($event, navToggle, userToggle)">
      <a class="brand" routerLink="/dashboard" (click)="openMenu = null">
        <span class="brand-mark">I&C</span>
        <span>
          <strong>Insulin & Coffee</strong>
          <small>Run on coffee and insulin</small>
        </span>
      </a>
      <button #navToggle type="button" class="header-toggle nav-toggle" aria-controls="primary-navigation"
        [attr.aria-expanded]="openMenu === 'navigation'" (click)="toggleMenu('navigation')">
        Menu
      </button>
      <nav id="primary-navigation" aria-label="Primary" [class.is-open]="openMenu === 'navigation'">
        <a routerLink="/calculator" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <rect x="6" y="3.5" width="12" height="17" rx="2" />
            <path d="M9 7.5h6" />
            <path d="M9 11h.01M12 11h.01M15 11h.01M9 14h.01M12 14h.01M15 14h.01M9 17h.01M12 17h.01M15 17h.01" />
          </svg>
          Calculator
        </a>
        <a routerLink="/history" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null" [class.active]="router.url.startsWith('/meals/')">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <circle cx="12" cy="12" r="8.5" />
            <path d="M12 7.5V12l3 2" />
          </svg>
          History
        </a>
        <a routerLink="/delivery-meals" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M5 17.5c-1.4-1.3-2-2.9-2-4.8C3 8.4 6.7 5 12 5s9 3.4 9 7.7-3.7 7.7-9 7.7c-1.1 0-2.1-.1-3-.4L5 21v-3.5Z" />
            <path d="M8.5 12.5h.01M12 12.5h.01M15.5 12.5h.01" />
          </svg>
          Ask Past Me
        </a>
        <a routerLink="/foods" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M5 12.5h14a7 7 0 0 1-14 0Z" />
            <path d="M8 12.5c-.7-2.7.7-4.3 2.3-5.2M12 12.5c-.7-2.7.7-4.3 2.3-5.2" />
          </svg>
          Foods
        </a>
        <a routerLink="/supplies" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="m12 3 7 4v9.5l-7 4-7-4V7l7-4Z" />
            <path d="m5 7 7 4 7-4" />
            <path d="M12 11v9.5" />
          </svg>
          Supplies
        </a>
        <a routerLink="/meal-assistant" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M12 3c0 5-2 7-7 7 5 0 7 2 7 7 0-5 2-7 7-7-5 0-7-2-7-7ZM19 15v6m-3-3h6" />
          </svg>
          AI Assistant
        </a>
      </nav>
      <div class="user-menu">
        <button #userToggle type="button" class="header-toggle" aria-controls="user-navigation"
          [attr.aria-expanded]="openMenu === 'user'" (click)="toggleMenu('user')">
          Account <span aria-hidden="true">▾</span>
        </button>
        <div id="user-navigation" class="user-dropdown" [hidden]="openMenu !== 'user'">
          <span class="account-name">{{ auth.user()?.username }}</span>
          <a routerLink="/settings" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">Settings</a>
          @if (auth.isAdmin()) {
            <a routerLink="/admin/login-attempts" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="openMenu = null">Login History</a>
          }
          <button type="button" (click)="logout()">Logout</button>
        </div>
      </div>
    </header>
    }
    <main>
      <router-outlet />
    </main>
    <app-toast-container />
    <footer class="app-footer">
      ☕ Powered by coffee • 💉 Powered by insulin • ❤️ Inspired by Taisha's legendary spreadsheet.
    </footer>
  `
})
export class AppComponent {
  openMenu: 'navigation' | 'user' | null = null;

  toggleMenu(menu: 'navigation' | 'user') {
    this.openMenu = this.openMenu === menu ? null : menu;
  }

  closeMenu(event: Event, navToggle: HTMLButtonElement, userToggle: HTMLButtonElement) {
    if (!this.openMenu) return;
    event.preventDefault();
    (this.openMenu === 'user' ? userToggle : navToggle).focus();
    this.openMenu = null;
  }

  logout() {
    this.openMenu = null;
    this.auth.logout();
  }

  constructor(readonly router: Router, readonly auth: AuthService) {}
}
