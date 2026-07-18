import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, combineLatest, switchMap } from 'rxjs';
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
        <article class="card stat">
          <span>Confirmed bolus</span>
          <strong>
            @if (meal.confirmedBolus !== null) {
              {{ meal.confirmedBolus | number:'1.0-2' }} u
            } @else {
              Not confirmed
            }
          </strong>
        </article>
      </section>

      @if (meal.confirmedBolus === null || confirmMessage) {
        <section class="card" id="confirm-insulin">
          <h2>Confirm insulin</h2>
          <p>Save the actual dose when you are ready. A confirmed value of 0 is allowed.</p>
          <form [formGroup]="confirmBolusForm" class="toolbar" (ngSubmit)="confirmBolus(meal.id)">
            <label>Actual dose
              <input type="number" min="0" step="0.1" formControlName="confirmedBolus">
            </label>
            <button type="submit" [disabled]="confirmBolusForm.invalid">Confirm insulin</button>
            @if (confirmMessage) { <span class="pill">{{ confirmMessage }}</span> }
          </form>
        </section>
      }

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
        <h2>Save to Delivery meals</h2>
        <form [formGroup]="deliveryMealForm" class="grid" (ngSubmit)="saveDeliveryMeal(meal.id)">
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
            <button type="submit" [disabled]="deliveryMealForm.invalid || meal.confirmedBolus === null">Save counted meal</button>
            @if (meal.confirmedBolus === null) { <span class="pending-status">Confirm insulin before saving this as a remembered meal.</span> }
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
  confirmMessage = '';
  private readonly refresh$ = new BehaviorSubject(0);
  meal$ = combineLatest([this.route.paramMap, this.refresh$]).pipe(
    switchMap(([params]) => this.api.getMeal(params.get('id') ?? this.id))
  );
  confirmBolusForm = this.fb.nonNullable.group({
    confirmedBolus: [0, [Validators.required, Validators.min(0)]]
  });
  deliveryMealForm = this.fb.nonNullable.group({
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

  saveDeliveryMeal(mealId: string) {
    if (this.deliveryMealForm.invalid) return;
    this.api.createDeliveryMealFromMeal(mealId, {
      ...this.deliveryMealForm.getRawValue(),
      resultRating: this.deliveryMealForm.controls.resultRating.value as any
    }).subscribe(() => {
      this.saveMessage = 'Saved';
      this.deliveryMealForm.reset({
        placeName: '',
        dishName: '',
        portionDescription: 'Same as logged meal',
        resultRating: 'Unknown',
        tags: '',
        isFavorite: true
      });
    });
  }

  confirmBolus(mealId: string) {
    if (this.confirmBolusForm.invalid) return;

    this.api.confirmMealBolus(mealId, this.confirmBolusForm.getRawValue()).subscribe(() => {
      this.confirmMessage = 'Confirmed';
      this.refresh$.next(this.refresh$.value + 1);
    });
  }
}
