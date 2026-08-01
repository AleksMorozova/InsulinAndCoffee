import { Injectable, computed, signal } from '@angular/core';

export interface ToastNotification {
  id: number;
  message: string;
  actionLabel?: string;
  action?: () => void;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly items = signal<ToastNotification[]>([]);
  private readonly recentKeys = new Map<string, number>();
  private nextId = 1;

  readonly notifications = computed(() => this.items());

  show(message: string, options: { dedupeKey?: string; timeoutMs?: number; actionLabel?: string; action?: () => void } = {}): void {
    const dedupeKey = options.dedupeKey ?? message;
    const now = Date.now();
    const lastShown = this.recentKeys.get(dedupeKey) ?? 0;

    if (now - lastShown < 5000) {
      return;
    }

    this.recentKeys.set(dedupeKey, now);
    const notification: ToastNotification = {
      id: this.nextId++,
      message,
      actionLabel: options.actionLabel,
      action: options.action
    };

    this.items.update((items) => [...items, notification]);
    window.setTimeout(() => this.dismiss(notification.id), options.timeoutMs ?? 6000);
  }

  dismiss(id: number): void {
    this.items.update((items) => items.filter((item) => item.id !== id));
  }

  clear(): void {
    this.items.set([]);
  }
}