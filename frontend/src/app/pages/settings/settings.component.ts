import { DatePipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { DiabetesSettings } from '../../core/models';
import { DisclaimerComponent } from '../../shared/disclaimer.component';
import { getApiErrorMessage } from '../../core/api-error';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, DisclaimerComponent],
  template: `
    <section class="page-title">
      <div>
        <h1>Settings</h1>
        <p>Your diabetes settings.</p>
      </div>
    </section>
    <app-disclaimer />

    <section class="card">
      @if (loading) { <p role="status">Loading settings...</p> }
      @if (error) { <p role="alert">{{ error }}</p> }
      <form [formGroup]="form" class="grid two" (ngSubmit)="save()">
        <label>Target glucose
          <input type="number" min="0.1" step="0.1" formControlName="targetGlucose">
        </label>
        <label>Carb ratio
          <input type="number" min="0.1" step="0.1" formControlName="carbRatio">
        </label>
        <label>Correction factor
          <input type="number" min="0.1" step="0.1" formControlName="correctionFactor">
        </label>
        <label>Insulin duration hours
          <input type="number" min="0.1" step="0.1" formControlName="insulinDurationHours">
        </label>
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || loading || saving || !settings">Save settings</button>
          @if (settings) { <span class="muted">Updated {{ settings.updatedAt | date:'medium' }}</span> }
        </div>
      </form>
    </section>
  `
})
export class SettingsComponent implements OnInit {
  settings?: DiabetesSettings;
  loading = false;
  saving = false;
  error = '';
  form = this.fb.nonNullable.group({
    targetGlucose: [6.5, [Validators.required, Validators.min(0.1)]],
    carbRatio: [10, [Validators.required, Validators.min(0.1)]],
    correctionFactor: [3, [Validators.required, Validators.min(0.1)]],
    insulinDurationHours: [4, [Validators.required, Validators.min(0.1)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit() {
    this.loading = true;
    this.api.getSettings().pipe(finalize(() => this.loading = false)).subscribe({
      next: settings => {
        this.settings = settings;
        this.form.patchValue(settings);
      },
      error: error => this.error = getApiErrorMessage(error, 'Could not load settings. Please try again.')
    });
  }

  save() {
    if (this.form.invalid || this.loading || this.saving || !this.settings) return;
    this.saving = true;
    this.error = '';
    this.api.updateSettings(this.form.getRawValue()).pipe(finalize(() => this.saving = false)).subscribe({
      next: settings => this.settings = settings,
      error: error => this.error = getApiErrorMessage(error, 'Could not save settings. Please try again.')
    });
  }
}
