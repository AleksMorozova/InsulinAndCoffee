import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { CalculatedMealItem } from '../../core/models';
import { CalculatorComponent } from './calculator.component';
import { inResponsiveFrame } from '../../testing/responsive-frame';

describe('Calculator item layout', () => {
  let fixture: ComponentFixture<CalculatorComponent>;
  let api: jasmine.SpyObj<ApiService>;
  let originalState: unknown;
  const row: CalculatedMealItem = { foodItemId: '00000000-0000-0000-0000-000000000000', foodName: 'Суши', quantity: 5,
    measurementType: 'Portion', weightGrams: null, carbsPer100g: null, carbsPerUnit: 15, calculatedCarbs: 75, carbOverride: 75,
    effectiveCarbs: 75, sourceDeliveryMealItemId: 'sushi-item', provenance: { referenceSource: 'Manual', transformation: 'Calculated, HistoricalReuse', sourceMealVerifiedAt: '2026-11-21T10:00:00Z' } };

  beforeEach(async () => {
    originalState = history.state;
    history.replaceState({ assistantMealItems: [{ foodItemId: row.foodItemId, foodNameSnapshot: row.foodName, quantity: 5,
      measurementType: 'Portion', carbsPerUnitSnapshot: 15, carbOverride: 75, sourceDeliveryMealItemId: 'sushi-item' }] }, '');
    api = jasmine.createSpyObj<ApiService>('ApiService', ['getFoods', 'calculateMeal', 'createMeal']);
    api.getFoods.and.returnValue(of([]));
    api.calculateMeal.and.returnValue(of({ foodCarbs: 75, carbAdjustment: 0, totalCarbs: 75, mealBolus: 7.5, correctionBolus: 0, suggestedBolus: 7.5, items: [row] }));
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['user']); auth.user.and.returnValue(null);
    await TestBed.configureTestingModule({ imports: [CalculatorComponent], providers: [provideRouter([]),
      { provide: ApiService, useValue: api }, { provide: AuthService, useValue: auth }] }).compileComponents();
    fixture = TestBed.createComponent(CalculatorComponent); fixture.detectChanges();
  });
  afterEach(() => history.replaceState(originalState, ''));

  it('keeps a single selected item at five portions and 75g with its explicit override intact', () => {
    expect(fixture.componentInstance.items.length).toBe(1);
    expect(fixture.componentInstance.items.at(0).value.quantity).toBe(5);
    expect(fixture.componentInstance.items.at(0).value.carbOverride).toBe(75);
    expect(fixture.componentInstance.calculation?.totalCarbs).toBe(75);
    expect(api.calculateMeal).toHaveBeenCalledWith(jasmine.objectContaining({ items: [jasmine.objectContaining({ quantity: 5, sourceDeliveryMealItemId: 'sushi-item', carbOverride: 75 })] }));
    expect(fixture.nativeElement.querySelector('.saved-override-toggle')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.meal-item-carbs app-carb-provenance')).toBeNull();
    const badge: HTMLElement | null = fixture.nativeElement.querySelector('.compact-source');
    expect(badge?.textContent?.trim()).toBe('From history');
    expect(badge?.title).toContain('Override entered by you');
  });

  it('opens the equal override editor without changing or clearing its saved value', () => {
    const toggle: HTMLButtonElement | null = fixture.nativeElement.querySelector('.saved-override-toggle');
    toggle?.click(); fixture.detectChanges();
    const input: HTMLInputElement | null = fixture.nativeElement.querySelector('.meal-item-card input[formControlName="carbOverride"]');
    expect(input?.value).toBe('75');
    expect(fixture.componentInstance.items.at(0).value.carbOverride).toBe(75);
    expect(api.createMeal).not.toHaveBeenCalled();
  });

  it('does not collapse an override input while the user is editing an equal value', () => {
    fixture.componentInstance.items.at(0).patchValue({ carbOverride: null }); fixture.detectChanges();
    const input: HTMLInputElement | null = fixture.nativeElement.querySelector('.meal-item-card input[formControlName="carbOverride"]');
    if (!input) throw new Error('Expected override editor');
    input.focus();
    // Background headless documents may update activeElement without emitting focus.
    input.dispatchEvent(new FocusEvent('focus'));
    expect(document.activeElement).toBe(input);
    fixture.componentInstance.items.at(0).patchValue({ carbOverride: 75 }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.saved-override-toggle')).toBeNull();
    expect(fixture.nativeElement.querySelector('.meal-item-card input[formControlName="carbOverride"]')).not.toBeNull();
    expect(fixture.componentInstance.items.at(0).value.carbOverride).toBe(75);
  });

  it('distinguishes a changed override from calculated carbs', () => {
    api.calculateMeal.and.returnValue(of({ foodCarbs: 75, carbAdjustment: 0, totalCarbs: 75, mealBolus: 7.5, correctionBolus: 0, suggestedBolus: 7.5,
      items: [{ ...row, quantity: 3, calculatedCarbs: 45 }] }));
    fixture.componentInstance.items.at(0).patchValue({ quantity: 3 }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.meal-item-carbs').textContent).toContain('45 g');
    expect(fixture.nativeElement.querySelector('.meal-item-carbs').textContent).toContain('Calculated');
    expect(fixture.componentInstance.items.at(0).value.carbOverride).toBe(75);
  });

  it('keeps numbers and provenance readable at desktop, tablet and mobile content widths', () => {
    const list: HTMLElement | null = fixture.nativeElement.querySelector('.current-meal-list');
    const card: HTMLElement | null = fixture.nativeElement.querySelector('.meal-item-card');
    const carbs: HTMLElement | null = fixture.nativeElement.querySelector('.meal-item-carbs');
    const source: HTMLElement | null = fixture.nativeElement.querySelector('.compact-source');
    if (!list || !card || !carbs || !source) throw new Error('Expected complete item layout');
    expect(card.getBoundingClientRect().right).toBeLessThanOrEqual(window.innerWidth + 1);
    for (const width of [760, 540, 340]) {
      list.style.width = `${width}px`;
      expect(card.scrollWidth).toBeLessThanOrEqual(card.clientWidth + 1);
      expect(carbs.getBoundingClientRect().width).toBeGreaterThanOrEqual(60);
      expect(source.getBoundingClientRect().height).toBeLessThan(25);
      expect(getComputedStyle(source).whiteSpace).toBe('nowrap');
      const name = card.querySelector('.meal-item-main')?.getBoundingClientRect();
      const amount = card.querySelector('.meal-weight')?.getBoundingClientRect();
      if (width > 600) expect(name?.top).toBeLessThan((amount?.bottom ?? 0));
      else expect(amount?.top).toBeGreaterThan(name?.top ?? 0);
    }
  });

  it('fits the real desktop, tablet and phone viewport without crushing provenance or carbs', async () => {
    await inResponsiveFrame(fixture.nativeElement, viewport => {
      const card: HTMLElement | null = fixture.nativeElement.querySelector('.meal-item-card');
      const source: HTMLElement | null = fixture.nativeElement.querySelector('.compact-source');
      const carbs: HTMLElement | null = fixture.nativeElement.querySelector('.meal-item-carbs');
      if (!card || !source || !carbs) throw new Error('Expected transferred item');
      expect(card.scrollWidth).toBeLessThanOrEqual(card.clientWidth + 1);
      expect(card.getBoundingClientRect().right).toBeLessThanOrEqual(viewport.innerWidth + 1);
      expect(source.getBoundingClientRect().height).toBeLessThan(25);
      expect(carbs.getBoundingClientRect().width).toBeGreaterThanOrEqual(60);
      expect(viewport.getComputedStyle(source).whiteSpace).toBe('nowrap');
    });
  });
});
