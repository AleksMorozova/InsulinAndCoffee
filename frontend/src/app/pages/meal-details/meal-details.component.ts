import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { ApiError } from '../../core/api-error';
import { toApiError } from '../../core/api-error.mapper';
import { ApiService } from '../../core/api.service';
import { FoodItem, FoodMeasurementType, MealDetail } from '../../core/models';
import { LoadingStateComponent } from '../../shared/loading-state/loading-state.component';
import { PageErrorComponent } from '../../shared/page-error/page-error.component';

type MealDetailsState =
  | { kind: 'loading' }
  | { kind: 'loaded'; meal: MealDetail }
  | { kind: 'not-found' }
  | { kind: 'error'; error: ApiError };

@Component({
  selector: 'app-meal-details',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink, LoadingStateComponent, PageErrorComponent],
  template: `
    @if (state$ | async; as state) {
      @if (state.kind === 'loading') {
        <app-loading-state message="Loading meal details..." />
      } @else if (state.kind === 'not-found') {
        <section class="meal-missing-state" role="status">
          <div class="meal-missing-icon" aria-hidden="true">?</div>
          <h1>We couldn’t find this meal</h1>
          <p>It may have been deleted, or the link you followed is no longer available.</p>
          <div class="meal-missing-actions">
            <a routerLink="/history"><button type="button">Back to history</button></a>
            <a routerLink="/calculator"><button type="button" class="subtle">Create a meal</button></a>
          </div>
        </section>
      } @else if (state.kind === 'error') {
        <app-page-error
          title="Unable to load this meal"
          message="Something went wrong while loading the meal. Please try again."
          (retry)="retryLoad()" />
        <div class="meal-error-actions">
          <a routerLink="/history"><button type="button" class="subtle">Back to history</button></a>
        </div>
      } @else {
        <section class="meal-details-head">
          <div>
            <p class="meal-details-eyebrow">Meal record</p>
            <h1>{{ state.meal.mealType }} Details</h1>
            <p class="meal-details-subline">{{ state.meal.mealTime | date:'fullDate' }} at {{ state.meal.mealTime | date:'shortTime' }}</p>
          </div>
          <div class="meal-details-status">
            <strong class="meal-details-status-badge" [class.confirmed]="state.meal.confirmedBolus !== null">
              {{ state.meal.confirmedBolus !== null ? 'Confirmed' : 'Pending' }}
            </strong>
            <span>
              @if (state.meal.confirmedBolus !== null) {
                {{ state.meal.confirmedBolus | number:'1.0-2' }} u recorded
              } @else {
                Insulin not confirmed
              }
            </span>
          </div>
        </section>

        <section class="meal-action-panel" [class.done]="state.meal.confirmedBolus !== null" id="confirm-insulin">
          <div class="meal-action-kicker">
            <span></span>
            <strong>{{ state.meal.confirmedBolus !== null ? 'Confirmed' : 'Needs your input' }}</strong>
          </div>
          <h2>{{ state.meal.confirmedBolus !== null ? 'Insulin confirmed' : 'Confirm insulin' }}</h2>
          <div class="meal-action-body">
            <div class="meal-details-stats">
              <article>
                <span>Total carbs</span>
                <strong>{{ state.meal.totalCarbs | number:'1.0-1' }}<small>g</small></strong>
              </article>
              <article>
                <span>Pre-meal glucose</span>
                <strong>{{ state.meal.preMealGlucose | number:'1.0-1' }}</strong>
              </article>
              <article>
                <span>Suggested bolus</span>
                <strong class="accent">{{ state.meal.suggestedBolus | number:'1.0-2' }}<small>u</small></strong>
              </article>
            </div>
          </div>
          <div class="meal-notes-strip">
            <span>Notes</span>
            <p [class.empty]="!state.meal.notes">{{ state.meal.notes || 'No saved notes for this meal.' }}</p>
          </div>
          @if (state.meal.confirmedBolus === null) {
            <form [formGroup]="confirmBolusForm" class="meal-confirm-form" (ngSubmit)="confirmBolus(state.meal.id)">
              <p class="meal-action-desc" id="actual-dose-help">
                Enter the actual dose you used. Use 0 if no insulin was taken.
              </p>
              <div class="meal-confirm-controls">
                <label>Actual dose (units)
                  <span class="meal-dose-input">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      inputmode="decimal"
                      placeholder="0.00"
                      aria-describedby="actual-dose-help"
                      formControlName="confirmedBolus">
                    <span>u</span>
                  </span>
                </label>
                <button type="button" class="meal-details-chip-action" (click)="useSuggestedBolus(state.meal)">Use suggested</button>
                <button type="submit" class="meal-details-urgent-action" [disabled]="confirmBolusForm.invalid">Confirm insulin</button>
              </div>
              @if (confirmMessage) { <span class="pill">{{ confirmMessage }}</span> }
            </form>
          } @else {
            <p class="meal-action-desc">Actual dose recorded for this meal. Suggested bolus remains shown for traceability.</p>
            <div class="meal-confirmed-dose">
              <span>Actual dose</span>
              <strong>{{ state.meal.confirmedBolus | number:'1.0-2' }}<small>u</small></strong>
            </div>
          }
        </section>

        <section class="meal-details-grid">
          <article class="meal-details-panel meal-food-panel">
            <div class="meal-details-panel-head">
              <h2>Food items</h2>
              @if (state.meal.confirmedBolus === null) {
                <button type="button" class="meal-details-quiet-action" (click)="toggleAddFood()">{{ showAddFood ? 'Cancel' : '+ Add food' }}</button>
              }
            </div>
            <table class="table meal-food-table">
              <thead>
                <tr>
                  <th>Food</th>
                  <th>Amount</th>
                  <th>Carb basis</th>
                  <th>Carbs</th>
                  @if (state.meal.confirmedBolus === null && state.meal.items.length > 0) {
                    <th class="meal-item-actions-heading" aria-hidden="true"></th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (item of state.meal.items; track item.id) {
                  <tr class="meal-food-row">
                    <td class="meal-food-name">{{ item.foodNameSnapshot }}</td>
                    <td class="meal-num">
                      @if (editingItemId === item.id) {
                        <form [formGroup]="editWeightForm" class="inline-edit-form" (ngSubmit)="saveItemWeight(state.meal.id, item.id)">
                          <input type="number" [min]="quantityMin(item.measurementType)" [step]="quantityStep(item.measurementType)" formControlName="quantity" [attr.aria-label]="quantityLabel(item.measurementType)">
                          <button type="submit" class="secondary" [disabled]="editWeightForm.invalid">Save</button>
                          <button type="button" class="meal-details-quiet-action" (click)="cancelEditItem()">Cancel</button>
                        </form>
                      } @else {
                        {{ formatQuantity(item.quantity, item.measurementType) }}
                      }
                    </td>
                    <td class="meal-num">{{ carbBasis(item) }}</td>
                    <td class="meal-num">{{ item.calculatedCarbs | number:'1.0-1' }} g</td>
                    @if (state.meal.confirmedBolus === null && state.meal.items.length > 0) {
                      <td class="meal-num meal-item-actions-cell">
                        <div class="meal-item-actions">
                          <button
                            type="button"
                            class="meal-row-action"
                            aria-label="Edit food item"
                            title="Edit food item"
                            (click)="startEditItem(item)">
                            <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                              <path d="M4 20h4.5L19 9.5 14.5 5 4 15.5V20Z"></path>
                              <path d="M13.5 6 18 10.5"></path>
                            </svg>
                          </button>
                          <button
                            type="button"
                            class="meal-row-action remove"
                            aria-label="Remove food item"
                            title="Remove food item"
                            (click)="removeMealItem(state.meal.id, item.id)">
                            <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                              <path d="M6 7h12"></path>
                              <path d="M10 7V5h4v2"></path>
                              <path d="M8 10v8"></path>
                              <path d="M12 10v8"></path>
                              <path d="M16 10v8"></path>
                              <path d="M7 7l1 13h8l1-13"></path>
                            </svg>
                          </button>
                        </div>
                      </td>
                    }
                  </tr>
                }
                <tr class="meal-total-row">
                  <td class="meal-food-name">Total</td>
                  <td></td>
                  <td></td>
                  <td class="meal-num">{{ state.meal.totalCarbs | number:'1.0-1' }} g</td>
                  @if (state.meal.confirmedBolus === null && state.meal.items.length > 0) { <td></td> }
                </tr>
              </tbody>
            </table>
            @if (state.meal.confirmedBolus === null && showAddFood) {
              <form [formGroup]="addFoodItemsForm" class="add-food-form" (ngSubmit)="submitAddFoodItems(state.meal.id)">
                <div formArrayName="items" class="add-food-list">
                  @for (item of addFoodItems.controls; track $index; let i = $index) {
                    <div class="add-food-row" [formGroupName]="i">
                      <label>Food
                        <select formControlName="foodItemId">
                          <option value="">Choose food</option>
                          @for (food of foods; track food.id) {
                            <option [value]="food.id">{{ food.name }} · {{ foodCarbBasis(food) }}</option>
                          }
                        </select>
                      </label>
                      <label>{{ quantityLabel(selectedAddFood(i)) }}
                        <input type="number" [min]="quantityMin(selectedAddFood(i))" [step]="quantityStep(selectedAddFood(i))" formControlName="quantity">
                      </label>
                      <button type="button" class="meal-details-quiet-action" (click)="removeAddFoodRow(i)" [disabled]="addFoodItems.length === 1">Remove</button>
                    </div>
                  }
                </div>
                <div class="actions">
                  <button type="button" class="meal-details-quiet-action" (click)="addFoodRow()">+ Add another</button>
                  <button type="submit" class="secondary" [disabled]="addFoodItemsForm.invalid">Save food</button>
                  @if (addFoodMessage) { <span class="pill">{{ addFoodMessage }}</span> }
                </div>
                @if (addFoodError) { <p class="error">{{ addFoodError }}</p> }
              </form>
            }
            @if (editFoodError) { <p class="error">{{ editFoodError }}</p> }
          </article>
        </section>
      }
    }
  `
})
export class MealDetailsComponent implements OnInit {
  @Input() id = '';
  foods: FoodItem[] = [];
  confirmMessage = '';
  addFoodMessage = '';
  addFoodError = '';
  editFoodError = '';
  editingItemId: string | null = null;
  showAddFood = false;
  private readonly refresh$ = new BehaviorSubject(0);
  private readonly mealId$ = this.route.paramMap.pipe(
    map((params) => params.get('id') ?? this.id),
    distinctUntilChanged()
  );
  state$ = combineLatest([this.mealId$, this.refresh$]).pipe(
    switchMap(([mealId]) => this.api.getMeal(mealId).pipe(
      map((meal): MealDetailsState => ({ kind: 'loaded', meal })),
      startWith({ kind: 'loading' } satisfies MealDetailsState),
      catchError((error) => {
        const apiError = toApiError(error, 'Could not load this meal.');
        return of(apiError.status === 404
          ? { kind: 'not-found' } satisfies MealDetailsState
          : { kind: 'error', error: apiError } satisfies MealDetailsState);
      })
    ))
  );
  confirmBolusForm = this.fb.group({
    confirmedBolus: [null as number | null, [Validators.required, Validators.min(0)]]
  });
  addFoodItemsForm = this.fb.group({
    items: this.fb.array([this.createAddFoodItemGroup()])
  });
  editWeightForm = this.fb.nonNullable.group({
    quantity: [100, [Validators.required, Validators.min(0.1)]]
  });

  constructor(private readonly api: ApiService, private readonly route: ActivatedRoute, private readonly fb: FormBuilder) {}

  ngOnInit() {
    this.api.getFoods().subscribe((foods) => this.foods = foods);
  }

  get addFoodItems() {
    return this.addFoodItemsForm.controls.items as FormArray;
  }

  retryLoad() {
    this.refresh$.next(this.refresh$.value + 1);
  }

  useSuggestedBolus(meal: MealDetail) {
    this.confirmBolusForm.patchValue({ confirmedBolus: meal.suggestedBolus });
  }

  toggleAddFood() {
    this.showAddFood = !this.showAddFood;
    this.addFoodMessage = '';
    this.addFoodError = '';
  }

  addFoodRow() {
    this.addFoodItems.push(this.createAddFoodItemGroup());
  }

  removeAddFoodRow(index: number) {
    if (this.addFoodItems.length === 1) return;
    this.addFoodItems.removeAt(index);
  }

  startEditItem(item: MealDetail['items'][number]) {
    this.editingItemId = item.id;
    this.editFoodError = '';
    this.applyEditQuantityValidators(item.measurementType);
    this.editWeightForm.reset({ quantity: item.quantity });
  }

  cancelEditItem() {
    this.editingItemId = null;
    this.editFoodError = '';
  }

  saveItemWeight(mealId: string, itemId: string) {
    if (this.editWeightForm.invalid) return;

    this.api.updateMealItem(mealId, itemId, this.editWeightForm.getRawValue()).subscribe({
      next: () => {
        this.editingItemId = null;
        this.editFoodError = '';
        this.refresh$.next(this.refresh$.value + 1);
      },
      error: (err) => this.editFoodError = this.getMealActionError(err, 'Could not update food.')
    });
  }

  removeMealItem(mealId: string, itemId: string) {
    if (!confirm('Remove this food item from the meal?')) return;

    this.api.removeMealItem(mealId, itemId).subscribe({
      next: () => {
        this.editingItemId = null;
        this.editFoodError = '';
        this.refresh$.next(this.refresh$.value + 1);
      },
      error: (err) => this.editFoodError = this.getMealActionError(err, 'Could not remove food.')
    });
  }

  confirmBolus(mealId: string) {
    if (this.confirmBolusForm.invalid) return;
    const confirmedBolus = this.confirmBolusForm.getRawValue().confirmedBolus;
    if (confirmedBolus === null) return;

    this.api.confirmMealBolus(mealId, { confirmedBolus }).subscribe(() => {
      this.confirmMessage = 'Confirmed';
      this.refresh$.next(this.refresh$.value + 1);
    });
  }

  submitAddFoodItems(mealId: string) {
    if (this.addFoodItemsForm.invalid) return;

    const items = this.addFoodItemsForm.getRawValue().items
      .map((item) => ({
        foodItemId: item.foodItemId ?? '',
        quantity: item.quantity ?? 0
      }))
      .filter((item) => item.foodItemId.trim().length > 0 && item.quantity > 0);

    this.api.addMealItems(mealId, { items }).subscribe({
      next: () => {
        this.addFoodMessage = 'Food added';
        this.addFoodError = '';
        this.editFoodError = '';
        this.showAddFood = false;
        this.addFoodItems.clear();
        this.addFoodItems.push(this.createAddFoodItemGroup());
        this.refresh$.next(this.refresh$.value + 1);
      },
      error: (err) => {
        this.addFoodMessage = '';
        this.addFoodError = this.getMealActionError(err, 'Could not add food.');
      }
    });
  }


  private getMealActionError(error: unknown, fallback: string) {
    const apiError = toApiError(error, fallback);
    return apiError.status === 404
      ? 'This meal no longer exists.'
      : apiError.message;
  }
  private createAddFoodItemGroup() {
    const group = this.fb.nonNullable.group({
      foodItemId: ['', Validators.required],
      quantity: [100, [Validators.required, Validators.min(0.1)]]
    });
    group.controls.foodItemId.valueChanges.subscribe((foodItemId) => {
      const food = this.foods.find((item) => item.id === foodItemId);
      group.controls.quantity.setValue(food ? this.defaultQuantity(food.measurementType) : 100);
      this.applyAddQuantityValidators(group, food?.measurementType ?? 'Grams');
    });
    return group;
  }

  selectedAddFood(index: number) {
    const foodId = this.addFoodItems.at(index).value.foodItemId;
    return this.foods.find((food) => food.id === foodId);
  }

  formatQuantity(quantity: number, measurementType: FoodMeasurementType) {
    if (measurementType === 'Grams') return `${this.formatNumber(quantity)} g`;
    const unit = measurementType === 'Piece'
      ? quantity === 1 ? 'piece' : 'pieces'
      : quantity === 1 ? 'portion' : 'portions';
    return `${this.formatNumber(quantity)} ${unit}`;
  }

  carbBasis(item: MealDetail['items'][number]) {
    if (item.measurementType === 'Grams') return `${this.formatNumber(item.carbsPer100gSnapshot ?? 0)} g / 100 g`;
    return `${this.formatNumber(item.carbsPerUnitSnapshot ?? 0)} g / ${item.measurementType === 'Piece' ? 'piece' : 'portion'}`;
  }

  foodCarbBasis(food: FoodItem) {
    return food.measurementType === 'Grams'
      ? `${this.formatNumber(food.carbsPer100g ?? 0)} g / 100 g`
      : `${this.formatNumber(food.carbsPerUnit ?? 0)} g / ${food.measurementType === 'Piece' ? 'piece' : 'portion'}`;
  }

  quantityLabel(foodOrType?: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    if (measurementType === 'Portion') return 'Portions';
    if (measurementType === 'Piece') return 'Pieces';
    return 'Weight';
  }

  quantityStep(foodOrType?: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    return measurementType === 'Piece' ? 1 : 0.1;
  }

  quantityMin(foodOrType?: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    return measurementType === 'Piece' ? 1 : 0.1;
  }

  private defaultQuantity(measurementType: FoodMeasurementType) {
    return measurementType === 'Grams' ? 100 : 1;
  }

  private applyEditQuantityValidators(measurementType: FoodMeasurementType) {
    this.editWeightForm.controls.quantity.setValidators(this.quantityValidators(measurementType));
    this.editWeightForm.controls.quantity.updateValueAndValidity({ emitEvent: false });
  }

  private applyAddQuantityValidators(group: ReturnType<MealDetailsComponent['createAddFoodItemGroup']>, measurementType: FoodMeasurementType) {
    group.controls.quantity.setValidators(this.quantityValidators(measurementType));
    group.controls.quantity.updateValueAndValidity({ emitEvent: false });
  }

  private quantityValidators(measurementType: FoodMeasurementType) {
    return measurementType === 'Piece'
      ? [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)]
      : [Validators.required, Validators.min(0.1)];
  }

  private formatNumber(value: number) {
    return new Intl.NumberFormat(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 1 }).format(value);
  }
}
