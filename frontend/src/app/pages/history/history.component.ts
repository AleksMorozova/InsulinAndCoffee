import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { mealTypes } from '../../core/models';

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
          @for (meal of meals$ | async; track meal.id) {
            <tr>
              <td>{{ meal.mealTime | date:'medium' }}</td>
              <td><span class="pill">{{ meal.mealType }}</span></td>
              <td>{{ meal.foodNames.join(', ') }}</td>
              <td>{{ meal.totalCarbs | number:'1.0-1' }} g</td>
              <td>{{ meal.confirmedBolus | number:'1.0-2' }} u</td>
              <td>{{ meal.preMealGlucose | number:'1.0-1' }}</td>
              <td><a [routerLink]="['/meals', meal.id]"><button class="subtle">Open</button></a></td>
            </tr>
          }
        </tbody>
      </table>
    </section>
  `
})
export class HistoryComponent {
  mealTypes = mealTypes;
  filters = this.fb.group({ search: [''], mealType: [''] });
  meals$ = this.filters.valueChanges.pipe(
    startWith(this.filters.value),
    debounceTime(200),
    switchMap((filters) => this.api.getMeals(filters.search ?? '', filters.mealType ?? ''))
  );

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}
}
