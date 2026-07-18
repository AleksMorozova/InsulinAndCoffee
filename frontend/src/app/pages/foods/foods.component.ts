import { DecimalPipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService, UpsertFoodRequest } from '../../core/api.service';
import { FoodItem } from '../../core/models';

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
            <span><strong>{{ food.carbsPer100g | number:'1.0-1' }}g</strong> carbs /100g</span>
            <span><strong>{{ food.proteinPer100g | number:'1.0-1' }}g</strong> protein</span>
            <span><strong>{{ food.caloriesPer100g | number:'1.0-0' }}</strong> kcal</span>
          </div>

          <button type="button" class="subtle icon-button favorite-button" (click)="toggleFavorite(food)" [title]="food.isFavorite ? 'Remove favorite' : 'Add favorite'">
            {{ food.isFavorite ? '★' : '☆' }}
          </button>

          <details class="overflow-menu">
            <summary title="More actions">⋮</summary>
            <div class="overflow-panel">
              <button type="button" class="subtle" (click)="openEdit(food)">Edit</button>
              <button type="button" class="ghost-danger" (click)="delete(food.id)">Delete</button>
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
              <p>{{ editingId ? 'Edit food' : 'New food' }}</p>
              <h2>{{ editingId ? 'Update nutrition values' : 'Add food to library' }}</h2>
            </div>
            <button type="button" class="subtle icon-button" (click)="closeEditor()" title="Close">x</button>
          </div>

          <form [formGroup]="form" class="grid" (ngSubmit)="save()">
            <label>Name <input formControlName="name" placeholder="Bread, borscht, latte..."></label>
            <div class="grid two">
              <label>Carbs / 100g <input type="number" min="0" step="0.1" formControlName="carbsPer100g"></label>
              <label>Protein / 100g <input type="number" min="0" step="0.1" formControlName="proteinPer100g"></label>
              <label>Fat / 100g <input type="number" min="0" step="0.1" formControlName="fatPer100g"></label>
              <label>Calories / 100g <input type="number" min="0" step="1" formControlName="caloriesPer100g"></label>
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
  search = '';
  editingId = '';
  isEditorOpen = false;
  filter: FoodFilter = 'all';
  sort: FoodSort = 'newest';

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    carbsPer100g: [0, [Validators.required, Validators.min(0)]],
    proteinPer100g: [0, [Validators.required, Validators.min(0)]],
    fatPer100g: [0, [Validators.required, Validators.min(0)]],
    caloriesPer100g: [0, [Validators.required, Validators.min(0)]],
    isFavorite: [false]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit() {
    this.loadFoods();
  }

  loadFoods() {
    this.api.getFoods(this.search).subscribe((foods) => this.foods = foods);
  }

  visibleFoods() {
    const filtered = this.filter === 'favorites' ? this.foods.filter((food) => food.isFavorite) : [...this.foods];
    return filtered.sort((a, b) => {
      if (this.sort === 'alphabetical') return a.name.localeCompare(b.name);
      if (this.sort === 'highestCarbs') return b.carbsPer100g - a.carbsPer100g;
      if (this.sort === 'lowestCarbs') return a.carbsPer100g - b.carbsPer100g;
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
    this.editingId = food.id;
    this.form.patchValue(food);
    this.isEditorOpen = true;
  }

  closeEditor() {
    this.isEditorOpen = false;
    this.reset();
  }

  save() {
    if (this.form.invalid) return;
    const request = this.form.getRawValue() as UpsertFoodRequest;
    const save$ = this.editingId ? this.api.updateFood(this.editingId, request) : this.api.createFood(request);
    save$.subscribe(() => {
      this.closeEditor();
      this.loadFoods();
    });
  }

  toggleFavorite(food: FoodItem) {
    const request: UpsertFoodRequest = {
      name: food.name,
      carbsPer100g: food.carbsPer100g,
      proteinPer100g: food.proteinPer100g,
      fatPer100g: food.fatPer100g,
      caloriesPer100g: food.caloriesPer100g,
      isFavorite: !food.isFavorite
    };
    this.api.updateFood(food.id, request).subscribe(() => this.loadFoods());
  }

  delete(id: string) {
    if (!confirm('Delete this food?')) return;
    this.api.deleteFood(id).subscribe(() => this.loadFoods());
  }

  reset() {
    this.editingId = '';
    this.form.reset({ name: '', carbsPer100g: 0, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false });
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
