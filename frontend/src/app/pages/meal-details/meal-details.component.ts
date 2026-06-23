import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { resultRatings } from '../../core/models';

@Component({
  selector: 'app-meal-details',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink],
  template: `
    @if (meal$ | async; as meal) {
      <section class="page-title">
        <div>
          <h1>{{ meal.mealType }} Details</h1>
          <p>{{ meal.mealTime | date:'fullDate' }} at {{ meal.mealTime | date:'shortTime' }}</p>
        </div>
        <div class="actions">
          <button type="button" (click)="useMeal(meal)">Create new meal from this meal</button>
          <a routerLink="/history"><button class="subtle">Back</button></a>
        </div>
      </section>

      <section class="grid four">
        <article class="card stat"><span>Total carbs</span><strong>{{ meal.totalCarbs | number:'1.0-1' }} g</strong></article>
        <article class="card stat"><span>Pre-meal glucose</span><strong>{{ meal.preMealGlucose | number:'1.0-1' }}</strong></article>
        <article class="card stat"><span>Suggested bolus</span><strong>{{ meal.suggestedBolus | number:'1.0-2' }} u</strong></article>
        <article class="card stat"><span>Confirmed bolus</span><strong>{{ meal.confirmedBolus | number:'1.0-2' }} u</strong></article>
      </section>

      <section class="grid two">
        <article class="card">
          <h2>Food items</h2>
          <table class="table">
            <thead><tr><th>Food</th><th>Weight</th><th>Carbs/100g</th><th>Carbs</th></tr></thead>
            <tbody>
              @for (item of meal.items; track item.id) {
                <tr>
                  <td>{{ item.foodNameSnapshot }}</td>
                  <td>{{ item.weightGrams | number:'1.0-1' }} g</td>
                  <td>{{ item.carbsPer100gSnapshot | number:'1.0-1' }} g</td>
                  <td>{{ item.calculatedCarbs | number:'1.0-1' }} g</td>
                </tr>
              }
            </tbody>
          </table>
        </article>
        <article class="card">
          <h2>Notes</h2>
          <p>{{ meal.notes || 'No notes saved.' }}</p>
        </article>
      </section>

      <section class="card">
        <h2>Save to Ask Past Me</h2>
        <form [formGroup]="knownMealForm" class="grid" (ngSubmit)="saveKnownMeal(meal.id)">
          <div class="grid two">
            <label>Place name <input formControlName="placeName" placeholder="Sushi Master"></label>
            <label>Dish name <input formControlName="dishName" placeholder="Philadelphia Set"></label>
          </div>
          <div class="grid two">
            <label>Portion description <input formControlName="portionDescription"></label>
            <label>Result
              <select formControlName="resultRating">
                @for (rating of resultRatings; track rating) { <option [value]="rating">{{ rating }}</option> }
              </select>
            </label>
          </div>
          <label>Tags <input formControlName="tags" placeholder="sushi, delivery, dinner"></label>
          <label class="toolbar">
            <input style="width:auto" type="checkbox" formControlName="isFavorite">
            Favorite
          </label>
          <div class="actions">
            <button type="submit" [disabled]="knownMealForm.invalid">Save counted meal</button>
            @if (saveMessage) { <span class="pill">{{ saveMessage }}</span> }
          </div>
        </form>
      </section>
    }
  `
})
export class MealDetailsComponent {
  @Input() id = '';
  resultRatings = resultRatings;
  saveMessage = '';
  meal$ = this.route.paramMap.pipe(switchMap((params) => this.api.getMeal(params.get('id') ?? this.id)));
  knownMealForm = this.fb.nonNullable.group({
    placeName: ['', Validators.required],
    dishName: ['', Validators.required],
    portionDescription: ['Same as logged meal', Validators.required],
    resultRating: ['Unknown', Validators.required],
    tags: [''],
    isFavorite: [true]
  });

  constructor(private readonly api: ApiService, private readonly router: Router, private readonly route: ActivatedRoute, private readonly fb: FormBuilder) {}

  useMeal(meal: any) {
    this.router.navigate(['/calculator'], { state: { meal } });
  }

  saveKnownMeal(mealId: string) {
    if (this.knownMealForm.invalid) return;
    this.api.createKnownMealFromMeal(mealId, {
      ...this.knownMealForm.getRawValue(),
      resultRating: this.knownMealForm.controls.resultRating.value as any
    }).subscribe(() => {
      this.saveMessage = 'Saved';
      this.knownMealForm.reset({
        placeName: '',
        dishName: '',
        portionDescription: 'Same as logged meal',
        resultRating: 'Unknown',
        tags: '',
        isFavorite: true
      });
    });
  }
}
