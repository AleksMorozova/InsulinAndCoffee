import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SettingsComponent } from './settings.component';

describe('SettingsComponent', () => {
  it('shows first-request server defaults immediately without a refresh', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getSettings', 'updateSettings']);
    api.getSettings.and.returnValue(of({ id: 'settings', targetGlucose: 6.5, carbRatio: 10, correctionFactor: 3, insulinDurationHours: 4, updatedAt: '' }));
    TestBed.configureTestingModule({ imports: [SettingsComponent], providers: [{ provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(SettingsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.form.controls.targetGlucose.value).toBe(6.5);
    expect(fixture.componentInstance.settings?.id).toBe('settings');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('button').disabled).toBeFalse();
    expect(api.getSettings).toHaveBeenCalledTimes(1);
  });

  it('shows genuine load failures and prevents saving an unloaded form', () => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['getSettings', 'updateSettings']);
    api.getSettings.and.returnValue(throwError(() => new Error('database unavailable')));
    TestBed.configureTestingModule({ imports: [SettingsComponent], providers: [{ provide: ApiService, useValue: api }] });
    const fixture = TestBed.createComponent(SettingsComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Could not load settings');
    expect(fixture.nativeElement.querySelector('button').disabled).toBeTrue();
    fixture.componentInstance.save();
    expect(api.updateSettings).not.toHaveBeenCalled();
  });
});
