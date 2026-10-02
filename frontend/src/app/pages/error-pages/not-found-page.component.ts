import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="error-screen">
      <div class="error-screen-icon" aria-hidden="true">?</div>
      <h1>Page not found</h1>
      <p>The page you were looking for does not exist or has moved.</p>
      <a routerLink="/dashboard"><button type="button">Go to dashboard</button></a>
    </section>
  `
})
export class NotFoundPageComponent {}
