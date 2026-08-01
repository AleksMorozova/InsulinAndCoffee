import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-access-denied-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="error-screen">
      <div class="error-screen-icon" aria-hidden="true">!</div>
      <h1>Access denied</h1>
      <p>You do not have permission to view this page.</p>
      <div class="error-screen-actions">
        <button type="button" class="secondary" (click)="goBack()">Return to previous page</button>
        <a routerLink="/"><button type="button" class="subtle">Go to home page</button></a>
      </div>
    </section>
  `
})
export class AccessDeniedPageComponent {
  constructor(private readonly location: Location) {}

  goBack(): void {
    this.location.back();
  }
}