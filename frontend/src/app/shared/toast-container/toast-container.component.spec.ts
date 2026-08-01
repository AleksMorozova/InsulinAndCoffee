import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NotificationService } from '../../core/notification.service';
import { ToastContainerComponent } from './toast-container.component';

describe('ToastContainerComponent', () => {
  let fixture: ComponentFixture<ToastContainerComponent>;
  let notifications: NotificationService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ToastContainerComponent] }).compileComponents();
    fixture = TestBed.createComponent(ToastContainerComponent);
    notifications = TestBed.inject(NotificationService);
  });

  it('renders and dismisses notifications', () => {
    notifications.show('Something went wrong. Please try again.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Something went wrong. Please try again.');

    fixture.nativeElement.querySelector('.toast-dismiss').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Something went wrong. Please try again.');
  });

  it('deduplicates repeated outage messages', () => {
    notifications.show('Service unavailable', { dedupeKey: 'outage' });
    notifications.show('Service unavailable', { dedupeKey: 'outage' });

    expect(notifications.notifications().length).toBe(1);
  });
});