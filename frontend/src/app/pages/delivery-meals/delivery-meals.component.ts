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
    <section class="memory-hero">
      <div class="memory-hero-copy">
        <h2>Ask Past Me</h2>
        <p>Built from your own experience. So you never count the same meal twice.</p>
      </div>

      <button type="button" class="hero-add-button" (click)="openCreate()">+ Add counted meal</button>

      <label class="hero-search">
        <span>Search your memories</span>
        <input
          [value]="search"
          (input)="search = $any($event.target).value; load()"
          placeholder="Search restaurant, dish, tags, or notes..."
          autocomplete="off">
      </label>
    </section>

    <section class="memory-section">
      <div class="section-head">
        <div>
          <h2>⭐ Favorites ({{ sections.favorites.length }})</h2>
          <p>Meals you reuse often.</p>
        </div>
      </div>
      <div class="quick-meal-strip">
        @for (meal of sections.favorites; track meal.id) {
          <ng-container [ngTemplateOutlet]="quickMealCard" [ngTemplateOutletContext]="{ meal: meal }" />
        } @empty {
          <div class="empty-state wide">
            <strong>No favorites yet.</strong>
            <p>Favorite the meals you trust most and they will stay close.</p>
          </div>
        }
      </div>
    </section>

    <section class="memory-section">
      <div class="section-head">
        <div>
          <h2>🔍 Search results ({{ sections.searchResults.length }})</h2>
          <p>{{ search ? 'Meals matching your memory.' : 'All counted meals.' }}</p>
        </div>
      </div>

      <div class="memory-card-grid">
        @for (meal of sections.searchResults; track meal.id) {
          <ng-container [ngTemplateOutlet]="mealCard" [ngTemplateOutletContext]="{ meal: meal }" />
        } @empty {
          <div class="empty-state wide">
            <strong>{{ search ? 'Nothing matched that search.' : 'No counted meals yet.' }}</strong>
            <p>{{ search ? 'Try a restaurant, dish, tag, or note from the meal.' : 'Add your first counted meal so this page can help you remember it later.' }}</p>
            @if (!search) {
              <button type="button" (click)="openCreate()">Add counted meal</button>
            }
          </div>
        }
      </div>
    </section>

    @if (isEditorOpen) {
      <div class="modal-backdrop" (click)="closeEditor()">
        <article class="delivery-modal" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <div>
              <p>{{ editingId ? 'Update memory' : 'New memory' }}</p>
              <h2>{{ editingId ? 'Edit counted meal' : 'Add counted meal' }}</h2>
            </div>
            <button type="button" class="subtle icon-button" (click)="closeEditor()" title="Close">x</button>
          </div>

          <form [formGroup]="form" class="grid" (ngSubmit)="save()">
            <div class="grid two">
              <label>Restaurant or place <input formControlName="placeName"></label>
              <label>Dish name <input formControlName="dishName"></label>
            </div>
            <label>Portion or short note <input formControlName="portionDescription" placeholder="Large set, 350g bowl, 1 medium cup"></label>
            <div class="grid three">
              <label>Carbs <input type="number" min="0.1" step="0.1" formControlName="carbs"></label>
              <label>Recorded insulin <input type="number" min="0" step="0.1" formControlName="usualInsulinUnits"></label>
              <label>Pre-meal glucose <input type="number" min="0" step="0.1" formControlName="lastPreMealGlucose"></label>
            </div>
            <div class="grid two">
              <label>Result
                <select formControlName="resultRating">
                  @for (rating of resultRatings; track rating) { <option [value]="rating">{{ labelRating(rating) }}</option> }
                </select>
              </label>
              <label>Tags <input formControlName="tags" placeholder="sushi, delivery, dinner"></label>
            </div>
            <label>Notes <textarea rows="3" formControlName="notes" placeholder="What mattered last time?"></textarea></label>
            <label class="toolbar favorite-check">
              <input type="checkbox" formControlName="isFavorite">
              Favorite
            </label>
            <div class="actions modal-actions">
              <button type="submit" [disabled]="form.invalid">{{ editingId ? 'Update meal' : 'Save meal' }}</button>
              <button type="button" class="subtle" (click)="closeEditor()">Cancel</button>
            </div>
          </form>
        </article>
      </div>
    }

    <ng-template #quickMealCard let-meal="meal">
      <article class="quick-meal-card">
        <div class="quick-meal-copy">
          <h3>{{ meal.dishName }}</h3>
          <p>{{ meal.placeName }}</p>
        </div>

        <p class="quick-meal-metrics">
          <strong>{{ meal.carbs | number:'1.0-1' }}g</strong> carbs
          <span>{{ meal.usualInsulinUnits | number:'1.0-2' }}U recorded</span>
        </p>

        <div class="quick-meal-context">
          <span class="result-badge {{ resultBadgeClass(meal.resultRating) }}">{{ resultLabel(meal.resultRating) }}</span>
          <span>Last used {{ meal.lastUsedAt ? (meal.lastUsedAt | date:'mediumDate') : 'never' }}</span>
        </div>

        <button type="button" (click)="createMealDraft(meal)">Use Again</button>
      </article>
    </ng-template>

    <ng-template #mealCard let-meal="meal">
      <article class="memory-card">
        <div class="memory-card-top">
          <div>
            <p>{{ meal.placeName }}</p>
            <h3>{{ meal.dishName }}</h3>
          </div>
          <details class="overflow-menu">
            <summary title="More actions">⋮</summary>
            <div class="overflow-panel">
              <button type="button" class="subtle" (click)="openEdit(meal)">Edit</button>
              <button type="button" class="subtle" (click)="toggleFavorite(meal)">{{ meal.isFavorite ? 'Remove Favorite' : 'Add Favorite' }}</button>
              <button type="button" class="ghost-danger" (click)="delete(meal.id)">Delete</button>
            </div>
          </details>
        </div>

        <p class="meal-note">{{ meal.portionDescription }}</p>

        <div class="metric-row">
          <div class="metric">
            <strong>{{ meal.carbs | number:'1.0-1' }}g</strong>
            <span>Carbs</span>
          </div>
          <div class="metric">
            <strong>{{ meal.usualInsulinUnits | number:'1.0-2' }}U</strong>
            <span>Recorded</span>
          </div>
          <div class="metric">
            <strong>{{ meal.usageCount }}x</strong>
            <span>Used</span>
          </div>
        </div>

        <div class="meal-meta">
          <span class="result-badge {{ resultBadgeClass(meal.resultRating) }}">{{ resultLabel(meal.resultRating) }}</span>
          <span>Last used {{ meal.lastUsedAt ? (meal.lastUsedAt | date:'mediumDate') : 'never' }}</span>
        </div>

        @if (meal.tags) {
          <div class="tag-row">
            @for (tag of tagList(meal.tags); track tag) {
              <span class="tag-chip">{{ tag }}</span>
            }
          </div>
        }

        <div class="card-actions">
          <button type="button" (click)="createMealDraft(meal)">Use Again</button>
        </div>
      </article>
    </ng-template>
  `
})
export class DeliveryMealsComponent implements OnInit {
  resultRatings = resultRatings;
  search = '';
  editingId = '';
  isEditorOpen = false;
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

  openCreate() {
    this.reset();
    this.isEditorOpen = true;
  }

  openEdit(meal: DeliveryMeal) {
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
    this.isEditorOpen = true;
  }

  closeEditor() {
    this.isEditorOpen = false;
    this.reset();
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
      this.closeEditor();
      this.load();
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
    if (!confirm('Delete this counted meal?')) return;
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
    return this.resultLabel(rating);
  }

  resultLabel(rating: string) {
    const labels: Record<string, string> = {
      Perfect: 'Good',
      Good: 'Good',
      HighGlucose: 'High',
      LowGlucose: 'Low',
      Unknown: 'Unknown'
    };
    return labels[rating] ?? rating.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  resultBadgeClass(rating: string) {
    return `result-${rating.replace('Glucose', '').toLowerCase()}`;
  }

  tagList(tags: string) {
    return tags.split(',').map((tag) => tag.trim()).filter(Boolean).slice(0, 5);
  }
}
