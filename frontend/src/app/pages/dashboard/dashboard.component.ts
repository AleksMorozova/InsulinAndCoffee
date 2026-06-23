import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="page-title">
      <div>
        <h1>Today</h1>
        <p>A calm snapshot of carbs, confirmed insulin, and the latest meal.</p>
      </div>
      <div class="actions">
        <a routerLink="/calculator"><button>New Meal</button></a>
        <a routerLink="/history"><button class="subtle">Use Previous Meal</button></a>
      </div>
    </section>

    @if (dashboard$ | async; as dashboard) {
      <section class="grid three">
        <article class="card stat">
          <span>Today's total carbs</span>
          <strong>{{ dashboard.todaysTotalCarbs | number:'1.0-1' }} g</strong>
        </article>
        <article class="card stat">
          <span>Confirmed insulin</span>
          <strong>{{ dashboard.todaysConfirmedInsulinUnits | number:'1.0-2' }} u</strong>
        </article>
        <article class="card">
          <h2>Last meal</h2>
          @if (dashboard.lastMeal) {
            <p><span class="pill">{{ dashboard.lastMeal.mealType }}</span></p>
            <h3>{{ dashboard.lastMeal.totalCarbs | number:'1.0-1' }} g carbs</h3>
            <p>{{ dashboard.lastMeal.mealTime | date:'medium' }}</p>
            <a [routerLink]="['/meals', dashboard.lastMeal.id]"><button class="secondary">Open details</button></a>
          } @else {
            <p>No meals saved yet.</p>
          }
        </article>
      </section>
    }
  `
})
export class DashboardComponent {
  dashboard$ = this.api.getDashboard();
  constructor(private readonly api: ApiService) {}
}
