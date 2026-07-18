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
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Dashboard</a>
        <a routerLink="/calculator" routerLinkActive="active">Calculator</a>
        <a routerLink="/history" routerLinkActive="active" [class.active]="router.url.startsWith('/meals/')">History</a>
        <a routerLink="/delivery-meals" routerLinkActive="active">Ask Past Me</a>
        <a routerLink="/foods" routerLinkActive="active">Foods</a>
        <a routerLink="/supplies" routerLinkActive="active">Supplies</a>
        <a routerLink="/settings" routerLinkActive="active">Settings</a>
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
