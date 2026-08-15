import { FormBuilder, Validators } from '@angular/forms';
import { of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { FoodItem } from '../../core/models';
import { CalculatorComponent } from './calculator.component';

describe('CalculatorComponent', () => {
  function createComponent() {
    const api = {
      getFoods: () => of([]),
      calculateMeal: jasmine.createSpy('calculateMeal').and.returnValue(of({
        foodCarbs: 0,
        carbAdjustment: 0,
        totalCarbs: 0,
        mealBolus: 0,
        correctionBolus: 0,
        suggestedBolus: 0,
        items: []
      })),
      createMeal: jasmine.createSpy('createMeal').and.returnValue(of({ id: 'meal-id' })),
      createFood: jasmine.createSpy('createFood').and.returnValue(of(foodItem()))
    } as unknown as ApiService;
    const router = { navigate: jasmine.createSpy('navigate') };

    return { component: new CalculatorComponent(new FormBuilder(), api, router as never), api };
  }

  it('quantitySuffix_ForSingularAndPluralQuantities_ReturnsNaturalCopy', () => {
    const { component } = createComponent();

    expect(component.quantitySuffix('Portion', 1)).toBe('portion');
    expect(component.quantitySuffix('Portion', 1.5)).toBe('portions');
    expect(component.quantitySuffix('Piece', 1)).toBe('piece');
    expect(component.quantitySuffix('Piece', 2)).toBe('pieces');
    expect(component.quantitySuffix('Grams', 150)).toBe('g');
  });

  it('selectedMeasurementType_WhenHistoricalSnapshotExists_UsesSnapshotInsteadOfCurrentFood', () => {
    const { component } = createComponent();
    component.foods = [foodItem({ id: 'soup-id', measurementType: 'Portion', carbsPer100g: null, carbsPerUnit: 20 })];
    component.items.clear();
    component.items.push(new FormBuilder().group({
      foodItemId: new FormBuilder().nonNullable.control('soup-id', Validators.required),
      quantity: new FormBuilder().nonNullable.control(300, [Validators.required, Validators.min(0.1)]),
      measurementType: new FormBuilder().control<'Grams'>('Grams'),
      foodNameSnapshot: new FormBuilder().control<string | null>('Soup'),
      carbsPer100gSnapshot: new FormBuilder().control<number | null>(5),
      carbsPerUnitSnapshot: new FormBuilder().control<number | null>(null),
      carbOverride: new FormBuilder().control<number | null>(null)
    }));

    expect(component.selectedMeasurementType(0)).toBe('Grams');
  });

  it('calculate_WhenHistoricalSnapshotExists_SendsSnapshotFieldsForReuse', () => {
    const { component, api } = createComponent();
    component.foods = [foodItem({ id: 'soup-id', measurementType: 'Portion', carbsPer100g: null, carbsPerUnit: 20 })];
    component.items.clear();
    component.items.push(new FormBuilder().group({
      foodItemId: new FormBuilder().nonNullable.control('soup-id', Validators.required),
      quantity: new FormBuilder().nonNullable.control(300, [Validators.required, Validators.min(0.1)]),
      measurementType: new FormBuilder().control<'Grams'>('Grams'),
      foodNameSnapshot: new FormBuilder().control<string | null>('Soup'),
      carbsPer100gSnapshot: new FormBuilder().control<number | null>(5),
      carbsPerUnitSnapshot: new FormBuilder().control<number | null>(null),
      carbOverride: new FormBuilder().control<number | null>(null)
    }));

    component.calculate();

    expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({
      items: [
        jasmine.objectContaining({
          foodItemId: 'soup-id',
          quantity: 300,
          measurementType: 'Grams',
          foodNameSnapshot: 'Soup',
          carbsPer100gSnapshot: 5,
          carbsPerUnitSnapshot: null,
          carbOverride: null
        })
      ]
    }));
  });

  it('onFoodQueryChange_WhenUserSelectsCurrentFood_ClearsHistoricalSnapshotFields', () => {
    const { component } = createComponent();
    component.foods = [foodItem({ id: 'soup-id', name: 'Soup', measurementType: 'Portion', carbsPer100g: null, carbsPerUnit: 20 })];
    component.items.at(0).patchValue({
      measurementType: 'Grams',
      foodNameSnapshot: 'Old Soup',
      carbsPer100gSnapshot: 5,
      carbsPerUnitSnapshot: null,
      carbOverride: 12
    });

    component.onFoodQueryChange(0, 'Soup');

    expect(component.items.at(0).value).toEqual(jasmine.objectContaining({
      foodItemId: 'soup-id',
      quantity: 1,
      measurementType: null,
      foodNameSnapshot: null,
      carbsPer100gSnapshot: null,
      carbsPerUnitSnapshot: null,
      carbOverride: null
    }));
  });

  it('nutritionBasisLabel_ForPiece_ReturnsPerPieceCopy', () => {
    const { component } = createComponent();

    expect(component.nutritionBasisLabel('Piece')).toBe('per piece');
  });


  it('calculate_WhenCarbsAreAdjusted_SendsMealAdjustmentAndItemOverride', () => {
    const { component, api } = createComponent();
    component.foods = [foodItem({ id: 'soup-id', name: 'Soup' })];
    component.items.at(0).patchValue({ foodItemId: 'soup-id', quantity: 100, carbOverride: 12 });
    component.form.controls.carbAdjustment.setValue(10);

    component.calculate();

    expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({
      carbAdjustment: 10,
      items: [jasmine.objectContaining({ carbOverride: 12 })]
    }));
  });
  function foodItem(overrides: Partial<FoodItem> = {}): FoodItem {
    return {
      id: 'food-id',
      name: 'Soup',
      measurementType: 'Grams',
      carbsPer100g: 5,
      carbsPerUnit: null,
      proteinPer100g: 4,
      fatPer100g: 2,
      caloriesPer100g: 60,
      isFavorite: false,
      createdAt: '2026-07-12T09:00:00Z',
      ...overrides
    };
  }
});


