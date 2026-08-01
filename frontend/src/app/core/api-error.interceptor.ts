import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiErrorMapper, isTemporaryOrUnexpectedApiError } from './api-error.mapper';
import { SKIP_GLOBAL_ERROR_NOTIFICATION } from './http-error-context';
import { LoggingService } from './logging.service';
import { NotificationService } from './notification.service';

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);
  const logger = inject(LoggingService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const apiError = ApiErrorMapper.map(error);

      if (isTemporaryOrUnexpectedApiError(apiError)) {
        logger.logHttpError(apiError);

        if (!request.context.get(SKIP_GLOBAL_ERROR_NOTIFICATION)) {
          notifications.show(apiError.message, {
            dedupeKey: `http-${apiError.status}-${apiError.message}`
          });
        }
      }

      return throwError(() => apiError);
    })
  );
};