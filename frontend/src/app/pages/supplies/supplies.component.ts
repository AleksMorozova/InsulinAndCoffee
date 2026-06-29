import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService, UpsertSupplyRequest } from '../../core/api.service';
import { SupplyCheckResult, SupplyStatus } from '../../core/models';

@Component({
  selector: 'app-supplies',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule],
  template: `
    <section class="supplies-header">
      <div>
        <h1>Supplies</h1>
        <p>Track diabetes supplies and see approximately how long they will last.</p>
      </div>
      <button type="button" (click)="openCreate()">+ Add supply</button>
    </section>

    @if (loadError) {
      <div class="supplies-message error" role="alert">
        <strong>Could not load supplies.</strong>
        <span>{{ loadError }}</span>
        <button type="button" class="subtle" (click)="loadSupplies()">Try again</button>
      </div>
    } @else if (isLoading) {
      <div class="supplies-loading" aria-live="polite">Checking your supplies...</div>
    } @else {
      <section class="supplies-grid" aria-label="Supply status">
        @for (supply of supplies; track supply.id) {
          <article class="supply-card">
            <div class="supply-card-head">
              <div class="supply-title">
                <span class="supply-icon" aria-hidden="true">{{ supplyIcon(supply.name) }}</span>
                <div>
                  <h2>{{ supply.name }}</h2>
                  <span>{{ supply.currentQuantity | number:'1.0-4' }} {{ supply.unit }} available</span>
                </div>
              </div>
              <span class="supply-status" [class]="'supply-status ' + statusClass(supply.status)">{{ statusLabel(supply.status) }}</span>
            </div>

            <div class="supply-days">
              @if (supply.daysLeft !== null && supply.daysLeft !== undefined) {
                <strong>{{ supply.daysLeft | number:'1.0-1' }}</strong>
                <span>Approximate days left</span>
              } @else {
                <strong>--</strong>
                <span>Set daily usage to estimate</span>
              }
            </div>

            <dl class="supply-details">
              <div>
                <dt>Daily usage</dt>
                <dd>{{ supply.dailyUsage | number:'1.0-4' }} {{ supply.unit }}</dd>
              </div>
              <div>
                <dt>Estimated run-out</dt>
                <dd>{{ supply.estimatedRunOutDate ? (supply.estimatedRunOutDate | date:'mediumDate') : 'Usage not set' }}</dd>
              </div>
            </dl>

            <div class="supply-actions">
              <button type="button" class="subtle" (click)="openEdit(supply)">Update quantity</button>
              <details class="overflow-menu">
                <summary title="More actions">⋮</summary>
                <div class="overflow-panel">
                  <button type="button" class="subtle" (click)="openEdit(supply)">Edit details</button>
                  <button type="button" class="ghost-danger" (click)="deleteSupply(supply)">Delete</button>
                </div>
              </details>
            </div>
          </article>
        } @empty {
          <div class="supplies-empty">
            <span class="supplies-empty-icon" aria-hidden="true">□</span>
            <strong>Your supply shelf is empty.</strong>
            <p>Add the supplies you use and we'll estimate how long they may last.</p>
            <button type="button" (click)="openCreate()">Add supply</button>
          </div>
        }
      </section>
    }

    @if (isEditorOpen) {
      <div class="modal-backdrop" (click)="closeEditor()">
        <article class="supply-editor-modal" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <div>
              <p>{{ editingId ? 'Edit supply' : 'New supply' }}</p>
              <h2>{{ editingId ? 'Update supply details' : 'Add to your supply shelf' }}</h2>
            </div>
            <button type="button" class="subtle icon-button" aria-label="Close supply editor" (click)="closeEditor()">×</button>
          </div>

          <form [formGroup]="form" class="supply-form" (ngSubmit)="save()">
            <label>Name
              <input formControlName="name" placeholder="Libre 3 Sensor, Pen Needles...">
            </label>
            <div class="grid two">
              <label>Current quantity
                <input type="number" min="0" step="0.0001" formControlName="currentQuantity">
              </label>
              <label>Unit
                <input formControlName="unit" placeholder="pcs, boxes, sensors...">
              </label>
              <label>Daily usage
                <input type="number" min="0" step="0.0001" formControlName="dailyUsage">
              </label>
              <label>Low stock threshold days
                <input type="number" min="0" step="1" formControlName="lowStockThresholdDays">
              </label>
            </div>

            <aside class="supply-help">
              <strong>Daily usage examples</strong>
              <span>Libre sensor: 1 sensor / 14 days = 0.0714 per day</span>
              <span>4 pen needles per day = daily usage 4</span>
              <span>4 test strips per day = daily usage 4</span>
            </aside>

            @if (saveError) { <p class="error" role="alert">{{ saveError }}</p> }
            <div class="actions modal-actions">
              <button type="submit" [disabled]="form.invalid || isSaving">{{ isSaving ? 'Saving...' : editingId ? 'Update supply' : 'Add supply' }}</button>
              <button type="button" class="subtle" (click)="closeEditor()">Cancel</button>
            </div>
          </form>
        </article>
      </div>
    }
  `
})
export class SuppliesComponent implements OnInit {
  supplies: SupplyCheckResult[] = [];
  isLoading = true;
  isSaving = false;
  isEditorOpen = false;
  editingId = '';
  loadError = '';
  saveError = '';

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    currentQuantity: [0, [Validators.required, Validators.min(0)]],
    unit: ['pcs', Validators.required],
    dailyUsage: [0, [Validators.required, Validators.min(0)]],
    lowStockThresholdDays: [10, [Validators.required, Validators.min(0)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit() {
    this.loadSupplies();
  }

  loadSupplies() {
    this.isLoading = true;
    this.loadError = '';
    this.api.getSupplyCheck().subscribe({
      next: (supplies) => {
        this.supplies = supplies;
        this.isLoading = false;
      },
      error: () => {
        this.loadError = 'The server did not respond. Please try again.';
        this.isLoading = false;
      }
    });
  }

  openCreate() {
    this.resetForm();
    this.isEditorOpen = true;
  }

  openEdit(supply: SupplyCheckResult) {
    this.editingId = supply.id;
    this.form.setValue({
      name: supply.name,
      currentQuantity: supply.currentQuantity,
      unit: supply.unit,
      dailyUsage: supply.dailyUsage,
      lowStockThresholdDays: supply.lowStockThresholdDays
    });
    this.isEditorOpen = true;
  }

  closeEditor() {
    if (this.isSaving) return;
    this.isEditorOpen = false;
    this.resetForm();
  }

  save() {
    if (this.form.invalid || this.isSaving) return;
    this.isSaving = true;
    this.saveError = '';
    const request = this.form.getRawValue() as UpsertSupplyRequest;
    const save$ = this.editingId
      ? this.api.updateSupply(this.editingId, request)
      : this.api.createSupply(request);

    save$.subscribe({
      next: () => {
        this.isSaving = false;
        this.isEditorOpen = false;
        this.resetForm();
        this.loadSupplies();
      },
      error: (error) => {
        this.saveError = error?.error?.title ?? 'Could not save this supply.';
        this.isSaving = false;
      }
    });
  }

  deleteSupply(supply: SupplyCheckResult) {
    if (!confirm(`Delete ${supply.name}?`)) return;
    this.api.deleteSupply(supply.id).subscribe({
      next: () => this.loadSupplies(),
      error: () => this.loadError = `Could not delete ${supply.name}.`
    });
  }

  statusLabel(status: SupplyStatus) {
    return { Ok: 'Looks okay', Low: 'Restock soon', Critical: 'Critical', Unknown: 'Usage not set' }[status];
  }

  statusClass(status: SupplyStatus) {
    return `status-${status.toLowerCase()}`;
  }

  supplyIcon(name: string) {
    const value = name.toLowerCase();
    if (value.includes('sensor')) return '◉';
    if (value.includes('needle') || value.includes('lancet')) return '✦';
    if (value.includes('strip')) return '▤';
    if (value.includes('wipe') || value.includes('patch')) return '◇';
    return '□';
  }

  private resetForm() {
    this.editingId = '';
    this.saveError = '';
    this.form.reset({ name: '', currentQuantity: 0, unit: 'pcs', dailyUsage: 0, lowStockThresholdDays: 10 });
  }
}
