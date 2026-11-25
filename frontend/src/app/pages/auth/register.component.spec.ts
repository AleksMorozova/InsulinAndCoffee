import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { RegisterComponent } from './register.component';

describe('Registration guidance', () => {
  it('explains requirements before submission and announces invalid fields without losing input', async () => {
    const register = jasmine.createSpy();
    await TestBed.configureTestingModule({ imports: [RegisterComponent], providers: [provideRouter([]),
      { provide: AuthService, useValue: { register } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegisterComponent); fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('#password-help')?.textContent).toContain('8–128');
    fixture.componentInstance.form.setValue({ username: 'ab', password: '1234567' });
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click(); fixture.detectChanges();
    expect(register).not.toHaveBeenCalled();
    expect(host.querySelector('[formControlName="password"]')?.getAttribute('aria-invalid')).toBe('true');
    expect(host.querySelector('#password-error')?.textContent).toContain('8–128');
    expect(fixture.componentInstance.form.controls.password.value).toBe('1234567');
  });

  it('preserves password bytes and prevents duplicate submission while navigating after success', async () => {
    const pending = new Subject<void>();
    const register = jasmine.createSpy().and.returnValue(pending);
    await TestBed.configureTestingModule({ imports: [RegisterComponent], providers: [provideRouter([]),
      { provide: AuthService, useValue: { register } }] }).compileComponents();
    const fixture = TestBed.createComponent(RegisterComponent);
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.componentInstance.form.setValue({ username: 'tester', password: ' 123456 ' });
    fixture.componentInstance.submit(); fixture.componentInstance.submit();
    expect(register).toHaveBeenCalledOnceWith('tester', ' 123456 ');
    pending.next(); pending.complete();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: 'true' } });
  });
});
