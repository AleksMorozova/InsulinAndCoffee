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
        <h1>Current Calculator</h1>
        <p>Build the meal, review the suggestion, then confirm the actual dose.</p>
      </div>
      @if (!directMode) {
        <button type="button" class="subtle" (click)="addItem()">Add food</button>
      } @else {
        <button type="button" class="subtle" (click)="clearDirectMeal()">Use food items</button>
      }
    </section>
    <app-disclaimer />

    <form [formGroup]="form" class="grid two" (ngSubmit)="save()">
      <section class="card grid">
        <div class="grid two">
          <label>Meal type
            <select formControlName="mealType">
              @for (type of mealTypes; track type) { <option [value]="type">{{ type }}</option> }
            </select>
          </label>
          <label>Pre-meal glucose
            <input type="number" min="0.1" step="0.1" formControlName="preMealGlucose">
          </label>
        </div>

        @if (directMode) {
          <div class="card">
            <p class="pill">Ask Past Me</p>
            <h2>{{ directFoodName }}</h2>
            <div class="grid two">
              <div class="stat"><span>Known carbs</span><strong>{{ directCarbs | number:'1.0-1' }} g</strong></div>
              <div class="stat"><span>Usual insulin</span><strong>{{ form.controls.confirmedBolus.value | number:'1.0-2' }} u</strong></div>
            </div>
          </div>
        } @else {
          <label>Search foods
            <input [value]="foodSearch" (input)="foodSearch = $any($event.target).value" placeholder="Search existing foods">
          </label>
          <div class="notice">
            <strong>Food not found?</strong>
            <button type="button" class="subtle" (click)="showCreateFood = true">Create New Food</button>
          </div>

          @if (showCreateFood) {
            <div class="card">
              <h2>Create New Food</h2>
              <form [formGroup]="newFoodForm" class="grid" (ngSubmit)="saveNewFood(false)">
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
                  <button type="submit" [disabled]="newFoodForm.invalid">Save</button>
                  <button type="button" class="secondary" [disabled]="newFoodForm.invalid" (click)="saveNewFood(true)">Save and Add</button>
                  <button type="button" class="subtle" (click)="showCreateFood = false">Cancel</button>
                </div>
              </form>
            </div>
          }

          <div formArrayName="items" class="grid">
            @for (item of items.controls; track $index; let i = $index) {
              <div class="card" [formGroupName]="i">
                <div class="grid two">
                  <label>Food
                  <select formControlName="foodItemId">
                    <option value="">Choose food</option>
                      @for (food of filteredFoods(); track food.id) {
                        <option [value]="food.id">{{ food.name }} ({{ food.carbsPer100g }}g/100g)</option>
                      }
                    </select>
                  </label>
                  <label>Weight grams
                    <input type="number" min="1" step="1" formControlName="weightGrams">
                  </label>
                </div>
                <div class="actions">
                  <span class="pill">{{ itemCarbs(i) | number:'1.0-1' }} g carbs</span>
                  <button type="button" class="danger" (click)="removeItem(i)" [disabled]="items.length === 1">Remove</button>
                </div>
              </div>
            }
          </div>
        }
      </section>

      <aside class="card calc-total grid">
        <h2>Calculation</h2>
        @if (calculation) {
          <div class="grid two">
            <div class="stat"><span>Total carbs</span><strong>{{ calculation.totalCarbs | number:'1.0-1' }} g</strong></div>
            <div class="stat"><span>Meal bolus</span><strong>{{ calculation.mealBolus | number:'1.0-2' }} u</strong></div>
            <div class="stat"><span>Correction</span><strong>{{ calculation.correctionBolus | number:'1.0-2' }} u</strong></div>
            <div class="stat"><span>Suggested</span><strong>{{ calculation.suggestedBolus | number:'1.0-2' }} u</strong></div>
          </div>
        } @else {
          <p>Add valid meal details to calculate.</p>
        }
        <label>Confirmed actual bolus
          <input type="number" min="0" step="0.1" formControlName="confirmedBolus">
        </label>
        <label>Notes
          <textarea rows="4" formControlName="notes"></textarea>
        </label>
        @if (error) { <p class="error">{{ error }}</p> }
        <button type="submit" [disabled]="form.invalid || !calculation">Save meal</button>
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
    const knownMeal = history.state?.knownMeal;
    if (knownMeal) {
      this.directMode = true;
      this.directCarbs = knownMeal.carbs;
      this.directFoodName = `${knownMeal.placeName} - ${knownMeal.dishName}`;
      this.items.clear();
      this.form.patchValue({
        preMealGlucose: knownMeal.lastPreMealGlucose ?? this.form.controls.preMealGlucose.value,
        confirmedBolus: knownMeal.usualInsulinUnits,
        notes: knownMeal.notes ?? ''
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
    const value = this.form.getRawValue();
    this.api.createMeal({
      mealType: value.mealType as any,
      preMealGlucose: value.preMealGlucose,
      confirmedBolus: value.confirmedBolus,
      notes: value.notes ?? '',
      items: this.directMode ? [] : value.items,
      directCarbs: this.directMode ? this.directCarbs : undefined,
      directFoodName: this.directMode ? this.directFoodName : undefined
    }).subscribe({
      next: (meal) => this.router.navigate(['/meals', meal.id]),
      error: (err) => this.error = err?.error?.title ?? 'Could not save meal.'
    });
  }
}
