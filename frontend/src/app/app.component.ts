import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="app-header">
      <a class="brand" routerLink="/">
        <span class="brand-mark">I&C</span>
        <span>
          <strong>Insulin & Coffee</strong>
          <small>Run on coffee and insulin</small>
        </span>
      </a>
      <nav>
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M3 11.5 12 4l9 7.5" />
            <path d="M5.5 10.5V20h13v-9.5" />
            <path d="M9.5 20v-6h5v6" />
          </svg>
          Dashboard
        </a>
        <a routerLink="/calculator" routerLinkActive="active">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <rect x="6" y="3.5" width="12" height="17" rx="2" />
            <path d="M9 7.5h6" />
            <path d="M9 11h.01M12 11h.01M15 11h.01M9 14h.01M12 14h.01M15 14h.01M9 17h.01M12 17h.01M15 17h.01" />
          </svg>
          Calculator
        </a>
        <a routerLink="/history" routerLinkActive="active" [class.active]="router.url.startsWith('/meals/')">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <circle cx="12" cy="12" r="8.5" />
            <path d="M12 7.5V12l3 2" />
          </svg>
          History
        </a>
        <a routerLink="/delivery-meals" routerLinkActive="active">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M5 17.5c-1.4-1.3-2-2.9-2-4.8C3 8.4 6.7 5 12 5s9 3.4 9 7.7-3.7 7.7-9 7.7c-1.1 0-2.1-.1-3-.4L5 21v-3.5Z" />
            <path d="M8.5 12.5h.01M12 12.5h.01M15.5 12.5h.01" />
          </svg>
          Ask Past Me
        </a>
        <a routerLink="/foods" routerLinkActive="active">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M5 12.5h14a7 7 0 0 1-14 0Z" />
            <path d="M8 12.5c-.7-2.7.7-4.3 2.3-5.2M12 12.5c-.7-2.7.7-4.3 2.3-5.2" />
          </svg>
          Foods
        </a>
        <a routerLink="/supplies" routerLinkActive="active">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="m12 3 7 4v9.5l-7 4-7-4V7l7-4Z" />
            <path d="m5 7 7 4 7-4" />
            <path d="M12 11v9.5" />
          </svg>
          Supplies
        </a>
        <a routerLink="/settings" routerLinkActive="active">
          <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7Z" />
            <path d="M19 12a7 7 0 0 0-.1-1.2l2-1.5-2-3.4-2.4 1a7 7 0 0 0-2-1.2L14.2 3h-4.4l-.4 2.7a7 7 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.5A7 7 0 0 0 5 12c0 .4 0 .8.1 1.2l-2 1.5 2 3.4 2.4-1a7 7 0 0 0 2 1.2l.4 2.7h4.4l.4-2.7a7 7 0 0 0 2-1.2l2.4 1 2-3.4-2-1.5c.1-.4.1-.8.1-1.2Z" />
          </svg>
          Settings
        </a>
      </nav>
    </header>
    <main>
      <router-outlet />
    </main>
    <footer class="app-footer">
      ☕ Powered by coffee • 💉 Powered by insulin • ❤️ Inspired by Taisha's legendary spreadsheet.
    </footer>
  `
})
export class AppComponent {
  constructor(readonly router: Router) {}
}
