import { Component } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-generic-error-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="error-screen">
      <div class="error-screen-icon" aria-hidden="true">!</div>
      <h1>Something went wrong</h1>
      <p>We couldn't load this page. Please try again.</p>
      @if (traceId) {
        <p class="support-reference">Support reference: {{ traceId }}</p>
      }
      <div class="error-screen-actions">
        <button type="button" (click)="tryAgain()">Try again</button>
        <a routerLink="/"><button type="button" class="subtle">Go to home page</button></a>
      </div>
    </section>
  `
})
export class GenericErrorPageComponent {
  readonly traceId = this.route.snapshot.queryParamMap.get('traceId');
  private readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');

  constructor(private readonly route: ActivatedRoute, private readonly router: Router) {}

  tryAgain(): void {
    if (this.returnUrl && this.returnUrl !== '/error') {
      this.router.navigateByUrl(this.returnUrl);
      return;
    }

    window.location.reload();
  }
}