import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, UpsertDeliveryMealRequest } from '../../core/api.service';
import { CarbProvenanceComponent } from '../../shared/carb-provenance.component';
import { getApiErrorMessage } from '../../core/api-error';
import { debounceTime, finalize, Subscription, tap } from 'rxjs';
import { CalculatedMealItem, DeliveryMeal, DeliveryMealItemInput, DeliveryMealPreview, DeliveryMealSections, FoodMeasurementType, ResultRating, resultRatings } from '../../core/models';

@Component({
  selector: 'app-delivery-meals',
  standalone: true,
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, ReactiveFormsModule, CarbProvenanceComponent],
  styleUrl: './delivery-meals.component.css',
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
      @if (loading) { <p role="status">Loading saved orders...</p> }
      @if (loadError) { <p role="alert">{{ loadError }}</p><button type="button" (click)="load()">Try again</button> }
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
          @if (!loading && !loadError) {
          <div class="empty-state wide">
            <strong>No favorites yet.</strong>
            <p>Favorite the meals you trust most and they will stay close.</p>
          </div>
          }
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
          @if (!loading && !loadError) {
          <div class="empty-state wide">
            <strong>{{ search ? 'Nothing matched that search.' : 'No counted meals yet.' }}</strong>
            <p>{{ search ? 'Try a restaurant, dish, tag, or note from the meal.' : 'Add your first counted meal so this page can help you remember it later.' }}</p>
            @if (!search) {
              <button type="button" (click)="openCreate()">Add counted meal</button>
            }
          </div>
          }
        }
      </div>
    </section>

    @if (isEditorOpen) {
      <div class="modal-backdrop" (click)="closeEditor()">
        <article #orderDialog class="delivery-modal" role="dialog" aria-modal="true" aria-labelledby="counted-meal-title" (keydown)="onDialogKey($event)" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <div>
              <p>{{ editingId ? 'Update memory' : 'New memory' }}</p>
              <h2 id="counted-meal-title">{{ editingId ? 'Edit counted meal' : 'Add counted meal' }}</h2>
            </div>
            <button type="button" class="subtle dialog-close" (click)="closeEditor()" aria-label="Close counted meal editor">×</button>
          </div>

          <form [formGroup]="form" class="counted-meal-form" (ngSubmit)="save()">
            <div class="modal-content">
            <div class="order-metadata-row">
              <label>Restaurant or place <input formControlName="placeName"></label>
              <label>Dish name <input formControlName="dishName"></label>
            </div>
            <label><span>Portion or short note <span class="optional-label">(optional)</span></span><input formControlName="portionDescription" placeholder="Large set, 350g bowl, 1 medium cup"></label>
            @if (!structuredMode) {
              <button type="button" class="subtle" (click)="enableItems()">Add structured items</button>
            } @else {
              <section class="order-items-editor" aria-label="Order items">
                <div class="order-items-heading"><h3>Items</h3><button type="button" class="subtle compact-button" (click)="addOrderItem()">+ Add item</button></div>
                @for (item of editorItems.controls; track item; let i = $index) {
                  <div [formGroup]="item" class="order-item-editor">
                    <div class="order-item-name-row"><label>Item {{ i + 1 }} <input formControlName="name" placeholder="Food name"></label>
                      <button type="button" class="subtle item-delete" [attr.aria-label]="'Remove item ' + (i + 1)" title="Remove item" (click)="removeItem(i)">×</button>
                    </div>
                    <div class="order-item-fields">
                    <label>Unit <select formControlName="measurementType"><option value="Grams">Grams</option><option value="Portion">Portion</option><option value="Piece">Piece</option></select></label>
                    <label>Quantity <input type="number" min="0.01" [step]="item.controls.measurementType.value === 'Piece' ? 1 : 0.01" formControlName="quantity"></label>
                    @if (item.controls.measurementType.value === 'Grams') {
                      <label>Carbs per 100 g <input type="number" min="0" step="0.01" formControlName="carbsPer100g"></label>
                    } @else {
                      <label>Carbs per unit <input type="number" min="0" step="0.01" formControlName="carbsPerUnit"></label>
                    }
                    <label><span>Override <span class="optional-label">(optional)</span></span><input type="number" min="0" step="0.01" formControlName="carbOverride" placeholder="Auto"></label>
                    </div>
                    @if (preview?.items?.[i]; as value) {
                      <p class="order-item-result">
                        @if (value.calculatedCarbs !== value.effectiveCarbs) {
                          Calculated {{ value.calculatedCarbs | number:'1.0-2' }} g <span>→ Used <strong>{{ value.effectiveCarbs | number:'1.0-2' }} g</strong></span>
                        } @else {
                          <strong>{{ value.effectiveCarbs | number:'1.0-2' }} g carbs</strong>
                          @if (value.carbOverride !== null) { <span>Saved override · same as calculated</span> }
                        }
                      </p>
                    }
                  </div>
                }
                @if (previewing) { <p role="status">Calculating items...</p> }
                @if (preview) {
                  <div class="order-effective-total"><span>Total effective carbs</span><strong>{{ preview.totalCarbs | number:'1.0-2' }} g</strong></div>
                  @if (savedTotal !== undefined && preview.difference !== 0) {
                    <p>Saved total: {{ savedTotal | number:'1.0-2' }} g · Difference: {{ preview.difference | number:'1.0-2' }} g. No difference is distributed across items.</p>
                    @if (acceptedTotal) { <p class="order-item-result">Calculated total selected for this edit.</p> }
                    @else { <button type="button" class="subtle compact-button" (click)="acceptTotal()">Use calculated item total</button> }
                  }
                }
              </section>
            }
            @if (editError) { <p role="alert">{{ editError }}</p> }
            <div class="order-history-fields" [class.structured-history]="structuredMode">
              @if (!structuredMode) { <label>Carbs <input type="number" min="0" step="0.01" formControlName="carbs"></label> }
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
            </div>
            <div class="actions modal-actions">
              <button type="submit" [disabled]="form.invalid || saving || (structuredMode && (!itemsReady || previewing))">{{ editingId ? 'Update meal' : 'Save meal' }}</button>
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
          <app-carb-provenance [provenance]="meal.provenance" />
          <span>{{ meal.usualInsulinUnits === null ? 'Insulin unrecorded' : (meal.usualInsulinUnits | number:'1.0-2') + ' U recorded' }}</span>
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
        @if (meal.items?.length) {
          <div class="order-item-list">
            @for (item of meal.items; track item.id) {
              <div class="order-item-summary">
                <label><input type="checkbox" [checked]="isSelected(meal.id, item.id)" (change)="toggleItem(meal.id, item.id)"> {{ item.name }} · {{ item.quantity }} {{ item.measurementType }} · {{ item.effectiveCarbs | number:'1.0-2' }} g</label>
                <app-carb-provenance [provenance]="item.provenance" [overridden]="item.carbOverride !== null && item.carbOverride !== undefined" />
                <button type="button" class="subtle" (click)="reuseItems(meal, [item.id])">Use this item</button>
              </div>
            }
            <button type="button" [disabled]="!selectedItems[meal.id]?.length" (click)="reuseItems(meal, selectedItems[meal.id] || [])">Use selected items</button>
          </div>
        }

        <div class="metric-row">
          <div class="metric">
            <strong>{{ meal.carbs | number:'1.0-1' }}g</strong>
            <span>Carbs</span>
          </div>
          <div class="metric">
            <strong>{{ meal.usualInsulinUnits === null ? 'Unrecorded' : (meal.usualInsulinUnits | number:'1.0-2') + ' U' }}</strong>
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private dialogElement?: HTMLElement;
  private returnFocus: HTMLElement | null = null;
  @ViewChild('orderDialog') set orderDialog(ref: ElementRef<HTMLElement> | undefined) {
    this.dialogElement = ref?.nativeElement;
    if (ref) afterNextRender(() => ref.nativeElement.querySelector('input')?.focus(), { injector: this.injector });
  }
  private previewRequest?: Subscription;
  structuredMode = false;
  savedTotal?: number;
  expectedRevision?: number;
  acceptedTotal = false;
  preview?: DeliveryMealPreview;
  previewing = false;
  saving = false;
  editError = '';
  selectedItems: Partial<Record<string, string[]>> = {};
  editorItems = this.fb.array([this.itemForm()]);
  resultRatings = resultRatings;
  loading = true;
  loadError = '';
  search = '';
  editingId = '';
  isEditorOpen = false;
  sections: DeliveryMealSections = { favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] };

  form = this.fb.nonNullable.group({
    placeName: ['', Validators.required],
    dishName: ['', Validators.required],
    portionDescription: [''],
    carbs: [0, [Validators.required, Validators.min(0)]],
    usualInsulinUnits: this.fb.control<number | null>(null, Validators.min(0)),
    lastPreMealGlucose: [0],
    resultRating: ['Unknown' as ResultRating, Validators.required],
    tags: [''],
    notes: [''],
    isFavorite: [false]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService, private readonly router: Router) {}

  ngOnInit() {
    this.load();
    this.editorItems.valueChanges.pipe(takeUntilDestroyed(this.destroyRef), tap(() => {
      this.previewRequest?.unsubscribe(); this.preview = undefined; this.acceptedTotal = false;
    }), debounceTime(150)).subscribe(() => this.previewItems());
  }

  load() {
    this.loading = true;
    this.loadError = '';
    this.api.getDeliveryMeals(this.search).pipe(finalize(() => this.loading = false)).subscribe({
      next: sections => this.sections = sections,
      error: error => this.loadError = getApiErrorMessage(error, 'Could not load saved orders. Please try again.')
    });
  }

  openCreate() {
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.reset();
    this.isEditorOpen = true;
  }

  openEdit(meal: DeliveryMeal) {
    this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.reset();
    this.editingId = meal.id;
    this.expectedRevision = meal.revision ?? 0;
    this.savedTotal = meal.carbs;
    this.structuredMode = !!meal.items?.length;
    this.editorItems.clear({ emitEvent: false });
    for (const item of meal.items ?? []) this.editorItems.push(this.itemForm(item), { emitEvent: false });
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
    if (this.structuredMode) this.previewItems();
  }

  closeEditor() {
    this.isEditorOpen = false;
    this.reset();
    this.returnFocus?.focus();
  }

  save() {
    if (this.form.invalid || this.saving || this.structuredMode && (!this.itemsReady || this.previewing)) return;
    const raw = this.form.getRawValue();
    const request: UpsertDeliveryMealRequest = {
      ...raw,
      portionDescription: raw.portionDescription.trim() || null,
      ...(this.structuredMode ? { items: this.itemInputs(), acceptCalculatedTotal: this.acceptedTotal } : {}),
      ...(this.editingId ? { expectedRevision: this.expectedRevision } : {}),
      lastPreMealGlucose: raw.lastPreMealGlucose > 0 ? raw.lastPreMealGlucose : undefined
    };
    const save$ = this.editingId ? this.api.updateDeliveryMeal(this.editingId, request) : this.api.createDeliveryMeal(request);
    this.saving = true;
    save$.pipe(finalize(() => this.saving = false)).subscribe({
      next: () => { this.closeEditor(); this.load(); },
      error: error => this.editError = getApiErrorMessage(error, 'Could not save this order. Please try again.')
    });
  }

  toggleFavorite(meal: DeliveryMeal) {
    this.api.toggleDeliveryMealFavorite(meal.id).subscribe(() => this.load());
  }

  createMealDraft(meal: DeliveryMeal) {
    this.api.createMealDraftFromDeliveryMeal(meal.id).subscribe({ next: prefill => {
      if (prefill.items?.length) { this.navigateItems(prefill.items); return; }
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
    }, error: error => this.loadError = getApiErrorMessage(error, 'Could not reuse this order. Reload it and try again.') });
  }

  delete(id: string) {
    if (!confirm('Delete this counted meal?')) return;
    this.api.deleteDeliveryMeal(id).subscribe(() => this.load());
  }

  reset() {
    this.previewRequest?.unsubscribe();
    this.structuredMode = false; this.savedTotal = undefined; this.expectedRevision = undefined;
    this.preview = undefined; this.editError = ''; this.previewing = false; this.acceptedTotal = false;
    this.editorItems.clear({ emitEvent: false });
    this.editingId = '';
    this.form.reset({
      placeName: '',
      dishName: '',
      portionDescription: '',
      carbs: 0,
      usualInsulinUnits: null,
      lastPreMealGlucose: 0,
      resultRating: 'Unknown',
      tags: '',
      notes: '',
      isFavorite: false
    });
  }

  private itemForm(item?: DeliveryMealItemInput) {
    return this.fb.group({
      id: this.fb.control<string | null>(item?.id ?? null), foodItemId: this.fb.control<string | null>(item?.foodItemId ?? null),
      name: this.fb.nonNullable.control(item?.name ?? '', Validators.required),
      quantity: this.fb.nonNullable.control(item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]),
      measurementType: this.fb.nonNullable.control<FoodMeasurementType>(item?.measurementType ?? 'Portion'),
      carbsPer100g: this.fb.control<number | null>(item?.carbsPer100g ?? null),
      carbsPerUnit: this.fb.control<number | null>(item?.carbsPerUnit ?? null),
      carbOverride: this.fb.control<number | null>(item?.carbOverride ?? null, Validators.min(0))
    });
  }

  enableItems() { this.structuredMode = true; this.addOrderItem(); }
  addOrderItem() { this.editorItems.push(this.itemForm()); }
  removeItem(index: number) { this.editorItems.removeAt(index); }
  private itemInputs(): DeliveryMealItemInput[] {
    return this.editorItems.getRawValue().map(item => ({ ...item,
      carbsPer100g: item.measurementType === 'Grams' ? item.carbsPer100g : null,
      carbsPerUnit: item.measurementType === 'Grams' ? null : item.carbsPerUnit }));
  }
  previewItems() {
    if (!this.structuredMode) return;
    this.previewRequest?.unsubscribe(); this.preview = undefined; this.editError = ''; this.acceptedTotal = false;
    if (this.editorItems.invalid || !this.editorItems.length) return;
    this.previewing = true;
    this.previewRequest = this.api.previewDeliveryMealItems(this.itemInputs(), this.savedTotal)
      .pipe(finalize(() => this.previewing = false)).subscribe({
        next: preview => {
          this.preview = preview;
          if (this.savedTotal === undefined || preview.totalCarbs === this.savedTotal)
            this.form.controls.carbs.setValue(preview.totalCarbs);
        },
        error: error => this.editError = getApiErrorMessage(error, 'Could not calculate order items.')
      });
  }
  acceptTotal() {
    if (!this.preview) return;
    this.form.controls.carbs.setValue(this.preview.totalCarbs); this.acceptedTotal = true; this.editError = '';
  }
  get itemsReady() {
    return !!this.preview && this.form.controls.carbs.value === this.preview.totalCarbs
      && (this.savedTotal === undefined || this.savedTotal === this.preview.totalCarbs || this.acceptedTotal);
  }
  isSelected(orderId: string, itemId: string) { return this.selectedItems[orderId]?.includes(itemId) ?? false; }
  toggleItem(orderId: string, itemId: string) {
    this.selectedItems[orderId] = this.isSelected(orderId, itemId)
      ? (this.selectedItems[orderId] ?? []).filter(id => id !== itemId) : [...(this.selectedItems[orderId] ?? []), itemId];
  }
  reuseItems(meal: DeliveryMeal, ids: string[]) {
    this.api.reuseDeliveryMealItems(meal.id, ids, meal.revision ?? 0).subscribe({
      next: items => this.navigateItems(items),
      error: error => this.loadError = getApiErrorMessage(error, 'Could not reuse saved items. Reload this order.')
    });
  }
  private navigateItems(items: CalculatedMealItem[]) {
    this.router.navigate(['/calculator'], { state: { assistantMealItems: items.map(item => ({
      foodItemId: item.foodItemId, quantity: item.quantity, measurementType: item.measurementType,
      foodNameSnapshot: item.foodName, carbsPer100gSnapshot: item.carbsPer100g, carbsPerUnitSnapshot: item.carbsPerUnit,
      carbOverride: item.carbOverride, sourceDeliveryMealItemId: item.sourceDeliveryMealItemId
    })) } });
  }

  labelRating(rating: string) {
    return this.resultLabel(rating);
  }

  onDialogKey(event: KeyboardEvent) {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); this.closeEditor(); return; }
    if (event.key !== 'Tab' || !this.dialogElement) return;
    const controls = Array.from(this.dialogElement.querySelectorAll<HTMLElement>('button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href]'));
    const first = controls[0]; const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
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
