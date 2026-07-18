import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Dashboard, DashboardMeal } from '../../core/models';

interface DashboardViewState {
  loading: boolean;
  dashboard: Dashboard | null;
  pendingMeals: DashboardMeal[];
  error: string | null;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  template: `
    @if (state$ | async; as state) {
      <section class="dashboard-action-grid" aria-label="Primary actions">
        <a class="dashboard-action-card primary" routerLink="/calculator" aria-label="Open calculator to log a new meal">
          <span class="dashboard-action-illustration" aria-hidden="true">
            <svg viewBox="0 0 24 24" focusable="false">
              <path d="M8 4.5c.8.7.8 1.6 0 2.3" />
              <path d="M12 4.5c.8.7.8 1.6 0 2.3" />
              <path d="M16 4.5c.8.7.8 1.6 0 2.3" />
              <path d="M5.5 9h11v5.5a4 4 0 0 1-4 4h-3a4 4 0 0 1-4-4V9Z" />
              <path d="M16.5 10.5h1a2.5 2.5 0 0 1 0 5h-1" />
              <path d="M4 20h15" />
            </svg>
          </span>
          <span class="dashboard-action-kicker">TIME TO REFUEL?</span>
          <strong>New Meal</strong>
          <span>Log what you're eating and calculate your insulin.</span>
          <span class="dashboard-action-cta">Open calculator <span aria-hidden="true">→</span></span>
        </a>

        <a class="dashboard-action-card memory" routerLink="/delivery-meals" aria-label="Open Ask Past Me">
          <span class="dashboard-action-illustration" aria-hidden="true">
            <svg viewBox="0 0 32 24" focusable="false">
              <ellipse cx="10" cy="13" rx="6.5" ry="5.2" />
              <ellipse cx="10" cy="13" rx="2.8" ry="2.2" />
              <ellipse cx="22" cy="13" rx="6.5" ry="5.2" />
              <ellipse cx="22" cy="13" rx="2.8" ry="2.2" />
              <path d="M6.8 8.4c1.9.9 4.5.9 6.4 0" />
              <path d="M18.8 8.4c1.9.9 4.5.9 6.4 0" />
              <path d="M9 6.5 25 3" />
              <path d="M11 7.7 27 4.2" />
            </svg>
          </span>
          <span class="dashboard-action-kicker">DON'T DO THE MATH TWICE</span>
          <strong>Ask Past Me</strong>
          <span>Your past self already did the math.</span>
          <span class="dashboard-action-cta">Ask your order history <span aria-hidden="true">→</span></span>
        </a>
      </section>

      @if (state.error) {
        <section class="card dashboard-message error">
          <h2>Couldn’t load today’s dashboard</h2>
          <p>{{ state.error }}</p>
          <button type="button" class="secondary" (click)="reload()">Try again</button>
        </section>
      } @else if (!state.loading) {
        @if (state.dashboard; as dashboard) {
          @if (state.pendingMeals.length > 0) {
            <section class="dashboard-section">
              <div class="section-head">
                <div>
                  <h2>Needs your attention</h2>
                  <p>Meals from today with insulin still unconfirmed.</p>
                </div>
              </div>
              <div class="attention-list">
                @for (meal of state.pendingMeals; track meal.id) {
                  <article class="card attention-card">
                    <div>
                      <span class="pill">{{ meal.mealType }}</span>
                      <h3>{{ meal.totalCarbs | number:'1.0-1' }} g carbs</h3>
                      <p>Insulin has not been confirmed</p>
                    </div>
                    <a [routerLink]="['/meals', meal.id]" [queryParams]="{ confirm: 'insulin' }"><button class="secondary">Confirm insulin</button></a>
                  </article>
                }
              </div>
            </section>
          }

          <section class="dashboard-section">
            <div class="section-head">
              <div class="dashboard-meals-heading">
                <h2>Today’s meals</h2>
                <p>{{ dashboard.date | date:'fullDate' }}</p>
              </div>
              <a routerLink="/history" class="dashboard-link">View history</a>
            </div>

            @if (dashboard.meals.length > 0) {
              <div class="today-meal-list">
                @for (meal of dashboard.meals; track meal.id) {
                  <a class="today-meal-row" [routerLink]="['/meals', meal.id]">
                    <time>{{ meal.mealTime | date:'shortTime' }}</time>
                    <strong>{{ meal.mealType }}</strong>
                    <span>{{ meal.totalCarbs | number:'1.0-1' }} g</span>
                    @if (meal.confirmedInsulin !== null) {
                      <span>{{ meal.confirmedInsulin | number:'1.0-2' }} u</span>
                    } @else {
                      <span class="pending-status">Not confirmed</span>
                    }
                  </a>
                }
              </div>
            } @else {
              <div class="empty-state dashboard-empty">
                <h2>No meals yet today</h2>
                <p>Let's fix that — every bite counts.</p>
              </div>
            }
          </section>

          <p class="dashboard-summary-line dashboard-daily-totals" aria-label="Today summary">
            <span class="dashboard-total-icon" aria-hidden="true">🌾</span>
            <strong>{{ dashboard.totalCarbs | number:'1.0-1' }} g</strong>
            <span>carbs today</span>
            <span class="dashboard-summary-separator" aria-hidden="true">•</span>
            <span class="dashboard-total-icon" aria-hidden="true">💉</span>
            <strong>{{ dashboard.confirmedInsulin | number:'1.0-2' }} u</strong>
            <span>confirmed insulin</span>
          </p>
        }
      }
    }
  `
})
export class DashboardComponent {
  state$ = this.loadDashboard();

  constructor(private readonly api: ApiService) {}

  reload() {
    this.state$ = this.loadDashboard();
  }

  private loadDashboard() {
    return this.api.getDashboard().pipe(
      map((dashboard): DashboardViewState => ({
        loading: false,
        dashboard,
        pendingMeals: dashboard.meals.filter((meal) => meal.requiresInsulinConfirmation),
        error: null
      })),
      startWith({
        loading: true,
        dashboard: null,
        error: null,
        pendingMeals: []
      } satisfies DashboardViewState),
      catchError(() => of({
        loading: false,
        dashboard: null,
        pendingMeals: [],
        error: 'Check your connection and try again.'
      } satisfies DashboardViewState))
    );
  }
}
