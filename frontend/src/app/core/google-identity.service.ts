import { DOCUMENT } from '@angular/common';
import { Inject, Injectable } from '@angular/core';

export interface GoogleCredentialResponse { credential: string; }

interface GoogleIdentityApi {
  initialize(configuration: { client_id: string; callback: (response: GoogleCredentialResponse) => void }): void;
  renderButton(element: HTMLElement, configuration: { theme: string; size: string; width: number; text: string }): void;
  disableAutoSelect(): void;
}

declare global {
  interface Window { google?: { accounts: { id: GoogleIdentityApi } }; }
}

@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
  private loadPromise?: Promise<GoogleIdentityApi>;

  constructor(@Inject(DOCUMENT) private readonly document: Document) {}

  load(): Promise<GoogleIdentityApi> {
    if (window.google) return Promise.resolve(window.google.accounts.id);
    if (this.loadPromise) return this.loadPromise;
    this.loadPromise = new Promise((resolve, reject) => {
      const script = this.document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      script.defer = true;
      script.onload = () => window.google ? resolve(window.google.accounts.id) : reject(new Error('Google Identity did not initialize.'));
      script.onerror = () => reject(new Error('Google Identity could not be loaded.'));
      this.document.head.appendChild(script);
    });
    return this.loadPromise;
  }
}
