import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, debounceTime, map, of, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { MealSummary, mealTypes } from '../../core/models';
import { getApiErrorMessage } from '../../core/api-error';

interface HistoryState { loading: boolean; meals: MealSummary[]; error: string; }

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink],
  template: `
    <section class="page-title">
      <div>
        <h1>Meal History</h1>
        <p>Review saved meals and search by food snapshot names.</p>
      </div>
      <a routerLink="/calculator"><button>New Meal</button></a>
    </section>

    <section class="card">
      <form [formGroup]="filters" class="toolbar">
        <label>Search food
          <input formControlName="search" placeholder="Bread, latte, borscht">
        </label>
        <label>Meal type
          <select formControlName="mealType">
            <option value="">All</option>
            @for (type of mealTypes; track type) { <option [value]="type">{{ type }}</option> }
          </select>
        </label>
      </form>
    </section>

    <section class="card">
      @if (state$ | async; as state) {
      @if (state.loading) {
        <p role="status">Loading meal history...</p>
      } @else if (state.error) {
        <p role="alert">{{ state.error }}</p>
        <button type="button" (click)="retry()">Try again</button>
      } @else if (!state.meals.length) {
        <div class="empty-state">
          <h2>{{ filters.value.search || filters.value.mealType ? 'No meals match these filters' : 'No meals yet' }}</h2>
          <p>Your saved meals will appear here. You can start with an empty food library.</p>
          <a routerLink="/calculator">Create your first meal</a>
        </div>
      } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Type</th>
            <th>Foods</th>
            <th>Carbs</th>
            <th>Confirmed</th>
            <th>Glucose</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (meal of state.meals; track meal.id) {
            <tr>
              <td>{{ meal.mealTime | date:'medium' }}</td>
              <td><span class="pill">{{ meal.mealType }}</span></td>
              <td>{{ meal.foodNames.join(', ') }}</td>
              <td>{{ meal.totalCarbs | number:'1.0-1' }} g</td>
              <td>
                @if (meal.confirmedBolus !== null) {
                  {{ meal.confirmedBolus | number:'1.0-2' }} u
                } @else {
                  <span class="pending-status">Not confirmed</span>
                }
              </td>
              <td>{{ meal.preMealGlucose | number:'1.0-1' }}</td>
              <td><a [routerLink]="['/meals', meal.id]"><button class="subtle">Open</button></a></td>
            </tr>
          }
        </tbody>
      </table>
      }
      }
    </section>
  `
})
export class HistoryComponent {
  mealTypes = mealTypes;
  filters = this.fb.group({ search: [''], mealType: [''] });
  private readonly refresh$ = new BehaviorSubject(0);
  state$ = combineLatest([
    this.filters.valueChanges.pipe(startWith(this.filters.value), debounceTime(200)), this.refresh$
  ]).pipe(
    switchMap(([filters]) => this.api.getMeals(filters.search ?? '', filters.mealType ?? '').pipe(
      map((meals): HistoryState => ({ loading: false, meals, error: '' })),
      startWith({ loading: true, meals: [], error: '' } satisfies HistoryState),
      catchError(error => of({ loading: false, meals: [], error: getApiErrorMessage(error, 'Could not load meal history. Please try again.') } satisfies HistoryState))
    ))
  );

  retry() { this.refresh$.next(this.refresh$.value + 1); }

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}
}
