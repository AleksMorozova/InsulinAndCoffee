import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, UpsertDeliveryMealRequest } from '../../core/api.service';
import { DeliveryMeal, DeliveryMealSections, resultRatings } from '../../core/models';

@Component({
  selector: 'app-delivery-meals',
  standalone: true,
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, ReactiveFormsModule],
  template: `
    <section class="page-title">
      <div>
        <h1>Delivery meals</h1>
        <p>Save repeat delivery orders with their previous carb estimates and notes.</p>
      </div>
    </section>

    <section class="card">
      <label>Search restaurant, dish, or tags
        <input [value]="search" (input)="search = $any($event.target).value; load()" placeholder="Sushi Master, latte, delivery">
      </label>
    </section>

    <section class="grid two">
      <article class="card">
        <h2>{{ editingId ? 'Edit delivery meal' : 'Add delivery meal' }}</h2>
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
            <p>No delivery meals yet.</p>
          }
        </div>
      </article>
    </section>

    <section class="grid">
      <article>
        <h2>Favorites</h2>
        <div class="delivery-grid">
          @for (meal of sections.favorites; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Favorite meals will appear here.</p>
          }
        </div>
      </article>

      <article>
        <h2>Most Used</h2>
        <div class="delivery-grid">
          @for (meal of sections.mostUsed; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Create a meal draft from a delivery meal to build this list.</p>
          }
        </div>
      </article>

      <article>
        <h2>Recently Used</h2>
        <div class="delivery-grid">
          @for (meal of sections.recentlyUsed; track meal.id) {
            <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
          } @empty {
            <p class="muted">Recent repeats will appear here.</p>
          }
        </div>
      </article>
    </section>

    <ng-template #mealCard let-meal="meal">
      <article class="card delivery-card">
        <div class="delivery-card-head">
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
          <button type="button" (click)="createMealDraft(meal)">Use in calculator</button>
          <button type="button" class="subtle" (click)="edit(meal)">Edit</button>
          <button type="button" class="danger" (click)="delete(meal.id)">Delete</button>
        </div>
      </article>
    </ng-template>
  `
})
export class DeliveryMealsComponent implements OnInit {
  resultRatings = resultRatings;
  search = '';
  editingId = '';
  sections: DeliveryMealSections = { favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] };

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
    this.api.getDeliveryMeals(this.search).subscribe((sections) => this.sections = sections);
  }

  save() {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue();
    const request: UpsertDeliveryMealRequest = {
      ...raw,
      resultRating: raw.resultRating as any,
      lastPreMealGlucose: raw.lastPreMealGlucose > 0 ? raw.lastPreMealGlucose : undefined
    };
    const save$ = this.editingId ? this.api.updateDeliveryMeal(this.editingId, request) : this.api.createDeliveryMeal(request);
    save$.subscribe(() => {
      this.reset();
      this.load();
    });
  }

  edit(meal: DeliveryMeal) {
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

  toggleFavorite(meal: DeliveryMeal) {
    this.api.toggleDeliveryMealFavorite(meal.id).subscribe(() => this.load());
  }

  createMealDraft(meal: DeliveryMeal) {
    this.api.createMealDraftFromDeliveryMeal(meal.id).subscribe((prefill) => {
      this.router.navigate(['/calculator'], {
        state: {
          deliveryMeal: {
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
    this.api.deleteDeliveryMeal(id).subscribe(() => this.load());
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
