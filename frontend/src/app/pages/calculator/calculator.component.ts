import { DecimalPipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { FoodItem, MealCalculation, mealTypes } from '../../core/models';
import { DisclaimerComponent } from '../../shared/disclaimer.component';

@Component({
  selector: 'app-calculator',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, DisclaimerComponent],
  template: `
    <section class="page-title">
      <div>
        <h1>Current Meal</h1>
        <p>Build your meal, review the suggestion, then confirm the actual dose.</p>
      </div>
    </section>
    <app-disclaimer />

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
              <input type="number" min="0.1" step="0.1" formControlName="preMealGlucose">
            </label>
          </div>
        </div>

        @if (directMode) {
          <div class="builder-card direct-meal-card">
            <div class="builder-section-head">
              <div>
                <p><span class="step-badge">③</span> Current Meal</p>
                <h2>{{ directFoodName }}</h2>
              </div>
              <button type="button" class="subtle" (click)="clearDirectMeal()">Use food items</button>
            </div>
            <div class="direct-meal-metrics">
              <div>
                <strong>{{ directCarbs | number:'1.0-1' }}g</strong>
                <span>Recorded carbs</span>
              </div>
              <div>
                <strong>{{ form.controls.confirmedBolus.value | number:'1.0-2' }}U</strong>
                <span>Recorded dose</span>
              </div>
            </div>
          </div>
        } @else {
          <div class="builder-card">
            <div class="builder-section-head">
              <div>
                <p><span class="step-badge">②</span> Find Food</p>
                <h2>Find what you are eating</h2>
              </div>
            </div>
            <label class="calculator-search">
              <span>Search existing foods</span>
              <input [value]="foodSearch" (input)="foodSearch = $any($event.target).value" placeholder="Search foods..." autocomplete="off">
            </label>
            <div class="food-helper">
              <div>
                <strong>Can't find your food?</strong>
                <p>Create it once, then reuse it later.</p>
              </div>
              <button type="button" class="subtle" (click)="showCreateFood = true">Create new food</button>
            </div>
          </div>

          @if (showCreateFood) {
            <div class="builder-card create-food-card" [formGroup]="newFoodForm">
              <div class="builder-section-head">
                <div>
                  <p>New Food</p>
                  <h2>Add it to your library</h2>
                </div>
              </div>
              <div class="grid">
                <label>Name <input formControlName="name"></label>
                <div class="grid two">
                  <label>Carbs per 100g <input type="number" min="0" step="0.1" formControlName="carbsPer100g"></label>
                  <label>Protein per 100g <input type="number" min="0" step="0.1" formControlName="proteinPer100g"></label>
                  <label>Fat per 100g <input type="number" min="0" step="0.1" formControlName="fatPer100g"></label>
                  <label>Calories per 100g <input type="number" min="0" step="1" formControlName="caloriesPer100g"></label>
                </div>
                <div class="grid two">
                  <label>Weight for Save and Add
                    <input type="number" min="1" step="1" formControlName="weightGrams">
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

          <div class="builder-card">
            <div class="builder-section-head">
              <div>
                <p><span class="step-badge">③</span> Current Meal</p>
                <h2>{{ items.length }} {{ items.length === 1 ? 'item' : 'items' }} added</h2>
              </div>
            </div>
            <div formArrayName="items" class="current-meal-list">
              @for (item of items.controls; track $index; let i = $index) {
                <article class="meal-item-card" [formGroupName]="i">
                  <div class="meal-item-icon" aria-hidden="true">{{ itemIcon(i) }}</div>
                  <div class="meal-item-main">
                    <label>Food
                      <select formControlName="foodItemId">
                        <option value="">Choose food</option>
                        @for (food of filteredFoods(); track food.id) {
                          <option [value]="food.id">{{ food.name }} ({{ food.carbsPer100g }}g/100g)</option>
                        }
                      </select>
                    </label>
                    <p>{{ itemName(i) }}</p>
                  </div>
                  <label class="meal-weight">Portion
                    <input type="number" min="1" step="1" formControlName="weightGrams">
                    <span>g</span>
                  </label>
                  <div class="meal-item-carbs">
                    <strong>{{ itemCarbs(i) | number:'1.0-1' }}g</strong>
                    <span>carbs</span>
                  </div>
                  <button type="button" class="subtle meal-remove" (click)="removeItem(i)" [disabled]="items.length === 1">Remove</button>
                </article>
              }
            </div>
            <button type="button" class="subtle add-another-food" (click)="addItem()">Add another food</button>
          </div>
        }
      </section>

      <aside class="calculation-card">
        <div class="builder-section-head">
          <div>
            <p><span class="step-badge">④</span> Calculation Summary</p>
            <h2>Review the result</h2>
          </div>
        </div>
        @if (calculation) {
          <div class="calculation-hero-values">
            <div>
              <strong>{{ calculation.totalCarbs | number:'1.0-1' }}g</strong>
              <span>Estimated carbs</span>
            </div>
            <div>
              <strong>{{ calculation.suggestedBolus | number:'1.0-2' }}U</strong>
              <span>Suggested insulin</span>
            </div>
          </div>
          <div class="calculation-breakdown">
            <span>Meal bolus <strong>{{ calculation.mealBolus | number:'1.0-2' }}U</strong></span>
            <span>Correction <strong>{{ calculation.correctionBolus | number:'1.0-2' }}U</strong></span>
          </div>
          <p class="calculation-note">This is only a suggestion. You know your body best.</p>
        } @else {
          <div class="calculation-empty">
            <strong>Add meal details to calculate.</strong>
            <p>Choose foods and portions, then the estimate will appear here.</p>
          </div>
        }

        <div class="confirmation-fields">
          <div class="builder-section-head compact">
            <div>
              <p><span class="step-badge">⑤</span> Confirm</p>
              <h2>Confirm the actual dose</h2>
            </div>
          </div>
          <label>Confirmed dose
            <input type="number" min="0" step="0.1" formControlName="confirmedBolus">
          </label>
          <label>Notes
            <textarea rows="4" formControlName="notes" placeholder="Anything worth remembering?"></textarea>
          </label>
        </div>
        @if (error) { <p class="error">{{ error }}</p> }
        <div class="save-actions">
          <button type="submit" class="save-meal-button" [disabled]="form.invalid || !calculation">Save Meal</button>
          <button type="button" class="outline-button" [disabled]="form.invalid || !calculation" (click)="saveAndAddAnother()">Save & Add Another</button>
        </div>
      </aside>
    </form>
  `
})
export class CalculatorComponent implements OnInit {
  mealTypes = mealTypes;
  foods: FoodItem[] = [];
  calculation?: MealCalculation;
  error = '';
  directMode = false;
  directCarbs?: number;
  directFoodName = '';
  foodSearch = '';
  showCreateFood = false;

  newFoodForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    carbsPer100g: [0, [Validators.required, Validators.min(0)]],
    proteinPer100g: [0, [Validators.min(0)]],
    fatPer100g: [0, [Validators.min(0)]],
    caloriesPer100g: [0, [Validators.min(0)]],
    isFavorite: [false],
    weightGrams: [100, [Validators.required, Validators.min(1)]]
  });

  form = this.fb.group({
    mealType: this.fb.nonNullable.control('Breakfast', Validators.required),
    preMealGlucose: this.fb.nonNullable.control(6.5, [Validators.required, Validators.min(0.1)]),
    confirmedBolus: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    notes: this.fb.control(''),
    items: this.fb.array([
      this.fb.group({
        foodItemId: this.fb.nonNullable.control('', Validators.required),
        weightGrams: this.fb.nonNullable.control(100, [Validators.required, Validators.min(1)])
      })
    ])
  });

  get items() {
    return this.form.controls.items as FormArray;
  }

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService, private readonly router: Router) {}

  ngOnInit() {
    this.api.getFoods().subscribe((foods) => this.foods = foods);
    this.form.valueChanges.subscribe(() => this.calculate());
    const previousMeal = history.state?.meal;
    const deliveryMeal = history.state?.deliveryMeal;
    if (deliveryMeal) {
      this.directMode = true;
      this.directCarbs = deliveryMeal.carbs;
      this.directFoodName = `${deliveryMeal.placeName} - ${deliveryMeal.dishName}`;
      this.items.clear();
      this.form.patchValue({
        preMealGlucose: deliveryMeal.lastPreMealGlucose ?? this.form.controls.preMealGlucose.value,
        confirmedBolus: deliveryMeal.usualInsulinUnits,
        notes: deliveryMeal.notes ?? ''
      }, { emitEvent: false });
      this.form.controls.confirmedBolus.markAsDirty();
    }
    if (previousMeal?.items?.length) {
      this.form.patchValue({
        mealType: previousMeal.mealType,
        preMealGlucose: previousMeal.preMealGlucose,
        notes: previousMeal.notes ?? ''
      });
      this.items.clear();
      for (const item of previousMeal.items) {
        this.items.push(this.fb.group({
          foodItemId: this.fb.nonNullable.control(item.foodItemId, Validators.required),
          weightGrams: this.fb.nonNullable.control(item.weightGrams, [Validators.required, Validators.min(1)])
        }));
      }
    }
    this.calculate();
  }

  addItem() {
    this.directMode = false;
    this.directCarbs = undefined;
    this.directFoodName = '';
    this.items.push(this.fb.group({
      foodItemId: this.fb.nonNullable.control('', Validators.required),
      weightGrams: this.fb.nonNullable.control(100, [Validators.required, Validators.min(1)])
    }));
  }

  clearDirectMeal() {
    this.directMode = false;
    this.directCarbs = undefined;
    this.directFoodName = '';
    if (this.items.length === 0) {
      this.addItem();
    }
    this.calculate();
  }

  removeItem(index: number) {
    this.items.removeAt(index);
    this.calculate();
  }

  itemCarbs(index: number) {
    const item = this.items.at(index).value as { foodItemId: string; weightGrams: number };
    const food = this.foods.find((f) => f.id === item.foodItemId);
    return food ? item.weightGrams * food.carbsPer100g / 100 : 0;
  }

  itemName(index: number) {
    const item = this.items.at(index).value as { foodItemId: string };
    return this.foods.find((food) => food.id === item.foodItemId)?.name ?? 'Choose a food to add it here';
  }

  itemIcon(index: number) {
    return this.foodIcon(this.itemName(index));
  }

  filteredFoods() {
    const term = this.foodSearch.trim().toLowerCase();
    if (!term) return this.foods;
    return this.foods.filter((food) => food.name.toLowerCase().includes(term));
  }

  saveNewFood(addToMeal: boolean) {
    if (this.newFoodForm.invalid) return;
    const value = this.newFoodForm.getRawValue();
    this.api.createFood({
      name: value.name,
      carbsPer100g: value.carbsPer100g,
      proteinPer100g: value.proteinPer100g ?? 0,
      fatPer100g: value.fatPer100g ?? 0,
      caloriesPer100g: value.caloriesPer100g ?? 0,
      isFavorite: value.isFavorite
    }).subscribe({
      next: (food) => {
        this.foods = [food, ...this.foods.filter((item) => item.id !== food.id)].sort((a, b) => a.name.localeCompare(b.name));
        if (addToMeal) {
          this.addFoodToMeal(food.id, value.weightGrams);
          this.showCreateFood = false;
        }
        this.foodSearch = food.name;
        this.newFoodForm.reset({ name: '', carbsPer100g: 0, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false, weightGrams: 100 });
      },
      error: (err) => this.error = err?.error?.title ?? 'Could not create food.'
    });
  }

  private addFoodToMeal(foodItemId: string, weightGrams: number) {
    this.directMode = false;
    if (this.items.length === 0) {
      this.addItem();
    }

    const emptyItem = this.items.controls.find((control) => !control.value.foodItemId);
    const target = emptyItem ?? this.fb.group({
      foodItemId: this.fb.nonNullable.control('', Validators.required),
      weightGrams: this.fb.nonNullable.control(100, [Validators.required, Validators.min(1)])
    });

    if (!emptyItem) {
      this.items.push(target);
    }

    target.patchValue({ foodItemId, weightGrams });
    this.calculate();
  }

  calculate() {
    if (this.form.controls.preMealGlucose.invalid || (!this.directMode && this.items.invalid)) {
      this.calculation = undefined;
      return;
    }

    const request = {
      mealType: this.form.controls.mealType.value as any,
      preMealGlucose: this.form.controls.preMealGlucose.value,
      items: this.directMode ? [] : this.items.value as { foodItemId: string; weightGrams: number }[],
      directCarbs: this.directMode ? this.directCarbs : undefined,
      directFoodName: this.directMode ? this.directFoodName : undefined
    };

    this.api.calculateMeal(request).subscribe({
      next: (result) => {
        this.calculation = result;
        if (!this.form.controls.confirmedBolus.dirty) {
          this.form.controls.confirmedBolus.setValue(result.suggestedBolus, { emitEvent: false });
        }
        this.error = '';
      },
      error: () => {
        this.calculation = undefined;
      }
    });
  }

  save() {
    if (this.form.invalid || !this.calculation) return;
    this.api.createMeal(this.createMealRequest()).subscribe({
      next: (meal) => this.router.navigate(['/meals', meal.id]),
      error: (err) => this.error = err?.error?.title ?? 'Could not save meal.'
    });
  }

  saveAndAddAnother() {
    if (this.form.invalid || !this.calculation) return;
    this.api.createMeal(this.createMealRequest()).subscribe({
      next: () => this.resetMealBuilder(),
      error: (err) => this.error = err?.error?.title ?? 'Could not save meal.'
    });
  }

  private createMealRequest() {
    const value = this.form.getRawValue();
    return {
      mealType: value.mealType as any,
      preMealGlucose: value.preMealGlucose,
      confirmedBolus: value.confirmedBolus,
      notes: value.notes ?? '',
      items: this.directMode ? [] : value.items,
      directCarbs: this.directMode ? this.directCarbs : undefined,
      directFoodName: this.directMode ? this.directFoodName : undefined
    };
  }

  private resetMealBuilder() {
    this.directMode = false;
    this.directCarbs = undefined;
    this.directFoodName = '';
    this.foodSearch = '';
    this.showCreateFood = false;
    this.items.clear();
    this.items.push(this.fb.group({
      foodItemId: this.fb.nonNullable.control('', Validators.required),
      weightGrams: this.fb.nonNullable.control(100, [Validators.required, Validators.min(1)])
    }));
    this.form.reset({
      mealType: this.form.controls.mealType.value,
      preMealGlucose: this.form.controls.preMealGlucose.value,
      confirmedBolus: 0,
      notes: '',
      items: [{ foodItemId: '', weightGrams: 100 }]
    });
    this.form.controls.confirmedBolus.markAsPristine();
    this.calculation = undefined;
    this.error = '';
  }

  private foodIcon(name: string) {
    const value = name.toLowerCase();
    if (/(bread|toast|bun|bagel|pita|lavash)/.test(value)) return '🍞';
    if (/(apple|banana|berry|fruit|orange|pear)/.test(value)) return '🍎';
    if (/(chocolate|candy|cookie|cake|dessert)/.test(value)) return '🍫';
    if (/(milk|yogurt|cheese|cottage|kefir)/.test(value)) return '🥛';
    if (/(rice|pilaf|buckwheat|pasta|noodle|porridge)/.test(value)) return '🍚';
    if (/(meat|beef|pork|chicken|cutlet|sausage)/.test(value)) return '🥩';
    if (/(salad|lettuce|cucumber|tomato|vegetable)/.test(value)) return '🥗';
    if (/(coffee|latte|espresso|cappuccino)/.test(value)) return '☕';
    if (/(sushi|roll|salmon|tuna)/.test(value)) return '🍣';
    if (/(soup|borscht|broth)/.test(value)) return '🥣';
    return '🍽️';
  }
}
