import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-state',
  standalone: true,
  template: `
    <section class="page-state" aria-live="polite">
      <div class="loading-dot" aria-hidden="true"></div>
      <p>{{ message }}</p>
    </section>
  `
})
export class LoadingStateComponent {
  @Input() message = 'Loading...';
}