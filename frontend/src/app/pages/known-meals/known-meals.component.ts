import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, UpsertKnownMealRequest } from '../../core/api.service';
import { KnownMeal, KnownMealSections, resultRatings } from '../../core/models';

@Component({
  selector: 'app-known-meals',
  standalone: true,
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, ReactiveFormsModule],
  template: `
    <section class="page-title">
      <div>
        <h1>Ask Past Me</h1>
        <p>Ask Past Me before recalculating meals you already know.</p>
      </div>
    </section>

    <section class="card">
      <label>Search restaurant, dish, or tags
        <input [value]="search" (input)="search = $any($event.target).value; load()" placeholder="Sushi Master, latte, delivery">
      </label>
    </section>

    <section class="grid two">
      <article class="card">
        <h2>{{ editingId ? 'Edit counted meal' : 'Add counted meal' }}</h2>
        <form [formGroup]="form" class="grid" (ngSubmit)="save()">
          <div class="grid two">
            <label>Place name <input formControlName="placeName"></label>
            <label>Dish name <input formControlName="dishName"></label>
          </div>
          <label>Portion description <input formControlName="portionDescription" placeholder="Large set, 350g bowl, 1 medium cup"></label>
          <div class="grid three">
            <label>Carbs <input type="number" min="0.1" step="0.1" formControlName="carbs"></label>
            <label>Usual insulin units <input type="number" min="0" step="0.1" formControlName="usualInsulinUnits"></label>
            <label>Last pre-meal glucose <input type="number" min="0" step="0.1" formControlName="lastPreMealGlucose"></label>
          </div>
          <div class="grid two">
            <label>Result
              <select formControlName="resultRating">
                @for (rating of resultRatings; track rating) { <option [value]="rating">{{ labelRating(rating) }}</option> }
              </select>
            </label>
            <label>Tags <input formControlName="tags" placeholder="sushi, delivery, dinner"></label>
          </div>
          <label>Notes <textarea rows="3" formControlName="notes"></textarea></label>
          <label class="toolbar">
            <input style="width:auto" type="checkbox" formControlName="isFavorite">
            Favorite
          </label>
          <div class="actions">
            <button type="submit" [disabled]="form.invalid">{{ editingId ? 'Update' : 'Save' }}</button>
            <button type="button" class="subtle" (click)="reset()">Clear</button>
          </div>
        </form>
      </article>

      <article class="card">
        <h2>Search results</h2>
        <div class="meal-card-list">
          @for (meal of sections.searchResults; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p>No counted meals yet.</p>
          }
        </div>
      </article>
    </section>

    <section class="grid">
      <article>
        <h2>Favorites</h2>
        <div class="known-grid">
          @for (meal of sections.favorites; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Favorite meals will appear here.</p>
          }
        </div>
      </article>

      <article>
        <h2>Most Used</h2>
        <div class="known-grid">
          @for (meal of sections.mostUsed; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Use a counted meal to build this list.</p>
          }
        </div>
      </article>

      <article>
        <h2>Recently Used</h2>
        <div class="known-grid">
          @for (meal of sections.recentlyUsed; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Recent repeats will appear here.</p>
          }
        </div>
      </article>
    </section>

    <ng-template #mealCard let-meal="meal">
      <article class="card known-card">
        <div class="known-card-head">
          <div>
            <p class="muted">{{ meal.placeName }}</p>
            <h3>{{ meal.dishName }}</h3>
          </div>
          <button type="button" class="subtle icon-button" (click)="toggleFavorite(meal)" [title]="meal.isFavorite ? 'Unfavorite' : 'Favorite'">
            {{ meal.isFavorite ? '★' : '☆' }}
          </button>
        </div>
        <p>{{ meal.portionDescription }}</p>
        <div class="grid two compact-stats">
          <span>Carbs: <strong>{{ meal.carbs | number:'1.0-1' }} g</strong></span>
          <span>Usually: <strong>{{ meal.usualInsulinUnits | number:'1.0-2' }} U</strong></span>
          <span>Result: <strong>{{ labelRating(meal.resultRating) }}</strong></span>
          <span>Used: <strong>{{ meal.usageCount }} times</strong></span>
        </div>
        <p class="muted">Last used: {{ meal.lastUsedAt ? (meal.lastUsedAt | date:'mediumDate') : 'Never' }}</p>
        @if (meal.tags) { <p><span class="pill">{{ meal.tags }}</span></p> }
        <div class="actions">
          <button type="button" (click)="useAgain(meal)">Use Again</button>
          <button type="button" class="subtle" (click)="edit(meal)">Edit</button>
          <button type="button" class="danger" (click)="delete(meal.id)">Delete</button>
        </div>
      </article>
    </ng-template>
  `
})
export class KnownMealsComponent implements OnInit {
  resultRatings = resultRatings;
  search = '';
  editingId = '';
  sections: KnownMealSections = { favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] };

  form = this.fb.nonNullable.group({
    placeName: ['', Validators.required],
    dishName: ['', Validators.required],
    portionDescription: ['', Validators.required],
    carbs: [0, [Validators.required, Validators.min(0.1)]],
    usualInsulinUnits: [0, [Validators.required, Validators.min(0)]],
    lastPreMealGlucose: [0],
    resultRating: ['Unknown', Validators.required],
    tags: [''],
    notes: [''],
    isFavorite: [false]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService, private readonly router: Router) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.getKnownMeals(this.search).subscribe((sections) => this.sections = sections);
  }

  save() {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    const request: UpsertKnownMealRequest = {
      ...raw,
      resultRating: raw.resultRating as any,
      lastPreMealGlucose: raw.lastPreMealGlucose > 0 ? raw.lastPreMealGlucose : undefined
    };
    const save$ = this.editingId ? this.api.updateKnownMeal(this.editingId, request) : this.api.createKnownMeal(request);
    save$.subscribe(() => {
      this.reset();
      this.load();
    });
  }

  edit(meal: KnownMeal) {
    this.editingId = meal.id;
    this.form.patchValue({
      placeName: meal.placeName,
      dishName: meal.dishName,
      portionDescription: meal.portionDescription,
      carbs: meal.carbs,
      usualInsulinUnits: meal.usualInsulinUnits,
      lastPreMealGlucose: meal.lastPreMealGlucose ?? 0,
      resultRating: meal.resultRating,
      tags: meal.tags,
      notes: meal.notes ?? '',
      isFavorite: meal.isFavorite
    });
  }

  toggleFavorite(meal: KnownMeal) {
    this.api.toggleKnownMealFavorite(meal.id).subscribe(() => this.load());
  }

  useAgain(meal: KnownMeal) {
    this.api.useKnownMealAgain(meal.id).subscribe((prefill) => {
      this.router.navigate(['/calculator'], {
        state: {
          knownMeal: {
            ...prefill,
            placeName: meal.placeName,
            dishName: meal.dishName,
            lastPreMealGlucose: meal.lastPreMealGlucose
          }
        }
      });
    });
  }

  delete(id: string) {
    this.api.deleteKnownMeal(id).subscribe(() => this.load());
  }

  reset() {
    this.editingId = '';
    this.form.reset({
      placeName: '',
      dishName: '',
      portionDescription: '',
      carbs: 0,
      usualInsulinUnits: 0,
      lastPreMealGlucose: 0,
      resultRating: 'Unknown',
      tags: '',
      notes: '',
      isFavorite: false
    });
  }

  labelRating(rating: string) {
    return rating.replace(/([a-z])([A-Z])/g, '$1 $2');
  }
}
