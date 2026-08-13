import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { FoodItem } from '../../core/models';
import { FoodsComponent } from './foods.component';

describe('FoodsComponent', () => {
  function createComponent() {
    const api = {
      getFoods: () => of([]),
      createFood: jasmine.createSpy('createFood').and.returnValue(of({})),
      updateFood: jasmine.createSpy('updateFood').and.returnValue(of({})),
      deleteFood: jasmine.createSpy('deleteFood').and.returnValue(of(undefined))
    } as unknown as ApiService;

    return { component: new FoodsComponent(new FormBuilder(), api), api };
  }

  it('nutritionBasisLabel_ForGrams_ReturnsPer100gCopy', () => {
    const { component } = createComponent();

    expect(component.nutritionBasisLabel('Grams')).toBe('per 100 g');
  });

  it('nutritionBasisLabel_ForPortion_ReturnsPerPortionCopy', () => {
    const { component } = createComponent();

    expect(component.nutritionBasisLabel('Portion')).toBe('per portion');
  });

  it('nutritionBasisLabel_ForPiece_ReturnsPerPieceCopy', () => {
    const { component } = createComponent();

    expect(component.nutritionBasisLabel('Piece')).toBe('per piece');
  });

  it('foodCarbBasisLabel_ForPortion_DoesNotUsePer100gCopy', () => {
    const { component } = createComponent();
    const food = foodItem({ measurementType: 'Portion', carbsPer100g: null, carbsPerUnit: 20 });

    expect(component.foodCarbBasisLabel(food)).toBe('carbs / portion');
  });

  it('save_WhenSwitchingFromGramsToPortion_SubmitsOnlyPortionCarbs', () => {
    const { component, api } = createComponent();
    component.openEdit(foodItem({ carbsPer100g: 12, carbsPerUnit: null }));
    component.setMeasurementType('Portion');
    component.form.patchValue({ carbsPer100g: 12, carbsPerUnit: 20 });

    component.save();

    expect(api.updateFood).toHaveBeenCalledWith(jasmine.any(String), jasmine.objectContaining({
      measurementType: 'Portion',
      carbsPer100g: null,
      carbsPerUnit: 20
    }));
  });

  it('onMenuToggle_WhenMenuOpens_TracksOnlyThatFoodMenu', () => {
    const { component } = createComponent();
    component.openMenuFoodId = 'first-food';

    component.onMenuToggle(menuToggleEvent(true), 'second-food');

    expect(component.openMenuFoodId).toBe('second-food');
  });

  it('onMenuToggle_WhenOpenMenuCloses_ClearsOpenMenu', () => {
    const { component } = createComponent();
    component.openMenuFoodId = 'food-id';

    component.onMenuToggle(menuToggleEvent(false), 'food-id');

    expect(component.openMenuFoodId).toBe('');
  });

  it('closeMenu_WhenDocumentIsClicked_ClearsOpenMenu', () => {
    const { component } = createComponent();
    component.openMenuFoodId = 'food-id';

    component.closeMenu();

    expect(component.openMenuFoodId).toBe('');
  });

  it('openEditFromMenu_ClosesMenuAndOpensExistingEditor', () => {
    const { component } = createComponent();
    component.openMenuFoodId = 'food-id';

    component.openEditFromMenu(foodItem());

    expect(component.openMenuFoodId).toBe('');
    expect(component.editingId).toBe('food-id');
    expect(component.isEditorOpen).toBeTrue();
  });

  it('deleteFromMenu_ClosesMenuAndRunsExistingDeleteFlow', () => {
    const { component, api } = createComponent();
    spyOn(window, 'confirm').and.returnValue(true);
    component.openMenuFoodId = 'food-id';

    component.deleteFromMenu('food-id');

    expect(component.openMenuFoodId).toBe('');
    expect(api.deleteFood).toHaveBeenCalledWith('food-id');
  });

  function menuToggleEvent(open: boolean): Event {
    return { currentTarget: { open } } as unknown as Event;
  }

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
