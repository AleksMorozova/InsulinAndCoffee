import { Component } from '@angular/core';
import { NotificationService } from '../../core/notification.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  template: `
    <section class="toast-region" aria-live="polite" aria-atomic="true" aria-label="Notifications">
      @for (notification of notifications.notifications(); track notification.id) {
        <article class="toast-card" role="status">
          <span>{{ notification.message }}</span>
          <div class="toast-actions">
            @if (notification.action && notification.actionLabel) {
              <button type="button" class="toast-action" (click)="runAction(notification.id, notification.action)">
                {{ notification.actionLabel }}
              </button>
            }
            <button type="button" class="toast-dismiss" (click)="notifications.dismiss(notification.id)" aria-label="Dismiss notification">
              ×
            </button>
          </div>
        </article>
      }
    </section>
  `
})
export class ToastContainerComponent {
  constructor(readonly notifications: NotificationService) {}

  runAction(id: number, action: () => void): void {
    this.notifications.dismiss(id);
    action();
  }
}