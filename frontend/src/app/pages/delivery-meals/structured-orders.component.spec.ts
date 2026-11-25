import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CalculatedMealItem, DeliveryMeal, DeliveryMealItem, DeliveryMealPreview } from '../../core/models';
import { DeliveryMealsComponent } from './delivery-meals.component';
import { inResponsiveFrame } from '../../testing/responsive-frame';

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
      'updateDeliveryMeal', 'reuseDeliveryMealItems', 'createMealDraftFromDeliveryMeal', 'previewMealCarbs', 'toggleDeliveryMealFavorite']);
    api.getDeliveryMeals.and.returnValue(of({ favorites: [], mostUsed: [], recentlyUsed: [], searchResults: [] }));
    api.previewDeliveryMealItems.and.returnValue(of(preview));
    api.createDeliveryMeal.and.returnValue(of(order));
    api.updateDeliveryMeal.and.returnValue(of(order));
    api.reuseDeliveryMealItems.and.returnValue(of([calculated]));
    await TestBed.configureTestingModule({ imports: [DeliveryMealsComponent], providers: [provideRouter([]), { provide: ApiService, useValue: api }] }).compileComponents();
    fixture = TestBed.createComponent(DeliveryMealsComponent); fixture.detectChanges();
  });

  it('labels quantities by their saved unit and preserves confirmed carbs and historical values', () => {
    const component = fixture.componentInstance;
    component.openCreate(); fixture.detectChanges();
    expect(component.form.controls.lastPreMealGlucose.value).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Pre-meal glucose (mmol/L, optional)');
    component.openEdit(order); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Quantity (pieces)');
    expect(fixture.nativeElement.textContent).toContain('Carbs (g) per piece');
    component.save();
    expect(api.updateDeliveryMeal).toHaveBeenCalledWith(order.id, jasmine.objectContaining({
      carbs: 75, lastPreMealGlucose: 15, items: [jasmine.objectContaining({ quantity: 5, measurementType: 'Piece', carbsPerUnit: 15 })]
    }));
  });

  it('selects full rows, quotes saved totals and transfers only selected IDs with one primary action', () => {
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const multi: DeliveryMeal = { ...order, portionDescription: '', carbs: 135, items: [
      { ...sushi, id: 'california', name: 'California', quantity: 1, effectiveCarbs: 60, calculatedCarbs: 60 }, sushi] };
    fixture.componentInstance.sections.searchResults = [multi]; fixture.detectChanges();
    const card: HTMLElement = fixture.nativeElement.querySelector('.memory-card');
    expect(card.querySelector('.meal-note')).toBeNull();
    expect(card.querySelectorAll('.memory-reuse-button').length).toBe(1);
    const rows = card.querySelectorAll<HTMLLabelElement>('.memory-item-row');
    rows[1].click(); fixture.detectChanges();
    expect(fixture.componentInstance.selectedTotals[order.id]).toBe(75);
    expect(card.textContent).toContain('1 selected');
    expect(api.reuseDeliveryMealItems).not.toHaveBeenCalled();
    fixture.componentInstance.selectAll(multi); fixture.detectChanges();
    expect(fixture.componentInstance.selectedTotals[order.id]).toBe(135);
    rows[0].click(); fixture.detectChanges();
    card.querySelector<HTMLButtonElement>('.memory-reuse-button')?.click();
    expect(api.reuseDeliveryMealItems).toHaveBeenCalledOnceWith(order.id, ['sushi-item'], 4);
    expect(card.textContent).toContain('Previously recorded: 99 U');
    expect(card.textContent).not.toContain('Unknown');
  });

  it('requests a backend total for a partial multi-item selection without recording reuse', () => {
    api.previewMealCarbs.and.returnValue(of({ totalCarbs: 135, foodCarbs: 135, items: [] }));
    const multi: DeliveryMeal = { ...order, carbs: 145, items: [sushi,
      { ...sushi, id: 'california', effectiveCarbs: 60 }, { ...sushi, id: 'third', effectiveCarbs: 10 }] };
    const component = fixture.componentInstance;
    component.toggleItem(multi.id, sushi.id, multi);
    component.toggleItem(multi.id, 'california', multi);
    expect(api.previewMealCarbs).toHaveBeenCalledOnceWith([
      jasmine.objectContaining({ sourceDeliveryMealItemId: 'sushi-item', quantity: 5 }),
      jasmine.objectContaining({ sourceDeliveryMealItemId: 'california', quantity: 5 })], 0);
    expect(component.selectedTotals[multi.id]).toBe(135);
    expect(api.reuseDeliveryMealItems).not.toHaveBeenCalled();
  });

  it('keeps compact favorites separate from full results at desktop, tablet and phone widths', async () => {
    const long: DeliveryMeal = { ...order, dishName: 'Long order', items: [{ ...sushi, name: 'Очень длинное название ресторанного блюда для проверки переноса текста' }] };
    fixture.componentInstance.sections = { favorites: [order, { ...long, id: 'long-favorite' }], searchResults: [order, long], mostUsed: [], recentlyUsed: [] };
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.memory-card').length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.favorite-bookmark').length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.memory-item-row input').length).toBe(0);
    await inResponsiveFrame(fixture.nativeElement, viewport => {
      const grids: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.memory-card-grid');
      for (const grid of Array.from(grids)) {
        expect(grid.scrollWidth).toBeLessThanOrEqual(grid.clientWidth + 1);
        const columns = viewport.getComputedStyle(grid).gridTemplateColumns.split(' ').length;
        expect(columns).toBe(viewport.innerWidth <= 760 ? 1 : 2);
      }
      const cards: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.memory-card');
      for (const card of Array.from(cards)) expect(card.scrollWidth).toBeLessThanOrEqual(card.clientWidth + 1);
      const bookmarks: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.favorite-bookmark');
      for (const bookmark of Array.from(bookmarks)) {
        expect(bookmark.scrollWidth).toBeLessThanOrEqual(bookmark.clientWidth + 1);
        expect(bookmark.getBoundingClientRect().height).toBeLessThan(180);
      }
    });
  });

  it('shows only bookmark essentials, hides absent notes and removes favorites through the existing API', () => {
    fixture.componentInstance.sections.favorites = [{ ...order, portionDescription: '  ', isFavorite: true }];
    fixture.detectChanges();
    const bookmark: HTMLElement = fixture.nativeElement.querySelector('.favorite-bookmark');
    expect(bookmark.textContent).toContain('Le Grill');
    expect(bookmark.textContent).toContain('Sushi order');
    expect(bookmark.textContent).toContain('75 g carbs');
    expect(bookmark.querySelector('.favorite-portion')).toBeNull();
    expect(bookmark.querySelector('app-carb-provenance')).toBeNull();
    expect(bookmark.textContent).not.toContain('Previously recorded');
    expect(bookmark.textContent).not.toContain('Used');
    api.toggleDeliveryMealFavorite.and.returnValue(of(order));
    bookmark.querySelector<HTMLButtonElement>('.favorite-remove')?.click();
    expect(api.toggleDeliveryMealFavorite).toHaveBeenCalledOnceWith(order.id);
  });

  it('reuses every item of a favorite even when only one search-result item is selected', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const california: CalculatedMealItem = { ...calculated, foodName: 'California', quantity: 1, carbsPerUnit: 60,
      calculatedCarbs: 60, effectiveCarbs: 60, sourceDeliveryMealItemId: 'california' };
    const favorite: DeliveryMeal = { ...order, carbs: 135, items: [sushi, { ...sushi, id: 'california', quantity: 1 }] };
    fixture.componentInstance.sections.favorites = [favorite];
    fixture.componentInstance.selectedItems[order.id] = [sushi.id];
    api.createMealDraftFromDeliveryMeal.and.returnValue(of({ id: order.id, carbs: 135, usualInsulinUnits: null, notes: '', items: [calculated, california] }));
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.favorite-reuse').click();
    expect(api.createMealDraftFromDeliveryMeal).toHaveBeenCalledOnceWith(order.id);
    expect(api.reuseDeliveryMealItems).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/calculator'], { state: { assistantMealItems: [
      jasmine.objectContaining({ sourceDeliveryMealItemId: sushi.id }), jasmine.objectContaining({ sourceDeliveryMealItemId: 'california' })] } });
  });

  it('creates item rows, shows backend totals and saves without a portion note', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.openCreate(); component.enableItems();
    component.form.patchValue({ placeName: 'Le Grill', dishName: 'Sushi order', portionDescription: '   ' });
    component.editorItems.at(0).patchValue({ name: 'Sushi', quantity: 5, measurementType: 'Piece', carbsPerUnit: 15 });
    tick(151); fixture.detectChanges();
    expect(api.previewDeliveryMealItems).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Total effective carbs');
    expect(component.form.controls.carbs.value).toBe(75);
    component.save();
    expect(api.createDeliveryMeal).toHaveBeenCalledWith(jasmine.objectContaining({ carbs: 75, portionDescription: null,
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

  it('keeps equal explicit overrides while avoiding duplicate calculated/effective summaries', () => {
    api.previewDeliveryMealItems.and.returnValue(of({ ...preview, items: [{ ...calculated, carbOverride: 75 }] }));
    const component = fixture.componentInstance;
    component.openEdit({ ...order, items: [{ ...sushi, carbOverride: 75 }] }); fixture.detectChanges();
    const summary: HTMLElement | null = fixture.nativeElement.querySelector('.order-item-result');
    expect(summary?.textContent).toContain('75 g carbs');
    expect(summary?.textContent).toContain('Saved override');
    expect(summary?.textContent).not.toContain('Effective 75');
    expect(component.editorItems.at(0).controls.carbOverride.value).toBe(75);
    component.save();
    expect(api.updateDeliveryMeal).toHaveBeenCalledWith('order', jasmine.objectContaining({ items: [jasmine.objectContaining({ carbOverride: 75 })] }));
  });

  it('keeps its footer visible and scrolls internally at desktop, tablet and phone content widths', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.openCreate(); component.enableItems();
    for (let index = 0; index < 7; index++) component.addOrderItem();
    tick(151); fixture.detectChanges();
    const dialog: HTMLElement | null = fixture.nativeElement.querySelector('.delivery-modal');
    const content: HTMLElement | null = fixture.nativeElement.querySelector('.modal-content');
    const footer: HTMLElement | null = fixture.nativeElement.querySelector('.modal-actions');
    if (!dialog || !content || !footer) throw new Error('Expected complete modal');
    expect(dialog.getBoundingClientRect().width).toBeLessThanOrEqual(window.innerWidth + 1);
    dialog.style.maxHeight = '500px';
    for (const width of [940, 700, 350]) {
      dialog.style.width = `${width}px`;
      expect(content.scrollHeight).toBeGreaterThan(content.clientHeight);
      expect(content.scrollWidth).toBeLessThanOrEqual(content.clientWidth + 1);
      expect(footer.getBoundingClientRect().bottom).toBeLessThanOrEqual(dialog.getBoundingClientRect().bottom + 1);
      expect(dialog.getBoundingClientRect().height).toBeLessThanOrEqual(501);
    }
  }));

  it('supports keyboard focus wrapping, Escape, and return focus to the opener', () => {
    const opener: HTMLButtonElement | null = fixture.nativeElement.querySelector('.hero-add-button');
    if (!opener) throw new Error('Expected modal opener');
    opener.focus(); opener.click(); fixture.detectChanges();
    const close: HTMLButtonElement | null = fixture.nativeElement.querySelector('.dialog-close');
    const actions: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.modal-actions button');
    const cancel = actions[actions.length - 1];
    if (!close || !cancel) throw new Error('Expected modal actions');
    close.focus(); close.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(cancel);
    cancel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.isEditorOpen).toBeFalse();
    expect(document.activeElement).toBe(opener);
  });

  it('keeps the dialog and actions within desktop, tablet and phone viewports', async () => {
    fixture.componentInstance.openCreate(); fixture.componentInstance.enableItems();
    for (let index = 0; index < 7; index++) fixture.componentInstance.addOrderItem();
    fixture.detectChanges();
    await inResponsiveFrame(fixture.nativeElement, viewport => {
      const dialog: HTMLElement | null = fixture.nativeElement.querySelector('.delivery-modal');
      const content: HTMLElement | null = fixture.nativeElement.querySelector('.modal-content');
      const footer: HTMLElement | null = fixture.nativeElement.querySelector('.modal-actions');
      if (!dialog || !content || !footer) throw new Error('Expected modal');
      expect(dialog.getBoundingClientRect().width).toBeLessThanOrEqual(viewport.innerWidth);
      expect(dialog.getBoundingClientRect().bottom).toBeLessThanOrEqual(viewport.innerHeight);
      expect(content.scrollWidth).toBeLessThanOrEqual(content.clientWidth + 1);
      expect(content.scrollHeight).toBeGreaterThan(content.clientHeight);
      expect(footer.getBoundingClientRect().bottom).toBeLessThanOrEqual(dialog.getBoundingClientRect().bottom + 1);
    });
  });
});
