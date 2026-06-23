import { DecimalPipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService, UpsertFoodRequest } from '../../core/api.service';
import { FoodItem } from '../../core/models';

@Component({
  selector: 'app-foods',
  standalone: true,
  imports: [DecimalPipe, ReactiveFormsModule],
  template: `
    <section class="page-title">
      <div>
        <h1>Food Library</h1>
        <p>Per-100g nutrition values used by the calculator.</p>
      </div>
    </section>

    <section class="grid two">
      <article class="card">
        <h2>{{ editingId ? 'Edit food' : 'Add food' }}</h2>
        <form [formGroup]="form" class="grid" (ngSubmit)="save()">
          <label>Name <input formControlName="name"></label>
          <div class="grid two">
            <label>Carbs / 100g <input type="number" min="0" step="0.1" formControlName="carbsPer100g"></label>
            <label>Protein / 100g <input type="number" min="0" step="0.1" formControlName="proteinPer100g"></label>
            <label>Fat / 100g <input type="number" min="0" step="0.1" formControlName="fatPer100g"></label>
            <label>Calories / 100g <input type="number" min="0" step="1" formControlName="caloriesPer100g"></label>
          </div>
          <label class="toolbar">
            <input style="width:auto" type="checkbox" formControlName="isFavorite">
            Favorite
          </label>
          <div class="actions">
            <button type="submit" [disabled]="form.invalid">{{ editingId ? 'Update' : 'Add' }}</button>
            <button type="button" class="subtle" (click)="reset()">Clear</button>
          </div>
        </form>
      </article>

      <article class="card grid">
        <label>Search foods
          <input [value]="search" (input)="search = $any($event.target).value; loadFoods()" placeholder="Search library">
        </label>
        <table class="table">
          <thead><tr><th>Name</th><th>Carbs</th><th>Favorite</th><th></th></tr></thead>
          <tbody>
            @for (food of foods; track food.id) {
              <tr>
                <td>{{ food.name }}</td>
                <td>{{ food.carbsPer100g | number:'1.0-1' }} g</td>
                <td>{{ food.isFavorite ? 'Yes' : 'No' }}</td>
                <td class="actions">
                  <button type="button" class="subtle" (click)="edit(food)">Edit</button>
                  <button type="button" class="danger" (click)="delete(food.id)">Delete</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </article>
    </section>
  `
})
export class FoodsComponent implements OnInit {
  foods: FoodItem[] = [];
  search = '';
  editingId = '';
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

  edit(food: FoodItem) {
    this.editingId = food.id;
    this.form.patchValue(food);
  }

  save() {
    if (this.form.invalid) return;
    const request = this.form.getRawValue() as UpsertFoodRequest;
    const save$ = this.editingId ? this.api.updateFood(this.editingId, request) : this.api.createFood(request);
    save$.subscribe(() => {
      this.reset();
      this.loadFoods();
    });
  }

  delete(id: string) {
    this.api.deleteFood(id).subscribe(() => this.loadFoods());
  }

  reset() {
    this.editingId = '';
    this.form.reset({ name: '', carbsPer100g: 0, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false });
  }
}
