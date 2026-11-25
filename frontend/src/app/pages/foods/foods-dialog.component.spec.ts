import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { FoodsComponent } from './foods.component';

describe('Food editor dialog', () => {
  it('focuses the name, traps both Tab directions, contains external focus and restores the opener on Escape', async () => {
    await TestBed.configureTestingModule({ imports: [FoodsComponent], providers: [
      { provide: ApiService, useValue: { getFoods: () => of([]) } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(FoodsComponent); fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    const opener = host.querySelector<HTMLButtonElement>('.hero-add-button');
    if (!opener) throw new Error('Missing opener');
    opener.focus(); opener.click(); fixture.detectChanges(); await fixture.whenStable();
    const dialog = host.querySelector<HTMLElement>('[role="dialog"]');
    const name = dialog?.querySelector<HTMLInputElement>('input');
    const close = dialog?.querySelector<HTMLButtonElement>('button');
    const cancel = dialog?.querySelector<HTMLButtonElement>('.modal-actions button.subtle');
    if (!dialog || !name || !close || !cancel) throw new Error('Missing dialog controls');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(document.activeElement).toBe(name);
    close.focus(); close.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(cancel);
    cancel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(close);
    opener.focus(); expect(document.activeElement).toBe(name);
    name.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges(); await fixture.whenStable();
    expect(host.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it('restores the visible overflow trigger after cancelling Edit', async () => {
    await TestBed.configureTestingModule({ imports: [FoodsComponent], providers: [
      { provide: ApiService, useValue: { getFoods: () => of([{ id: 'food', name: 'Test food', measurementType: 'Piece',
        carbsPerUnit: 15, carbsPer100g: null, proteinPer100g: 0, fatPer100g: 0, caloriesPer100g: 0, isFavorite: false, createdAt: '' }]) } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(FoodsComponent); fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    const summary = host.querySelector<HTMLElement>('summary');
    if (!summary) throw new Error('Missing overflow trigger');
    summary.click(); fixture.detectChanges();
    const edit = host.querySelector<HTMLButtonElement>('.overflow-panel button');
    if (!edit) throw new Error('Missing Edit');
    edit.focus(); edit.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(host.querySelector('[role="dialog"]')?.getAttribute('aria-labelledby')).toBe('food-editor-title');
    host.querySelector<HTMLButtonElement>('.modal-actions button.subtle')?.click();
    fixture.detectChanges(); await fixture.whenStable();
    expect(document.activeElement).toBe(summary);
  });

  it('guards delayed saves and retains entered data on failure for retry', async () => {
    const pending = new Subject<unknown>();
    const createFood = jasmine.createSpy().and.returnValue(pending);
    await TestBed.configureTestingModule({ imports: [FoodsComponent], providers: [
      { provide: ApiService, useValue: { getFoods: () => of([]), createFood } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(FoodsComponent); fixture.detectChanges();
    const component = fixture.componentInstance;
    component.openCreate(); component.form.controls.name.setValue('Test food');
    component.save(); component.save(); fixture.detectChanges();
    expect(createFood).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.querySelector('button[type="submit"]').textContent).toContain('Saving');
    pending.error(new Error('Unavailable')); fixture.detectChanges();
    expect(component.saving).toBeFalse(); expect(component.isEditorOpen).toBeTrue();
    expect(component.form.controls.name.value).toBe('Test food');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    component.save(); expect(createFood).toHaveBeenCalledTimes(2);
  });
});
