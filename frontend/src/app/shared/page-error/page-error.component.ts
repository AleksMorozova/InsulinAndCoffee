import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-page-error',
  standalone: true,
  template: `
    <section class="page-state page-state-error" role="alert">
      <div class="page-state-icon" aria-hidden="true">!</div>
      <h2>{{ title }}</h2>
      <p>{{ message }}</p>
      @if (showRetry) {
        <button type="button" class="secondary" (click)="retry.emit()">{{ retryLabel }}</button>
      }
    </section>
  `
})
export class PageErrorComponent {
  @Input() title = 'Unable to load this page';
  @Input() message = 'Please try again.';
  @Input() retryLabel = 'Try again';
  @Input() showRetry = true;
  @Output() retry = new EventEmitter<void>();
}