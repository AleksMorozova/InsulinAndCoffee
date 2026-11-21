import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CalculatedMealItem, DeliveryMeal, DeliveryMealItem, DeliveryMealPreview } from '../../core/models';
import { DeliveryMealsComponent } from './delivery-meals.component';

describe('Structured Ask Past Me orders', () => {
  let fixture: ComponentFixture<DeliveryMealsComponent>;
  let api: jasmine.SpyObj<ApiService>;
  const sushi: DeliveryMealItem = { id: 'sushi-item', name: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15,
    carbOverride: null, calculatedCarbs: 75, effectiveCarbs: 75, displayOrder: 0, provenance: { referenceSource: 'Manual', transformation: 'Calculated' } };
  const order: DeliveryMeal = { id: 'order', placeName: 'Le Grill', dishName: 'Sushi order', portionDescription: 'Original note',
    carbs: 75, usualInsulinUnits: 99, lastPreMealGlucose: 15, resultRating: 'Unknown', tags: '', isFavorite: false,
    usageCount: 0, createdAt: '', revision: 4, items: [sushi] };
  const calculated: CalculatedMealItem = { foodItemId: '00000000-0000-0000-0000-000000000000', foodName: 'Sushi',
    quantity: 5, measurementType: 'Piece', weightGrams: null, carbsPer100g: null, carbsPerUnit: 15,
    calculatedCarbs: 75, carbOverride: null, effectiveCarbs: 75, sourceDeliveryMealItemId: 'sushi-item' };
  const preview: DeliveryMealPreview = { items: [calculated], totalCarbs: 75, difference: 0 };

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['getDeliveryMeals', 'previewDeliveryMealItems', 'createDeliveryMeal',
      'updateDeliveryMeal', 'reuseDeliveryMealItems', 'createMealDraftFromDeliveryMeal']);
    api.getDeliveryMeals.and.returnValue(of({ favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] }));
    api.previewDeliveryMealItems.and.returnValue(of(preview));
    api.createDeliveryMeal.and.returnValue(of(order));
    api.updateDeliveryMeal.and.returnValue(of(order));
    api.reuseDeliveryMealItems.and.returnValue(of([calculated]));
    await TestBed.configureTestingModule({ imports: [DeliveryMealsComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] }).compileComponents();
    fixture = TestBed.createComponent(DeliveryMealsComponent); fixture.detectChanges();
  });

  it('creates item rows, shows backend totals and saves structured data', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.openCreate(); component.enableItems();
    component.form.patchValue({ placeName: 'Le Grill', dishName: 'Sushi order', portionDescription: 'Five pieces' });
    component.editorItems.at(0).patchValue({ name: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15 });
    tick(151); fixture.detectChanges();
    expect(api.previewDeliveryMealItems).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Item total:');
    expect(component.form.controls.carbs.value).toBe(75);
    component.save();
    expect(api.createDeliveryMeal).toHaveBeenCalledWith(jasmine.objectContaining({ carbs: 75,
      items: [jasmine.objectContaining({ name: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15 })] }));
    expect(component.isEditorOpen).toBeFalse();
  }));

  it('preserves IDs and requires explicit acceptance of a changed saved total', fakeAsync(() => {
    api.previewDeliveryMealItems.and.returnValues(of(preview), of({ items: [{ ...calculated, quantity: 3, calculatedCarbs: 45, effectiveCarbs: 45 }], totalCarbs: 45, difference: -30 }));
    const component = fixture.componentInstance;
    component.openEdit(order);
    component.editorItems.at(0).patchValue({ quantity: 3 });
    expect(component.preview).toBeUndefined();
    tick(151); fixture.detectChanges();
    expect(component.form.controls.carbs.value).toBe(75);
    expect(fixture.nativeElement.textContent).toContain('Difference: -30');
    expect(component.acceptedTotal).toBeFalse();
    component.acceptTotal(); component.save();
    expect(api.updateDeliveryMeal).toHaveBeenCalledWith('order', jasmine.objectContaining({ carbs: 45, expectedRevision: 4,
      acceptCalculatedTotal: true, items: [jasmine.objectContaining({ id: 'sushi-item', quantity: 3 })] }));
  }));

  it('adds and removes multiple items without accepting an empty structured order', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.openCreate(); component.enableItems(); component.addOrderItem();
    expect(component.editorItems.length).toBe(2);
    component.removeItem(1); component.removeItem(0); tick(151);
    expect(component.preview).toBeUndefined();
    component.save(); expect(api.createDeliveryMeal).not.toHaveBeenCalled();
  }));

  it('keeps failed edits open with their input values', fakeAsync(() => {
    api.updateDeliveryMeal.and.returnValue(throwError(() => new Error('conflict')));
    const component = fixture.componentInstance;
    component.openEdit(order); component.save(); fixture.detectChanges();
    expect(component.isEditorOpen).toBeTrue();
    expect(component.editorItems.at(0).controls.id.value).toBe('sushi-item');
    expect(fixture.nativeElement.textContent).toContain('Could not save this order');
    tick(151);
  }));

  it('reuses one or multiple selected IDs through the backend and navigates without medical history', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const component = fixture.componentInstance;
    component.toggleItem(order.id, sushi.id);
    component.reuseItems(order, component.selectedItems[order.id] ?? []);
    expect(api.reuseDeliveryMealItems).toHaveBeenCalledOnceWith('order', ['sushi-item'], 4);
    expect(navigate).toHaveBeenCalledWith(['/calculator'], { state: { assistantMealItems: [jasmine.objectContaining({
      sourceDeliveryMealItemId: 'sushi-item', quantity: 5, carbsPerUnitSnapshot: 15 })] } });
    component.toggleItem(order.id, 'another-item');
    expect(component.selectedItems[order.id]).toEqual(['sushi-item', 'another-item']);
  });

  it('reuses a whole structured order as editable items and leaves legacy reuse intact', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    api.createMealDraftFromDeliveryMeal.and.returnValues(
      of({ id: 'order', carbs: 75, usualInsulinUnits: 99, notes: '', items: [calculated] }),
      of({ id: 'legacy', carbs: 135, usualInsulinUnits: null, notes: 'Original note' }));
    fixture.componentInstance.createMealDraft(order);
    expect(navigate).toHaveBeenCalledWith(['/calculator'], { state: { assistantMealItems: [jasmine.objectContaining({ sourceDeliveryMealItemId: 'sushi-item' })] } });
    fixture.componentInstance.createMealDraft({ ...order, id: 'legacy', items: [] });
    expect(navigate).toHaveBeenCalledWith(['/calculator'], jasmine.objectContaining({ state: jasmine.objectContaining({ deliveryMeal: jasmine.objectContaining({ id: 'legacy', carbs: 135 }) }) }));
  });

  it('wraps the item editor at phone content width without horizontal overflow', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.openCreate(); component.enableItems();
    component.editorItems.at(0).patchValue({ name: 'Long restaurant item name', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15 });
    tick(151); fixture.detectChanges();
    const editor: HTMLElement | null = fixture.nativeElement.querySelector('.order-items-editor');
    expect(editor).not.toBeNull();
    if (editor) {
      editor.style.width = '340px';
      expect(editor.scrollWidth).toBeLessThanOrEqual(editor.clientWidth + 1);
      expect(editor.querySelectorAll('input').length).toBeGreaterThanOrEqual(4);
    }
  }));
});
