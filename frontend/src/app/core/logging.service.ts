import { Injectable } from '@angular/core';
import { ApiError } from './api-error';

@Injectable({ providedIn: 'root' })
export class LoggingService {
  logHttpError(error: ApiError): void {
    console.error('[HTTP error]', {
      status: error.status,
      title: error.title,
      traceId: error.traceId,
      code: error.code
    });
  }

  logRuntimeError(error: unknown): void {
    console.error('[Application error]', error);
  }
}