import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PageErrorComponent } from './page-error.component';

describe('PageErrorComponent', () => {
  let fixture: ComponentFixture<PageErrorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PageErrorComponent] }).compileComponents();
    fixture = TestBed.createComponent(PageErrorComponent);
  });

  it('renders the supplied message and emits retry', () => {
    const retry = jasmine.createSpy('retry');
    fixture.componentInstance.title = 'Unable to load users';
    fixture.componentInstance.message = 'Please try again.';
    fixture.componentInstance.retry.subscribe(retry);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load users');
    expect(fixture.nativeElement.textContent).toContain('Please try again.');

    fixture.nativeElement.querySelector('button').click();
    expect(retry).toHaveBeenCalled();
  });
});