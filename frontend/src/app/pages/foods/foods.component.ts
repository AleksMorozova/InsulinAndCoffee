import { DecimalPipe } from '@angular/common';
import { Component, HostListener, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService, UpsertFoodRequest } from '../../core/api.service';
import { FoodItem, FoodMeasurementType } from '../../core/models';

type FoodFilter = 'all' | 'favorites';
type FoodSort = 'newest' | 'alphabetical' | 'highestCarbs' | 'lowestCarbs' | 'favorites';

@Component({
  selector: 'app-foods',
  standalone: true,
  imports: [DecimalPipe, ReactiveFormsModule],
  template: `
    <section class="food-library-hero">
      <div class="food-library-copy">
          <h2>Food library</h2>
          <p>Your personal nutrition shelf for foods.</p>
      </div>

      <button type="button" class="hero-add-button" (click)="openCreate()">+ Add food</button>

      <label class="hero-search">
        <span>Search foods</span>
        <input
          [value]="search"
          (input)="search = $any($event.target).value; loadFoods()"
          placeholder="Search foods by name..."
          autocomplete="off">
      </label>
    </section>

    <section class="food-toolbar">
      <div class="segmented-control" aria-label="Food filter">
        <button type="button" [class.active]="filter === 'all'" (click)="setFilter('all')">All</button>
        <button type="button" [class.active]="filter === 'favorites'" (click)="setFilter('favorites')">Favorites</button>
      </div>

      <label class="compact-select">Sort
        <select [value]="sort" (change)="setSort($any($event.target).value)">
          <option value="newest">Newest</option>
          <option value="alphabetical">Alphabetical</option>
          <option value="highestCarbs">Highest carbs</option>
          <option value="lowestCarbs">Lowest carbs</option>
          <option value="favorites">Favorites</option>
        </select>
      </label>
    </section>

    <section class="food-list" aria-label="Foods">
      @for (food of visibleFoods(); track food.id) {
        <article class="food-row">
          <div class="food-icon" aria-hidden="true">{{ foodIcon(food.name) }}</div>

          <div class="food-main">
            <h2>{{ food.name }}</h2>
            <p>{{ food.isFavorite ? 'Favorite food' : 'Saved food' }}</p>
          </div>

          <div class="food-nutrition">
            <span><strong>{{ foodCarbBasisValue(food) | number:'1.0-1' }}g</strong> {{ foodCarbBasisLabel(food) }}</span>
            <span><strong>{{ food.proteinPer100g | number:'1.0-1' }}g</strong> protein</span>
            <span><strong>{{ food.caloriesPer100g | number:'1.0-0' }}</strong> kcal</span>
          </div>

          <button type="button" class="subtle icon-button favorite-button" (click)="toggleFavorite(food)" [title]="food.isFavorite ? 'Remove favorite' : 'Add favorite'">
            {{ food.isFavorite ? '★' : '☆' }}
          </button>

          <details
            class="overflow-menu"
            [open]="isMenuOpen(food.id)"
            (toggle)="onMenuToggle($event, food.id)"
            (click)="$event.stopPropagation()">
            <summary title="More actions" (keydown.escape)="closeMenu()">⋮</summary>
            <div class="overflow-panel">
              <button type="button" class="subtle" (click)="openEditFromMenu(food)">Edit</button>
              <button type="button" class="ghost-danger" (click)="deleteFromMenu(food.id)">Delete</button>
            </div>
          </details>
        </article>
      } @empty {
        <div class="food-empty-state">
          <div class="food-empty-icon" aria-hidden="true">☕</div>
          <strong>{{ filter === 'favorites' ? 'No favorite foods yet.' : 'Your food library is empty.' }}</strong>
          <p>{{ filter === 'favorites' ? 'Mark foods as favorites to keep them close.' : 'Start building your personal food database.' }}</p>
          <button type="button" (click)="openCreate()">Add food</button>
        </div>
      }
    </section>

    @if (isEditorOpen) {
      <div class="modal-backdrop" (click)="closeEditor()">
        <article class="food-editor-modal" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <div>
              @if (editingId) {
                <h2>Edit food</h2>
              } @else {
                <p>New food</p>
                <h2>Add food to library</h2>
              }
            </div>
            <button type="button" class="subtle icon-button" (click)="closeEditor()" title="Close">x</button>
          </div>

          <form [formGroup]="form" class="grid" (ngSubmit)="save()">
            <label>Name <input formControlName="name" placeholder="Bread, borscht, latte..."></label>
            <div class="measurement-field">
              <span class="form-label">Calculate by</span>
              <div class="segmented-control" aria-label="Calculate food by">
                @for (type of measurementTypes; track type) {
                  <button type="button" [class.active]="form.controls.measurementType.value === type" (click)="setMeasurementType(type)">
                    {{ measurementTypeLabel(type) }}
                  </button>
                }
              </div>
            </div>
            <div class="grid two">
              @if (form.controls.measurementType.value === 'Grams') {
                <label>Carbs per 100 g <input type="number" min="0" step="0.1" formControlName="carbsPer100g"></label>
              } @else {
                <label>Carbs per {{ unitSingular(form.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="carbsPerUnit"></label>
              }
              <label>Protein {{ nutritionBasisLabel(form.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="proteinPer100g"></label>
              <label>Fat {{ nutritionBasisLabel(form.controls.measurementType.value) }} <input type="number" min="0" step="0.1" formControlName="fatPer100g"></label>
              <label>Calories {{ nutritionBasisLabel(form.controls.measurementType.value) }} <input type="number" min="0" step="1" formControlName="caloriesPer100g"></label>
            </div>
            <label class="toolbar favorite-check">
              <input type="checkbox" formControlName="isFavorite">
              Favorite
            </label>
            <div class="actions modal-actions">
              <button type="submit" [disabled]="form.invalid">{{ editingId ? 'Update food' : 'Save food' }}</button>
              <button type="button" class="subtle" (click)="closeEditor()">Cancel</button>
            </div>
          </form>
        </article>
      </div>
    }
  `
})
export class FoodsComponent implements OnInit {
  foods: FoodItem[] = [];
  measurementTypes: FoodMeasurementType[] = ['Grams', 'Portion', 'Piece'];
  search = '';
  editingId = '';
  isEditorOpen = false;
  filter: FoodFilter = 'all';
  sort: FoodSort = 'newest';
  openMenuFoodId = '';

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    measurementType: ['Grams' as FoodMeasurementType, Validators.required],
    carbsPer100g: [0, [Validators.required, Validators.min(0)]],
    carbsPerUnit: [0, [Validators.min(0)]],
    proteinPer100g: [0, [Validators.required, Validators.min(0)]],
    fatPer100g: [0, [Validators.required, Validators.min(0)]],
    caloriesPer100g: [0, [Validators.required, Validators.min(0)]],
    isFavorite: [false]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit() {
    this.loadFoods();
  }

  @HostListener('document:click')
  closeMenu() {
    this.openMenuFoodId = '';
  }

  loadFoods() {
    this.api.getFoods(this.search).subscribe((foods) => this.foods = foods);
  }

  visibleFoods() {
    const filtered = this.filter === 'favorites' ? this.foods.filter((food) => food.isFavorite) : [...this.foods];
    return filtered.sort((a, b) => {
      if (this.sort === 'alphabetical') return a.name.localeCompare(b.name);
      if (this.sort === 'highestCarbs') return this.foodCarbBasisValue(b) - this.foodCarbBasisValue(a);
      if (this.sort === 'lowestCarbs') return this.foodCarbBasisValue(a) - this.foodCarbBasisValue(b);
      if (this.sort === 'favorites') return Number(b.isFavorite) - Number(a.isFavorite) || a.name.localeCompare(b.name);
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    });
  }

  setFilter(filter: FoodFilter) {
    this.filter = filter;
  }

  setSort(sort: FoodSort) {
    this.sort = sort;
  }

  openCreate() {
    this.reset();
    this.isEditorOpen = true;
  }

  openEdit(food: FoodItem) {
    this.closeMenu();
    this.editingId = food.id;
    this.form.reset({
      name: food.name,
      measurementType: food.measurementType,
      carbsPer100g: food.carbsPer100g ?? 0,
      carbsPerUnit: food.carbsPerUnit ?? 0,
      proteinPer100g: food.proteinPer100g,
      fatPer100g: food.fatPer100g,
      caloriesPer100g: food.caloriesPer100g,
      isFavorite: food.isFavorite
    });
    this.applyMeasurementValidators(food.measurementType);
    this.isEditorOpen = true;
  }

  closeEditor() {
    this.isEditorOpen = false;
    this.reset();
  }

  save() {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    const request: UpsertFoodRequest = {
      name: raw.name,
      measurementType: raw.measurementType,
      carbsPer100g: raw.measurementType === 'Grams' ? raw.carbsPer100g : null,
      carbsPerUnit: raw.measurementType === 'Grams' ? null : raw.carbsPerUnit,
      proteinPer100g: raw.proteinPer100g,
      fatPer100g: raw.fatPer100g,
      caloriesPer100g: raw.caloriesPer100g,
      isFavorite: raw.isFavorite
    };
    const save$ = this.editingId ? this.api.updateFood(this.editingId, request) : this.api.createFood(request);
    save$.subscribe(() => {
      this.closeEditor();
      this.loadFoods();
    });
  }

  toggleFavorite(food: FoodItem) {
    const request: UpsertFoodRequest = {
      name: food.name,
      measurementType: food.measurementType,
      carbsPer100g: food.carbsPer100g,
      carbsPerUnit: food.carbsPerUnit,
      proteinPer100g: food.proteinPer100g,
      fatPer100g: food.fatPer100g,
      caloriesPer100g: food.caloriesPer100g,
      isFavorite: !food.isFavorite
    };
    this.api.updateFood(food.id, request).subscribe(() => this.loadFoods());
  }

  delete(id: string) {
    this.closeMenu();
    if (!confirm('Delete this food?')) return;
    this.api.deleteFood(id).subscribe(() => this.loadFoods());
  }

  isMenuOpen(foodId: string) {
    return this.openMenuFoodId === foodId;
  }

  onMenuToggle(event: Event, foodId: string) {
    const details = event.currentTarget as HTMLDetailsElement;
    this.openMenuFoodId = details.open ? foodId : this.openMenuFoodId === foodId ? '' : this.openMenuFoodId;
  }

  openEditFromMenu(food: FoodItem) {
    this.closeMenu();
    this.openEdit(food);
  }

  deleteFromMenu(id: string) {
    this.closeMenu();
    this.delete(id);
  }

  reset() {
    this.editingId = '';
    this.form.reset({ name: '', measurementType: 'Grams', carbsPer100g: 0, carbsPerUnit: 0, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false });
    this.applyMeasurementValidators('Grams');
  }

  setMeasurementType(measurementType: FoodMeasurementType) {
    this.form.controls.measurementType.setValue(measurementType);
    this.applyMeasurementValidators(measurementType);
  }

  measurementTypeLabel(measurementType: FoodMeasurementType) {
    return measurementType === 'Grams' ? 'Grams' : measurementType;
  }

  unitSingular(measurementType: FoodMeasurementType) {
    return measurementType === 'Piece' ? 'piece' : 'portion';
  }

  nutritionBasisLabel(measurementType: FoodMeasurementType) {
    if (measurementType === 'Grams') return 'per 100 g';
    return `per ${this.unitSingular(measurementType)}`;
  }

  foodCarbBasisValue(food: FoodItem) {
    return food.measurementType === 'Grams'
      ? food.carbsPer100g ?? 0
      : food.carbsPerUnit ?? 0;
  }

  foodCarbBasisLabel(food: FoodItem) {
    if (food.measurementType === 'Grams') return 'carbs / 100 g';
    return `carbs / ${this.unitSingular(food.measurementType)}`;
  }

  private applyMeasurementValidators(measurementType: FoodMeasurementType) {
    if (measurementType === 'Grams') {
      this.form.controls.carbsPer100g.setValidators([Validators.required, Validators.min(0)]);
      this.form.controls.carbsPerUnit.setValidators([Validators.min(0)]);
    } else {
      this.form.controls.carbsPer100g.setValidators([Validators.min(0)]);
      this.form.controls.carbsPerUnit.setValidators([Validators.required, Validators.min(0)]);
    }

    this.form.controls.carbsPer100g.updateValueAndValidity({ emitEvent: false });
    this.form.controls.carbsPerUnit.updateValueAndValidity({ emitEvent: false });
  }

  foodIcon(name: string) {
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
