import { ErrorHandler, Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import { LoggingService } from './logging.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  constructor(
    private readonly logger: LoggingService,
    private readonly router: Router,
    private readonly zone: NgZone
  ) {}

  handleError(error: unknown): void {
    this.logger.logRuntimeError(error);

    const currentUrl = this.router.url.split('?')[0];
    if (currentUrl === '/error') {
      return;
    }

    this.zone.run(() => {
      this.router.navigate(['/error'], { queryParams: { returnUrl: this.router.url } });
    });
  }
}