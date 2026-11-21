import { FormBuilder, Validators } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { MealDetail } from '../../core/models';
import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { FoodItem } from '../../core/models';
import { CalculatorComponent } from './calculator.component';

describe('CalculatorComponent', () => {
  it('hydrates structured saved order items, scales through the backend and ignores historical insulin/glucose', () => {
    const { component, api } = createComponent();
    const previous = history.state;
    try {
      history.replaceState({ deliveryMeal: { id: 'order', placeName: 'Le Grill', dishName: 'Set', carbs: 75,
        usualInsulinUnits: 99, lastPreMealGlucose: 15, items: [{ foodItemId: '00000000-0000-0000-0000-000000000000',
          foodName: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15, sourceDeliveryMealItemId: 'sushi-item' }] } }, '');
      component.ngOnInit();
      expect(component.form.controls.preMealGlucose.value).toBe(5.5);
      expect(component.form.controls.confirmedBolus.value).toBeNull();
      component.items.at(0).patchValue({ quantity: 3 });
      expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ preMealGlucose: 5.5, items: [jasmine.objectContaining({ sourceDeliveryMealItemId: 'sushi-item', quantity: 3 })] }));
    } finally { history.replaceState(previous, ''); }
  });
  it('carries the historical identity while keeping provenance and verification server controlled', () => {
    const { component, api } = createComponent();
    const previous = history.state;
    try {
      history.replaceState({ assistantMealItems: [{ foodItemId: 'food', sourceMealItemId: 'original-item',
        quantity: 150, measurementType: 'Grams', foodNameSnapshot: 'Soup', carbsPer100gSnapshot: 10,
        carbOverride: 18, provenance: { referenceSource: 'Manual', transformation: 'HistoricalReuse' } }],
        assistantOriginalMealId: 'original' }, '');
      component.ngOnInit();
      expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ items: [jasmine.objectContaining({
        sourceMealItemId: 'original-item', quantity: 150, carbOverride: 18, clearCarbOverride: false
      })] }));
      component.items.at(0).patchValue({ carbOverride: null });
      expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ items: [jasmine.objectContaining({
        sourceMealItemId: 'original-item', clearCarbOverride: true
      })] }));
    } finally { history.replaceState(previous, ''); }
  });
  function createComponent(auth?: AuthService) {
    const api = {
      getFoods: () => of([]),
      previewMealCarbs: jasmine.createSpy('previewMealCarbs').and.returnValue(of({ foodCarbs: 59.4, totalCarbs: 59.4, items: [] })),
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

    return { component: new CalculatorComponent(new FormBuilder(), api, router as never, auth), api };
  }

  it('loads the assistant draft with editable portions and calculates with the default without saving it', () => {
    const { component, api } = createComponent();
    const previous = history.state;
    try {
      history.replaceState({ assistantMealItems: [
        { foodItemId: 'porridge', quantity: 180, measurementType: 'Grams', foodNameSnapshot: '[Meal Assistant] Porridge', carbsPer100gSnapshot: 23 },
        { foodItemId: 'borscht', quantity: 1, measurementType: 'Portion', foodNameSnapshot: 'Borscht', carbsPerUnitSnapshot: 20, carbOverride: 18 }
      ] }, '');
      component.ngOnInit();
      expect(component.items.length).toBe(2);
      expect(component.form.controls.preMealGlucose.invalid).toBeFalse();
      expect(component.form.controls.preMealGlucose.value).toBe(5.5);
      expect(component.glucoseIsDefault).toBeTrue();
      component.save();
      expect(api.createMeal).not.toHaveBeenCalled();
      expect(component.itemName(0)).toBe('Porridge');
      expect(component.calculation).toBeDefined();
      expect(component.glucoseDefaultHint).toContain('5.5 mmol/L');
      expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ preMealGlucose: 5.5 }));
      component.items.at(0).patchValue({ quantity: 200 });
      component.form.controls.preMealGlucose.setValue(6.8);
      component.recordEnteredGlucose();
      expect(component.glucoseIsDefault).toBeFalse();
      expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({
        preMealGlucose: 6.8, items: [
          jasmine.objectContaining({ foodItemId: 'porridge', quantity: 200 }),
          jasmine.objectContaining({ foodItemId: 'borscht', quantity: 1, carbOverride: 18 })
        ]
      }));
      expect(api.createMeal).not.toHaveBeenCalled();
    } finally {
      history.replaceState(previous, '');
    }
  });

  it('prefers entered calculator glucose over explicit assistant glucose and ignores historical glucose', () => {
    const previous = history.state;
    const key = 'insulin-and-coffee.calculator-glucose.test-user';
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['user']);
    auth.user.and.returnValue({ id: 'test-user', username: 'test', role: 'User', createdAt: '' });
    try {
      const state = { assistantMealItems: [{ foodItemId: 'bread', quantity: 1, measurementType: 'Piece',
        foodNameSnapshot: 'Bread', carbsPerUnitSnapshot: 15 }], assistantCurrentGlucose: 7.2, preMealGlucose: 11 };
      history.replaceState(state, '');
      const first = createComponent(auth).component;
      first.ngOnInit();
      expect(first.form.controls.preMealGlucose.value).toBe(7.2);
      first.form.controls.preMealGlucose.setValue(6.8);
      first.recordEnteredGlucose();
      const reopened = createComponent(auth).component;
      reopened.ngOnInit();
      expect(reopened.form.controls.preMealGlucose.value).toBe(6.8);
      sessionStorage.removeItem(key);
      history.replaceState({ ...state, assistantCurrentGlucose: undefined }, '');
      const historical = createComponent(auth).component;
      historical.ngOnInit();
      expect(historical.form.controls.preMealGlucose.value).toBe(5.5);
      expect(historical.glucoseIsDefault).toBeTrue();
    } finally {
      sessionStorage.removeItem(key);
      history.replaceState(previous, '');
    }
  });

  describe('historical repeat hydration', () => {
    const snapshot = {
      id: 'item', foodItemId: 'deleted-food', foodNameSnapshot: 'Original soup', quantity: 150,
      measurementType: 'Grams' as const, weightGrams: 150, carbsPer100gSnapshot: 10,
      carbsPerUnitSnapshot: null, calculatedCarbs: 15, carbOverride: 18, effectiveCarbs: 18
    };
    const meal: MealDetail = {
      id: 'original', mealType: 'Lunch', mealTime: '2026-01-01T12:00:00Z', createdAt: '',
      preMealGlucose: 11, suggestedBolus: 7, confirmedBolus: 6, carbAdjustment: -3,
      totalCarbs: 15, foodNames: ['Original soup'], items: [snapshot]
    };

    for (const entry of ['assistant', 'previousMeal', 'delivery']) {
      it('uses fresh glucose and leaves insulin empty from ' + entry, async () => {
        const previous = history.state;
        const api = jasmine.createSpyObj<ApiService>('ApiService', ['getFoods', 'calculateMeal', 'createMeal']);
        api.getFoods.and.returnValue(of([]));
        api.calculateMeal.and.returnValue(of({
          foodCarbs: 18, carbAdjustment: -3, totalCarbs: 15, mealBolus: 1.5, correctionBolus: 0,
          suggestedBolus: 1.5, items: []
        }));
        const auth = jasmine.createSpyObj<AuthService>('AuthService', ['user']);
        auth.user.and.returnValue(null);
        await TestBed.configureTestingModule({
          imports: [CalculatorComponent],
          providers: [
            { provide: ApiService, useValue: api },
            { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigate']) },
            { provide: AuthService, useValue: auth }
          ]
        }).compileComponents();
        try {
          if (entry === 'assistant') {
            history.replaceState({ assistantMealItems: meal.items, assistantCarbAdjustment: meal.carbAdjustment,
              assistantOriginalMealId: meal.id }, '');
          } else if (entry === 'previousMeal') {
            history.replaceState({ meal }, '');
          } else {
            history.replaceState({ deliveryMeal: { carbs: 15, placeName: 'Cafe', dishName: 'Soup',
              usualInsulinUnits: 6, lastPreMealGlucose: 11, notes: 'Saved order' } }, '');
          }
          const fixture = TestBed.createComponent(CalculatorComponent);
          fixture.detectChanges();
          const component = fixture.componentInstance;
          expect(component.form.controls.preMealGlucose.value).toBe(5.5);
          expect(component.glucoseIsDefault).toBeTrue();
          expect(component.form.controls.confirmedBolus.value).toBeNull();
          expect(api.calculateMeal).toHaveBeenCalledTimes(1);
          expect(api.createMeal).not.toHaveBeenCalled();

          if (entry !== 'delivery') {
            expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({
              preMealGlucose: 5.5, carbAdjustment: -3,
              items: [jasmine.objectContaining({
                foodItemId: 'deleted-food', foodNameSnapshot: 'Original soup', quantity: 150,
                measurementType: 'Grams', carbsPer100gSnapshot: 10, carbOverride: 18
              })]
            }));
            const page: HTMLElement = fixture.nativeElement;
            expect(page.textContent).toContain('includes a meal-level carbohydrate adjustment (-3 g)');
            expect(page.querySelector('[aria-label="Override carbs"]')).not.toBeNull();
            component.items.at(0).patchValue({ quantity: 200 });
            fixture.detectChanges();
            expect(component.form.controls.carbAdjustment.value).toBe(-3);
            expect(component.items.at(0).value.carbOverride).toBe(18);
            expect(page.textContent).toContain('review and edit Adjust carbs');
            component.adjustCarbAdjustment(1);
            expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ carbAdjustment: -2 }));
            api.calculateMeal.and.returnValue(throwError(() => ({
              isApiError: true, status: 400, message: 'Saved carbs per 100 g must be zero or greater.'
            })));
            component.items.at(0).patchValue({ quantity: 250 });
            fixture.detectChanges();
            expect(page.querySelector('[role="alert"]')?.textContent).toContain('Saved carbs per 100 g');
            expect(component.calculation).toBeUndefined();
            expect(page.querySelector('[aria-label="Manual carb adjustment delta"]')).not.toBeNull();
          }
          fixture.destroy();
        } finally {
          history.replaceState(previous, '');
          TestBed.resetTestingModule();
        }
      });
    }
  });

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


