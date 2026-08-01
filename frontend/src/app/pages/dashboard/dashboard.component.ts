import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Dashboard, DashboardMeal } from '../../core/models';
import { PageErrorComponent } from '../../shared/page-error/page-error.component';

interface DashboardViewState {
  loading: boolean;
  dashboard: Dashboard | null;
  pendingMeals: DashboardMeal[];
  error: string | null;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink, PageErrorComponent],
  template: `
    @if (state$ | async; as state) {
      <section class="dashboard-action-grid" aria-label="Primary actions">
        <a class="dashboard-action-card primary" routerLink="/calculator" aria-label="Open calculator to log a new meal">
          <span class="dashboard-action-illustration" aria-hidden="true">
            <svg viewBox="0 0 220 150" focusable="false">
              <ellipse class="soft-shadow" cx="118" cy="124" rx="78" ry="14" />
              <path class="steam" d="M101 30c10 12-8 20 2 32" />
              <path class="steam" d="M121 22c12 14-9 23 3 37" />
              <path class="steam" d="M141 32c9 11-7 18 2 30" />
              <ellipse class="plate" cx="116" cy="103" rx="70" ry="23" />
              <path class="cup" d="M91 54h59v31a23 23 0 0 1-23 23h-13a23 23 0 0 1-23-23V54Z" />
              <path class="cup" d="M151 63h7c12 0 18 8 18 17s-6 17-18 17h-9" />
              <ellipse class="coffee" cx="120.5" cy="57" rx="29.5" ry="8.5" />
              <circle class="sushi salmon" cx="86" cy="104" r="16" />
              <circle class="sushi rice" cx="122" cy="107" r="15" />
              <circle class="sushi nori" cx="153" cy="104" r="15" />
              <circle class="sushi-core" cx="122" cy="107" r="7" />
              <circle class="wasabi" cx="65" cy="115" r="6" />
            </svg>
          </span>
          <span class="dashboard-action-kicker">TIME TO REFUEL?</span>
          <strong>New Meal</strong>
          <span>Log what you're eating and calculate your insulin.</span>
          <span class="dashboard-action-cta"><span aria-hidden="true">+</span> Open calculator <span aria-hidden="true">→</span></span>
        </a>

        <a class="dashboard-action-card memory" routerLink="/delivery-meals" aria-label="Open Ask Past Me">
          <span class="dashboard-action-illustration" aria-hidden="true">
            <svg viewBox="0 0 220 150" focusable="false">
              <ellipse class="soft-shadow" cx="131" cy="126" rx="76" ry="13" />
              <path class="book-cover" d="M78 41l78 20-18 68-79-21 19-67Z" />
              <path class="book-page" d="M89 47l57 15-15 55-57-15 15-55Z" />
              <path class="pencil" d="M54 111 114 41" />
              <path class="pencil-tip" d="M114 41l10-4-4 10" />
              <path class="note-line" d="M98 75c11 0 18 3 25 10" />
              <path class="note-line" d="M93 91c13 0 22 4 30 12" />
              <path class="glasses" d="M137 40c16-12 36 2 29 18-6 13-25 12-31 0-2-5-1-12 2-18Z" />
              <path class="glasses" d="M174 47c17-11 35 5 26 20-7 12-27 8-31-5-1-5 1-11 5-15Z" />
              <path class="glasses" d="M164 55l8 3" />
            </svg>
          </span>
          <span class="dashboard-action-kicker">DON'T DO THE MATH TWICE</span>
          <strong>Ask Past Me</strong>
          <span>Find a previous order and reuse your insulin math.</span>
          <span class="dashboard-action-cta muted-cta"><span aria-hidden="true">⌕</span> Ask your order history <span aria-hidden="true">→</span></span>
        </a>
      </section>

      @if (state.error) {
        <app-page-error
          title="Couldn’t load today’s dashboard"
          [message]="state.error"
          (retry)="reload()" />
      } @else if (!state.loading) {
        @if (state.dashboard; as dashboard) {
          <section class="dashboard-section">
            <div class="section-head">
              <div class="dashboard-section-title">
                <span class="dashboard-section-icon" aria-hidden="true">🍴</span>
                <div class="dashboard-meals-heading">
                  <h2>Today’s meals</h2>
                  <p>{{ dashboard.date | date:'fullDate' }}</p>
                </div>
              </div>
              <a routerLink="/history" class="dashboard-link">View all history <span aria-hidden="true">→</span></a>
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
                <span class="dashboard-empty-icon" aria-hidden="true">☕</span>
                <h2>No meals yet today</h2>
                <p>Your meals will appear here once you add one.</p>
                <a routerLink="/calculator"><button type="button"><span aria-hidden="true">+</span> Log your first meal</button></a>
              </div>
            }
          </section>

          <section class="dashboard-daily-totals" aria-label="Today summary">
            <div class="dashboard-total-cell">
              <span class="dashboard-total-icon carbs" aria-hidden="true">🌾</span>
              <div>
                <strong>{{ dashboard.totalCarbs | number:'1.0-1' }} g</strong>
                <span>carbs today</span>
              </div>
            </div>
            <div class="dashboard-total-cell">
              <span class="dashboard-total-icon insulin" aria-hidden="true">💉</span>
              <div>
                <strong>{{ dashboard.confirmedInsulin | number:'1.0-2' }} u</strong>
                <span>confirmed insulin</span>
              </div>
            </div>
            <div class="dashboard-total-cell">
              <span class="dashboard-total-icon pending" aria-hidden="true">!</span>
              <div>
                <strong>
                  @if (state.pendingMeals.length > 0) {
                    {{ state.pendingMeals.length }} pending {{ state.pendingMeals.length === 1 ? 'action' : 'actions' }}
                  } @else {
                    No pending actions
                  }
                </strong>
                <span>
                  @if (state.pendingMeals.length > 0) {
                    Insulin confirmation needed
                  } @else {
                    You’re all set!
                  }
                </span>
              </div>
            </div>
          </section>
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
