import { DatePipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { DiabetesSettings } from '../../core/models';
import { DisclaimerComponent } from '../../shared/disclaimer.component';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, DisclaimerComponent],
  template: `
    <section class="page-title">
      <div>
        <h1>Settings</h1>
        <p>Current diabetes settings for the default user, Aleksandra.</p>
      </div>
    </section>
    <app-disclaimer />

    <section class="card">
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
          <button type="submit" [disabled]="form.invalid">Save settings</button>
          @if (settings) { <span class="muted">Updated {{ settings.updatedAt | date:'medium' }}</span> }
        </div>
      </form>
    </section>
  `
})
export class SettingsComponent implements OnInit {
  settings?: DiabetesSettings;
  form = this.fb.nonNullable.group({
    targetGlucose: [6.5, [Validators.required, Validators.min(0.1)]],
    carbRatio: [10, [Validators.required, Validators.min(0.1)]],
    correctionFactor: [3, [Validators.required, Validators.min(0.1)]],
    insulinDurationHours: [4, [Validators.required, Validators.min(0.1)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit() {
    this.api.getSettings().subscribe((settings) => {
      this.settings = settings;
      this.form.patchValue(settings);
    });
  }

  save() {
    if (this.form.invalid) return;
    this.api.updateSettings(this.form.getRawValue()).subscribe((settings) => this.settings = settings);
  }
}
