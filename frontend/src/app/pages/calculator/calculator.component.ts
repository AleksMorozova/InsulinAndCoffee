import { DecimalPipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { assistantFoodName } from '../../core/meal-assistant.models';
import { getApiErrorMessage } from '../../core/api-error';
import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { FoodItem, FoodMeasurementType, MealCalculation, MealCarbPreview, MealDetail, MealItemInput, mealTypes } from '../../core/models';
import { CarbProvenanceComponent } from '../../shared/carb-provenance.component';
import { DisclaimerComponent } from '../../shared/disclaimer.component';

@Component({
  selector: 'app-calculator',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, DisclaimerComponent, CarbProvenanceComponent],
  template: `
    <section class="page-title">
      <div>
        <h1>Current Meal</h1>
        <p>Build your meal, review the suggestion, then confirm the actual dose.</p>
      </div>
    </section>
    <app-disclaimer />
    @if (foodLoadError) { <p role="alert">{{ foodLoadError }}</p><button type="button" (click)="loadFoods()">Retry food search</button> }

    <form [formGroup]="form" class="calculator-layout" (ngSubmit)="save()">
      <section class="meal-builder">
        <div class="builder-card">
          <div class="builder-section-head">
            <div>
              <p><span class="step-badge">①</span> Meal Information</p>
              <h2>Start with the basics</h2>
            </div>
          </div>
          <div class="grid two">
            <label>Meal type
              <select formControlName="mealType">
                @for (type of mealTypes; track type) { <option [value]="type">{{ type }}</option> }
              </select>
            </label>
            <label>Current glucose
              <input type="number" min="0.1" step="0.1" formControlName="preMealGlucose" (input)="recordEnteredGlucose()">
              @if (glucoseIsDefault) { <small>{{ glucoseDefaultHint }}</small> }
            </label>
          </div>
        </div>

        @if (directMode) {
          <div class="builder-card direct-meal-card">
            <div class="builder-section-head">
              <div>
                <p><span class="step-badge">②</span> Build your meal</p>
                <h2>{{ directFoodName }}</h2>
              </div>
              <button type="button" class="subtle" (click)="clearDirectMeal()">Use food items</button>
            </div>
            <div class="direct-meal-metrics">
              <div>
                <strong>{{ directCarbs | number:'1.0-1' }}g</strong>
                <span>Recorded carbs</span>
                <app-carb-provenance [provenance]="calculation?.items?.[0]?.provenance" />
              </div>
              <div>
                <strong>
                  @if (form.controls.confirmedBolus.value !== null) {
                    {{ form.controls.confirmedBolus.value | number:'1.0-2' }}U
                  } @else {
                    Not confirmed
                  }
                </strong>
                <span>Recorded dose</span>
              </div>
            </div>
          </div>
        } @else {
          <div class="builder-card">
            <div class="builder-section-head build-meal-header">
              <div>
                <p><span class="step-badge">②</span> Build your meal</p>
              </div>
              <button type="button" class="create-food-button" (click)="openNewFood()">+ Create food</button>
            </div>
            @if (showCreateFood) {
              <div class="create-food-panel" [formGroup]="newFoodForm">
                <div class="builder-section-head">
                  <div>
                    <p>New Food</p>
                    <h2>Add it to your library</h2>
                  </div>
                </div>
                <div class="grid">
                  <label>Name <input formControlName="name"></label>
                  <div class="measurement-field">
                    <span class="form-label">Calculate by</span>
                    <div class="segmented-control" aria-label="Calculate food by">
                      @for (type of measurementTypes; track type) {
                        <button type="button" [class.active]="newFoodForm.controls.measurementType.value === type" (click)="setNewFoodMeasurementType(type)">
                          {{ type }}
                        </button>
                      }
                    </div>
                  </div>
                  <div class="grid two">
                    @if (newFoodForm.controls.measurementType.value === 'Grams') {
                      <label>Carbs per 100 g <input type="number" min="0" step="0.1" formControlName="carbsPer100g"></label>
                    } @else {
                      <label>Carbs per {{ unitSingular(newFoodForm.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="carbsPerUnit"></label>
                    }
                    <label>Protein {{ nutritionBasisLabel(newFoodForm.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="proteinPer100g"></label>
                    <label>Fat {{ nutritionBasisLabel(newFoodForm.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="fatPer100g"></label>
                    <label>Calories {{ nutritionBasisLabel(newFoodForm.controls.measurementType.value) }} <input type="number" min="0" step="1" formControlName="caloriesPer100g"></label>
                  </div>
                  <div class="grid two">
                    <label>{{ quantityLabel(newFoodForm.controls.measurementType.value) }} for Save and Add
                      <input type="number" [min]="quantityMin(newFoodForm.controls.measurementType.value)" [step]="quantityStep(newFoodForm.controls.measurementType.value)" formControlName="quantity">
                    </label>
                    <label class="toolbar">
                      <input style="width:auto" type="checkbox" formControlName="isFavorite">
                      Favorite
                    </label>
                  </div>
                  <div class="actions">
                    <button type="button" [disabled]="newFoodForm.invalid" (click)="saveNewFood(false)">Save</button>
                    <button type="button" class="secondary" [disabled]="newFoodForm.invalid" (click)="saveNewFood(true)">Save and Add</button>
                    <button type="button" class="subtle" (click)="showCreateFood = false">Cancel</button>
                  </div>
                </div>
              </div>
            }
            <div formArrayName="items" class="current-meal-list">
              <div class="meal-item-grid-head" aria-hidden="true">
                <span>Food</span>
                <span>Amount</span>
                <span>Carbs</span>
                <span>Override</span>
                <span>Action</span>
              </div>
              @for (item of items.controls; track $index; let i = $index) {
                <article class="meal-item-card" [formGroupName]="i" [class.has-carb-override]="hasItemOverride(i)">
                  <div class="meal-item-main">
                    <input
                      [id]="'meal-food-' + i"
                      type="text"
                      role="combobox"
                      aria-label="Food"
                      aria-autocomplete="list"
                      [attr.aria-controls]="'food-options-' + i"
                      [attr.list]="'food-options-' + i"
                      [value]="foodQuery(i)"
                      (input)="onFoodQueryChange(i, $any($event.target).value)"
                      placeholder="Search or choose a food…"
                      autocomplete="off">
                    <datalist [id]="'food-options-' + i">
                      @for (food of foods; track food.id) {
                        <option [value]="food.name">{{ foodCarbBasis(food) }}</option>
                      }
                    </datalist>
                    @if (assistantDraft && !item.value.foodItemId) { <small>Unresolved · choose a saved food or remove this row.</small> }
                  </div>
                  <div class="meal-weight">
                    <input type="number" [min]="quantityMin(selectedMeasurementType(i))" [step]="quantityStep(selectedMeasurementType(i))" formControlName="quantity" [attr.aria-label]="quantityLabel(selectedMeasurementType(i))">
                    <span>{{ quantitySuffix(selectedMeasurementType(i), itemQuantity(i)) }}</span>
                  </div>
                  <div class="meal-item-carbs" aria-label="Calculated carbs">
                    <strong>@if (itemCalculatedCarbs(i); as carbs) { {{ carbs | number:'1.0-2' }} g } @else if (itemCalculatedCarbs(i) === 0) { 0 g } @else { — }</strong>
                    @if (itemCalculatedCarbs(i) !== null) { <app-carb-provenance [provenance]="(calculation ?? carbPreview)?.items?.[i]?.provenance" [overridden]="hasItemOverride(i)" /> }
                  </div>
                  <div class="item-carb-override">
                    <div class="override-input-wrap">
                      <input
                        [id]="'item-carb-override-' + i"
                        type="number"
                        min="0"
                        step="0.1"
                        formControlName="carbOverride"
                        placeholder="Auto"
                        aria-label="Override carbs"
                        [class.active]="hasItemOverride(i)">
                      <span>g</span>
                    </div>
                    @if (hasItemOverride(i)) {
                      <button type="button" class="reset-carb-button" aria-label="Reset to calculated carbs" title="Reset to calculated carbs" (click)="resetItemOverride(i)">↺</button>
                    }
                  </div>
                  <button
                    type="button"
                    class="meal-remove"
                    aria-label="Remove food from meal"
                    (click)="removeItem(i)"
                    [disabled]="items.length === 1">×</button>
                </article>
              }
            </div>
            <button type="button" class="subtle add-another-food" (click)="addItem()">+ Add food to this meal</button>
          </div>
        }
      </section>

      <aside class="calculation-card">
        <div class="builder-section-head">
          <div>
            <p><span class="step-badge">③</span> Calculation Summary</p>
            <h2>Review the result</h2>
          </div>
        </div>
        @if (historicalRepeat && hasCarbAdjustment()) {
          <p class="calculation-note" role="note">
            This repeat includes a meal-level carbohydrate adjustment ({{ signedCarbAdjustment() }} g).
            If you change the foods or portions, review and edit Adjust carbs below.
          </p>
        }
        @if (calculation) {
          <div class="calculation-state calculation-results">
            <div class="calculation-hero-values">
              <div>
                <strong>{{ calculation.totalCarbs | number:'1.0-1' }} g</strong>
                <span>Final carbs</span>
              </div>
              <div>
                <strong>{{ calculation.suggestedBolus | number:'1.0-2' }} U</strong>
                <span>Suggested insulin</span>
              </div>
            </div>
            <div class="calculation-breakdown">
              <span>Meal bolus <strong>{{ calculation.mealBolus | number:'1.0-2' }} U</strong></span>
              <span>Correction <strong>{{ calculation.correctionBolus | number:'1.0-2' }} U</strong></span>
            </div>
            <p class="calculation-note"><span class="calculation-note-icon" aria-hidden="true">i</span>This is only a suggestion. You know your body best.</p>
          </div>
        } @else if (assistantDraft) {
          <div class="calculation-state calculation-results">
            @if (carbPreview) {
              <strong>{{ carbPreview.totalCarbs | number:'1.0-2' }} g</strong>
              <span> Estimated carbohydrates</span>
            }
            @if (form.controls.preMealGlucose.invalid) {
              <p>Enter current glucose to calculate insulin.</p>
            }

          </div>
        } @else {
          <div class="calculation-state calculation-empty">
            <strong>Choose your first food.</strong>
            <p>We'll automatically calculate:</p>
            <ul>
              <li>Estimated carbs</li>
              <li>Suggested insulin</li>
              <li>Meal bolus</li>
              <li>Correction dose</li>
            </ul>
            <span class="calculation-empty-direction">Start by adding a food <span aria-hidden="true">→</span></span>
          </div>
        }

        @if (calculation || historicalRepeat) {
            <div class="carb-adjustment-panel">
              <div class="carb-summary-lines">
                @if (calculation) { <span><span>Foods</span><strong>{{ calculation.foodCarbs | number:'1.0-1' }} g</strong></span> }
                @if (hasCarbAdjustment()) {
                  <span><span>Manual adjustment</span><strong>{{ signedCarbAdjustment() }} g</strong></span>
                }
              </div>
              <div class="meal-carb-adjustment-control">
                <span>Adjust carbs</span>
                <div class="adjustment-control-row">
                  <button type="button" class="adjustment-step-button" aria-label="Subtract 1 gram" (click)="adjustCarbAdjustment(-1)">−</button>
                  <span class="adjustment-input-wrap">
                    <input type="text" inputmode="decimal" [value]="adjustmentInputValue" (input)="setCarbAdjustmentFromInput($event)" (blur)="syncCarbAdjustmentInput()" aria-label="Manual carb adjustment delta">
                    <span>g</span>
                  </span>
                  <button type="button" class="adjustment-step-button" aria-label="Add 1 gram" (click)="adjustCarbAdjustment(1)">+</button>
                </div>
              </div>
            </div>
        }
        @if (error) { <p role="alert">{{ error }}</p> }
        <div class="confirmation-fields">
          <div class="builder-section-head compact">
            <div>
              <p><span class="step-badge">④</span> Confirm</p>
              <h2>Confirm the actual dose</h2>
            </div>
          </div>
          <label>Confirmed dose <span class="muted">(optional)</span>
            <input type="number" min="0" step="0.1" formControlName="confirmedBolus">
          </label>
          <label>Notes
            <textarea rows="4" formControlName="notes" placeholder="Anything worth remembering?"></textarea>
          </label>
        </div>

        <div class="save-actions">
          @if (glucoseIsDefault) { <small>{{ glucoseSaveHint }}</small> }
          <button type="submit" class="save-meal-button" [disabled]="glucoseIsDefault || form.invalid || !calculation">Save Meal</button>
          <button type="button" class="outline-button" [disabled]="glucoseIsDefault || form.invalid || !calculation" (click)="saveAndAddAnother()">Save & Add Another</button>
        </div>
      </aside>
    </form>
  `
})
export class CalculatorComponent implements OnInit {
  mealTypes = mealTypes;
  measurementTypes: FoodMeasurementType[] = ['Grams', 'Portion', 'Piece'];
  foods: FoodItem[] = [];
  foodLoadError = '';
  calculation?: MealCalculation;
  carbPreview?: MealCarbPreview;
  assistantDraft = false;
  historicalRepeat = false;
  glucoseIsDefault = false;
  assistantLocale: 'en' | 'ru' | 'uk' = 'en';
  private calculationRequest?: Subscription;

  ngOnDestroy() {
    this.calculationRequest?.unsubscribe();
  }
  error = '';
  directMode = false;
  directCarbs?: number;
  directFoodName = '';
  private sourceDeliveryMealId?: string;
  showCreateFood = false;
  newFoodTargetIndex = 0;
  foodQueries: string[] = [''];
  adjustmentInputValue = '0';
  private lastCalculationKey = '';

  newFoodForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    measurementType: ['Grams' as FoodMeasurementType, Validators.required],
    carbsPer100g: [0, [Validators.required, Validators.min(0)]],
    carbsPerUnit: [0, [Validators.min(0)]],
    proteinPer100g: [0, [Validators.min(0)]],
    fatPer100g: [0, [Validators.min(0)]],
    caloriesPer100g: [0, [Validators.min(0)]],
    isFavorite: [false],
    quantity: [100, [Validators.required, Validators.min(0.1)]]
  });

  form = this.fb.group({
    mealType: this.fb.nonNullable.control('Breakfast', Validators.required),
    preMealGlucose: this.fb.control<number | null>(6.5, [Validators.required, Validators.min(0.1)]),
    confirmedBolus: this.fb.control<number | null>(null, [Validators.min(0)]),
    notes: this.fb.control(''),
    carbAdjustment: this.fb.nonNullable.control(0),
    items: this.fb.array([
      this.fb.group({
        foodItemId: this.fb.nonNullable.control('', Validators.required),
        quantity: this.fb.nonNullable.control(100, [Validators.required, Validators.min(0.1)]),
        measurementType: this.fb.control<FoodMeasurementType | null>(null),
        foodNameSnapshot: this.fb.control<string | null>(null),
        carbsPer100gSnapshot: this.fb.control<number | null>(null),
        carbsPerUnitSnapshot: this.fb.control<number | null>(null),
        carbOverride: this.fb.control<number | null>(null, [Validators.min(0)]),
        sourceMealItemId: this.fb.control<string | null>(null), sourceDeliveryMealId: this.fb.control<string | null>(null)
      })
    ])
  });

  get items() {
    return this.form.controls.items as FormArray;
  }

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService, private readonly router: Router, private readonly auth?: AuthService) {}

  ngOnInit() {
    this.loadFoods();
    this.form.valueChanges.subscribe(() => this.calculate());
    const previousMeal: MealDetail | undefined = history.state?.meal;
    const deliveryMeal = history.state?.deliveryMeal;
    if (deliveryMeal) {
      this.directMode = true;
      this.directCarbs = deliveryMeal.carbs;
      this.sourceDeliveryMealId = deliveryMeal.id;
      this.directFoodName = `${deliveryMeal.placeName} - ${deliveryMeal.dishName}`;
      this.items.clear({ emitEvent: false });
      this.form.patchValue({
        confirmedBolus: null,
        notes: deliveryMeal.notes ?? ''
      }, { emitEvent: false });
      this.initializeRepeatGlucose();
    }
    const assistantItems: MealItemInput[] | undefined = history.state?.assistantMealItems
      ?? previousMeal?.items.map(item => ({ ...item, sourceMealItemId: item.id }));
    if (assistantItems?.length) {
      this.assistantDraft = true;
      this.initializeRepeatGlucose();
      this.form.controls.confirmedBolus.setValue(null, { emitEvent: false });
      this.form.controls.carbAdjustment.setValue(
        previousMeal?.carbAdjustment ?? history.state?.assistantCarbAdjustment ?? 0, { emitEvent: false });
      this.historicalRepeat = !!previousMeal || !!history.state?.assistantOriginalMealId;
      this.items.clear({ emitEvent: false });
      for (const item of assistantItems) {
        this.items.push(this.fb.group({
          foodItemId: this.fb.nonNullable.control(item.foodItemId, Validators.required),
          quantity: this.fb.nonNullable.control(item.quantity ?? item.weightGrams ?? 0, [Validators.required, Validators.min(0.1)]),
          measurementType: this.fb.control<FoodMeasurementType | null>(item.measurementType ?? null),
          foodNameSnapshot: this.fb.control<string | null>(item.foodNameSnapshot ?? null),
          carbsPer100gSnapshot: this.fb.control<number | null>(item.carbsPer100gSnapshot ?? null),
          carbsPerUnitSnapshot: this.fb.control<number | null>(item.carbsPerUnitSnapshot ?? null),
          carbOverride: this.fb.control<number | null>(item.carbOverride ?? null, [Validators.min(0)]),
          sourceMealItemId: this.fb.control<string | null>(item.sourceMealItemId ?? null), sourceDeliveryMealId: this.fb.control<string | null>(item.sourceDeliveryMealId ?? null)
        }), { emitEvent: false });
        this.applyItemQuantityValidators(this.items.length - 1, item.measurementType ?? 'Grams');
      }
      this.syncFoodQueries();
    }
    if (previousMeal?.items?.length) {
      this.form.patchValue({
        mealType: previousMeal.mealType,
        notes: previousMeal.notes ?? ''
      }, { emitEvent: false });
    }
    this.syncCarbAdjustmentInput();
    this.calculate();
  }

  addItem() {
    this.directMode = false;
    this.directCarbs = undefined;
    this.sourceDeliveryMealId = undefined;
    this.directFoodName = '';
    this.items.push(this.fb.group({
      foodItemId: this.fb.nonNullable.control('', Validators.required),
      quantity: this.fb.nonNullable.control(100, [Validators.required, Validators.min(0.1)]),
      measurementType: this.fb.control<FoodMeasurementType | null>(null),
      foodNameSnapshot: this.fb.control<string | null>(null),
      carbsPer100gSnapshot: this.fb.control<number | null>(null),
      carbsPerUnitSnapshot: this.fb.control<number | null>(null),
      carbOverride: this.fb.control<number | null>(null, [Validators.min(0)]),
      sourceMealItemId: this.fb.control<string | null>(null), sourceDeliveryMealId: this.fb.control<string | null>(null)
    }));
    this.foodQueries.push('');
  }

  clearDirectMeal() {
    this.sourceDeliveryMealId = undefined;
    this.directMode = false;
    this.directCarbs = undefined;
    this.directFoodName = '';
    if (this.items.length === 0) {
      this.addItem();
    }
    this.syncCarbAdjustmentInput();
    this.calculate();
  }

  removeItem(index: number) {
    this.items.removeAt(index);
    this.foodQueries.splice(index, 1);
    this.syncCarbAdjustmentInput();
    this.calculate();
  }

  itemCalculatedCarbs(index: number) {
    return (this.calculation ?? this.carbPreview)?.items[index]?.calculatedCarbs ?? null;
  }

  itemEffectiveCarbs(index: number) {
    return (this.calculation ?? this.carbPreview)?.items[index]?.effectiveCarbs ?? this.itemCalculatedCarbs(index);
  }

  hasItemOverride(index: number) {
    const value = this.items.at(index).value.carbOverride;
    return value !== null && value !== undefined;
  }

  resetItemOverride(index: number) {
    this.items.at(index).patchValue({ carbOverride: null });
  }

  focusItemOverride(index: number) {
    window.setTimeout(() => document.getElementById(`item-carb-override-${index}`)?.focus(), 0);
  }

  hasCarbAdjustment() {
    return (this.form.controls.carbAdjustment.value ?? 0) !== 0;
  }

  signedCarbAdjustment() {
    const value = this.form.controls.carbAdjustment.value ?? 0;
    if (value > 0) return `+${this.formatAdjustment(value)}`;
    if (value < 0) return `-${this.formatAdjustment(value)}`;
    return '0';
  }


  adjustCarbAdjustment(delta: number) {
    const current = this.form.controls.carbAdjustment.value ?? 0;
    this.form.controls.carbAdjustment.setValue(Math.round((current + delta) * 10) / 10);
    this.syncCarbAdjustmentInput();
  }

  setCarbAdjustmentFromInput(event: Event) {
    const input = event.target as HTMLInputElement;
    const rawValue = input.value.trim();
    this.adjustmentInputValue = rawValue;

    if (!rawValue || rawValue === '+' || rawValue === '-' || rawValue === '.' || rawValue === '+.' || rawValue === '-.') {
      return;
    }

    const parsed = Number(rawValue.replace(',', '.').replace(/^\+/, ''));
    if (Number.isFinite(parsed)) {
      this.form.controls.carbAdjustment.setValue(Math.round(parsed * 10) / 10);
      if (!/[.,]$/.test(rawValue)) {
        this.syncCarbAdjustmentInput();
      }
    }
  }

  syncCarbAdjustmentInput() {
    this.adjustmentInputValue = this.signedCarbAdjustment();
  }

  private formatAdjustment(value: number) {
    return `${Math.abs(value)}`;
  }

  itemQuantity(index: number) {
    return this.items.at(index).value.quantity ?? null;
  }

  itemName(index: number, fallback = 'Choose a food to add it here') {
    const item = this.items.at(index).value as { foodItemId: string; foodNameSnapshot?: string | null };
    const name = (this.assistantDraft ? item.foodNameSnapshot : null)
      ?? this.foods.find(food => food.id === item.foodItemId)?.name ?? item.foodNameSnapshot ?? fallback;
    return this.assistantDraft ? assistantFoodName(name) : name;
  }

  foodQuery(index: number) {
    return this.foodQueries[index] ?? this.itemName(index, '');
  }

  onFoodQueryChange(index: number, query: string) {
    this.foodQueries[index] = query;
    const food = this.foods.find((item) => item.name.localeCompare(query.trim(), undefined, { sensitivity: 'accent' }) === 0);
    this.items.at(index).patchValue({
      foodItemId: food?.id ?? '',
      quantity: food ? this.defaultQuantity(food) : this.items.at(index).value.quantity,
      measurementType: null,
      foodNameSnapshot: null,
      carbsPer100gSnapshot: null,
      carbsPerUnitSnapshot: null,
      carbOverride: null,
      sourceMealItemId: null, sourceDeliveryMealId: null
    });
    this.applyItemQuantityValidators(index, food?.measurementType ?? 'Grams');
  }

  openNewFood() {
    const emptyIndex = this.items.controls.findIndex((control) => !control.value.foodItemId);
    this.newFoodTargetIndex = emptyIndex >= 0 ? emptyIndex : this.items.length;
    this.showCreateFood = true;
  }

  saveNewFood(addToMeal: boolean) {
    if (this.newFoodForm.invalid) return;
    const value = this.newFoodForm.getRawValue();
    this.api.createFood({
      name: value.name,
      measurementType: value.measurementType,
      carbsPer100g: value.measurementType === 'Grams' ? value.carbsPer100g : null,
      carbsPerUnit: value.measurementType === 'Grams' ? null : value.carbsPerUnit,
      proteinPer100g: value.proteinPer100g ?? 0,
      fatPer100g: value.fatPer100g ?? 0,
      caloriesPer100g: value.caloriesPer100g ?? 0,
      isFavorite: value.isFavorite
    }).subscribe({
      next: (food) => {
        this.foods = [food, ...this.foods.filter((item) => item.id !== food.id)].sort((a, b) => a.name.localeCompare(b.name));
        if (addToMeal) {
          this.addFoodToMeal(food.id, value.quantity, this.newFoodTargetIndex);
        }
        this.showCreateFood = false;
        this.resetNewFoodForm();
      },
      error: (err) => this.error = getApiErrorMessage(err, 'Could not create food.')
    });
  }

  private addFoodToMeal(foodItemId: string, quantity: number, targetIndex: number) {
    this.directMode = false;
    if (this.items.length === 0) {
      this.addItem();
    }

    if (targetIndex >= this.items.length) {
      this.addItem();
    }

    const effectiveIndex = Math.min(targetIndex, this.items.length - 1);
    const target = this.items.at(effectiveIndex);
    target.patchValue({ foodItemId, quantity, carbOverride: null });
    this.applyItemQuantityValidators(effectiveIndex, this.foods.find((food) => food.id === foodItemId)?.measurementType ?? 'Grams');
    this.foodQueries[effectiveIndex] = this.foods.find((food) => food.id === foodItemId)?.name ?? '';
    this.syncCarbAdjustmentInput();
    this.calculate();
  }

  private initializeRepeatGlucose() {
    const stored = this.glucoseSessionKey ? Number(sessionStorage.getItem(this.glucoseSessionKey)) : null;
    const supplied: unknown = history.state?.assistantCurrentGlucose;
    const value = this.validGlucose(stored) ? stored : this.validGlucose(supplied) ? supplied : null;
    this.glucoseIsDefault = value === null;
    this.assistantLocale = history.state?.assistantLocale === 'ru' ? 'ru' : history.state?.assistantLocale === 'uk' ? 'uk' : 'en';
    this.form.controls.preMealGlucose.setValue(value ?? 5.5, { emitEvent: false });
  }

  private get glucoseSessionKey() {
    const id = this.auth?.user()?.id;
    return id ? 'insulin-and-coffee.calculator-glucose.' + id : null;
  }

  private validGlucose(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value) && value >= 0.1;
  }

  recordEnteredGlucose() {
    const glucose = this.form.controls.preMealGlucose.value;
    const key = this.glucoseSessionKey;
    if (key) {
      if (this.validGlucose(glucose)) sessionStorage.setItem(key, String(glucose));
      else sessionStorage.removeItem(key);
    }
    if (!this.validGlucose(glucose)) return;
    this.glucoseIsDefault = false;
    this.calculate();
  }

  get glucoseDefaultHint() {
    return { en: 'Glucose not confirmed. Calculations use the default value of 5.5 mmol/L.', ru: 'Глюкоза не подтверждена. Расчёт выполнен со значением 5.5 ммоль/л по умолчанию.',
      uk: 'Глюкоза не підтверджена. Розрахунок використовує значення 5.5 ммоль/л за замовчуванням.' }[this.assistantLocale];
  }

  get glucoseSaveHint() {
    return { en: 'Enter your current glucose to save the meal.', ru: 'Введите текущую глюкозу, чтобы сохранить приём пищи.',
      uk: 'Введіть поточну глюкозу, щоб зберегти прийом їжі.' }[this.assistantLocale];
  }

  calculate() {
    const validItems = this.mealInputs()
      .filter((item) => item.foodItemId.trim().length > 0 && item.quantity > 0);

    if (!this.directMode && (validItems.length === 0 || (this.assistantDraft && this.items.invalid))) {
      this.calculationRequest?.unsubscribe();
      this.carbPreview = undefined;
      if (this.assistantDraft && this.items.invalid) this.error = 'Review unresolved foods and invalid quantities before continuing.';
      this.calculation = undefined;
      return;
    }

    const glucose = this.form.controls.preMealGlucose;
    if (glucose.invalid || glucose.value === null) {
      this.calculationRequest?.unsubscribe();
      this.calculation = undefined;
      if (this.assistantDraft && !this.directMode) {
        this.carbPreview = undefined;
        this.calculationRequest = this.api.previewMealCarbs(validItems, this.form.controls.carbAdjustment.value ?? 0).subscribe({
          next: preview => { this.carbPreview = preview; this.error = ''; },
          error: error => this.error = getApiErrorMessage(error, 'Could not calculate carbs. Review the transferred foods.')
        });
      }
      return;
    }

    const request = {
      mealType: this.form.controls.mealType.value as any,
      preMealGlucose: glucose.value,
      items: this.directMode ? [] : validItems,
      directCarbs: this.directMode ? this.directCarbs : undefined,
      directFoodName: this.directMode ? this.directFoodName : undefined,
      ...(this.directMode && this.sourceDeliveryMealId ? { sourceDeliveryMealId: this.sourceDeliveryMealId } : {}),
      carbAdjustment: this.form.controls.carbAdjustment.value ?? 0
    };

    const calculationKey = JSON.stringify(request);
    if (this.calculation && calculationKey === this.lastCalculationKey) {
      return;
    }

    this.calculationRequest?.unsubscribe();
    this.calculation = undefined;
    this.calculationRequest = this.api.calculateMeal(request).subscribe({
      next: (result) => {
        this.calculation = result;
        this.carbPreview = undefined;
        this.lastCalculationKey = calculationKey;
        this.error = '';
      },
      error: error => {
        this.calculation = undefined;
        this.error = getApiErrorMessage(error, 'Could not calculate this meal. Review the food carbohydrate values.');
        this.lastCalculationKey = '';
      }
    });
  }

  save() {
    if (this.glucoseIsDefault || this.form.invalid || !this.calculation || this.form.controls.preMealGlucose.value === null) return;
    this.api.createMeal(this.createMealRequest(this.form.controls.preMealGlucose.value)).subscribe({
      next: (meal) => this.router.navigate(['/meals', meal.id]),
      error: (err) => this.error = getApiErrorMessage(err, 'Could not save meal.')
    });
  }

  saveAndAddAnother() {
    if (this.glucoseIsDefault || this.form.invalid || !this.calculation || this.form.controls.preMealGlucose.value === null) return;
    this.api.createMeal(this.createMealRequest(this.form.controls.preMealGlucose.value)).subscribe({
      next: () => this.resetMealBuilder(),
      error: (err) => this.error = getApiErrorMessage(err, 'Could not save meal.')
    });
  }

  private createMealRequest(preMealGlucose: number) {
    const value = this.form.getRawValue();
    return {
      mealType: value.mealType as any,
      preMealGlucose,
      confirmedBolus: value.confirmedBolus ?? null,
      notes: value.notes ?? '',
      items: this.directMode ? [] : this.mealInputs(),
      directCarbs: this.directMode ? this.directCarbs : undefined,
      directFoodName: this.directMode ? this.directFoodName : undefined,
      ...(this.directMode && this.sourceDeliveryMealId ? { sourceDeliveryMealId: this.sourceDeliveryMealId } : {}),
      carbAdjustment: this.form.controls.carbAdjustment.value ?? 0
    };
  }

  private resetMealBuilder() {
    this.sourceDeliveryMealId = undefined;
    this.assistantDraft = false;
    this.historicalRepeat = false;
    this.glucoseIsDefault = false;
    this.carbPreview = undefined;
    this.directMode = false;
    this.directCarbs = undefined;
    this.directFoodName = '';
    this.showCreateFood = false;
    this.newFoodTargetIndex = 0;
    this.foodQueries = [''];
    this.items.clear();
    this.items.push(this.fb.group({
      foodItemId: this.fb.nonNullable.control('', Validators.required),
      quantity: this.fb.nonNullable.control(100, [Validators.required, Validators.min(0.1)]),
      measurementType: this.fb.control<FoodMeasurementType | null>(null),
      foodNameSnapshot: this.fb.control<string | null>(null),
      carbsPer100gSnapshot: this.fb.control<number | null>(null),
      carbsPerUnitSnapshot: this.fb.control<number | null>(null),
      carbOverride: this.fb.control<number | null>(null, [Validators.min(0)]),
      sourceMealItemId: this.fb.control<string | null>(null), sourceDeliveryMealId: this.fb.control<string | null>(null)
    }));
    this.form.reset({
      mealType: this.form.controls.mealType.value,
      preMealGlucose: this.form.controls.preMealGlucose.value,
      confirmedBolus: null,
      notes: '',
      carbAdjustment: 0,
      items: [{ foodItemId: '', quantity: 100, measurementType: null, foodNameSnapshot: null, carbsPer100gSnapshot: null, carbsPerUnitSnapshot: null, carbOverride: null }]
    });
    this.form.controls.confirmedBolus.markAsPristine();
    this.calculation = undefined;
    this.lastCalculationKey = '';
    this.error = '';
    this.syncCarbAdjustmentInput();
  }

  private syncFoodQueries() {
    this.foodQueries = this.items.controls.map((_, index) => this.itemName(index, ''));
  }

  loadFoods() {
    this.foodLoadError = '';
    this.api.getFoods().subscribe({
      next: foods => { this.foods = foods; this.syncFoodQueries(); },
      error: error => this.foodLoadError = getApiErrorMessage(error, 'Could not load foods. Please try again.')
    });
  }

  private mealInputs(): MealItemInput[] {
    return this.form.getRawValue().items.map(item => {
      if (this.assistantDraft && !this.historicalRepeat && !item.sourceMealItemId && !item.sourceDeliveryMealId)
        return { foodItemId: item.foodItemId, quantity: item.quantity, carbOverride: item.carbOverride };
      return { ...item, ...(item.sourceMealItemId ? { clearCarbOverride: item.carbOverride === null } : {}) };
    });
  }

  selectedFood(index: number) {
    const item = this.items.at(index).value as { foodItemId: string };
    return this.foods.find((food) => food.id === item.foodItemId);
  }

  selectedMeasurementType(index: number) {
    const item = this.items.at(index).value as { measurementType?: FoodMeasurementType | null };
    return item.measurementType ?? this.selectedFood(index)?.measurementType ?? 'Grams';
  }

  foodCarbBasis(food: FoodItem) {
    return food.measurementType === 'Grams'
      ? `${food.carbsPer100g ?? 0}g carbs / 100 g`
      : `${food.carbsPerUnit ?? 0}g carbs / ${this.unitSingular(food.measurementType)}`;
  }

  quantityLabel(foodOrType?: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    if (measurementType === 'Portion') return 'Portions';
    if (measurementType === 'Piece') return 'Pieces';
    return 'Weight';
  }

  quantitySuffix(foodOrType?: FoodItem | FoodMeasurementType, quantity?: number | null) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    if (measurementType === 'Portion') return quantity === 1 ? 'portion' : 'portions';
    if (measurementType === 'Piece') return quantity === 1 ? 'piece' : 'pieces';
    return 'g';
  }

  unitSingular(measurementType: FoodMeasurementType) {
    return measurementType === 'Piece' ? 'piece' : 'portion';
  }

  nutritionBasisLabel(measurementType: FoodMeasurementType) {
    if (measurementType === 'Grams') return 'per 100 g';
    return `per ${this.unitSingular(measurementType)}`;
  }

  quantityStep(foodOrType?: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType?.measurementType;
    return measurementType === 'Piece' ? 1 : 0.1;
  }

  quantityMin(foodOrType?: FoodItem | FoodMeasurementType) {
    return 0.1;
  }

  setNewFoodMeasurementType(measurementType: FoodMeasurementType) {
    this.newFoodForm.controls.measurementType.setValue(measurementType);
    this.applyNewFoodMeasurementValidators(measurementType);
    this.newFoodForm.controls.quantity.setValue(this.defaultQuantity(measurementType));
  }

  private defaultQuantity(foodOrType: FoodItem | FoodMeasurementType) {
    const measurementType = typeof foodOrType === 'string' ? foodOrType : foodOrType.measurementType;
    return measurementType === 'Grams' ? 100 : 1;
  }

  private resetNewFoodForm() {
    this.newFoodForm.reset({ name: '', measurementType: 'Grams', carbsPer100g: 0, carbsPerUnit: 0, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false, quantity: 100 });
    this.applyNewFoodMeasurementValidators('Grams');
  }

  private applyNewFoodMeasurementValidators(measurementType: FoodMeasurementType) {
    if (measurementType === 'Grams') {
      this.newFoodForm.controls.carbsPer100g.setValidators([Validators.required, Validators.min(0)]);
      this.newFoodForm.controls.carbsPerUnit.setValidators([Validators.min(0)]);
    } else {
      this.newFoodForm.controls.carbsPer100g.setValidators([Validators.min(0)]);
      this.newFoodForm.controls.carbsPerUnit.setValidators([Validators.required, Validators.min(0)]);
    }

    const quantityValidators = measurementType === 'Piece'
      ? [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)]
      : [Validators.required, Validators.min(0.1)];
    this.newFoodForm.controls.quantity.setValidators(quantityValidators);

    this.newFoodForm.controls.carbsPer100g.updateValueAndValidity({ emitEvent: false });
    this.newFoodForm.controls.carbsPerUnit.updateValueAndValidity({ emitEvent: false });
    this.newFoodForm.controls.quantity.updateValueAndValidity({ emitEvent: false });
  }

  private applyItemQuantityValidators(index: number, measurementType: FoodMeasurementType) {
    const quantityControl = this.items.at(index).get('quantity');
    const validators = measurementType === 'Piece'
      ? [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)]
      : [Validators.required, Validators.min(0.1)];

    quantityControl?.setValidators(validators);
    quantityControl?.updateValueAndValidity({ emitEvent: false });
  }
}



















