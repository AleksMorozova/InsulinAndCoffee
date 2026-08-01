import { HttpErrorResponse } from '@angular/common/http';
import { ApiError, ApiProblemDetails, isApiError } from './api-error';

const defaultMessages: Record<number, string> = {
  0: 'Unable to connect to the server. Check your connection and try again.',
  400: 'Please check the highlighted fields and try again.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested item could not be found.',
  409: 'This item was changed by another user. Refresh the page and try again.',
  422: 'Please review the information and try again.',
  500: 'Something went wrong. Please try again.',
  502: 'The service is temporarily unavailable. Please try again later.',
  503: 'The service is temporarily unavailable. Please try again later.',
  504: 'The service is temporarily unavailable. Please try again later.'
};

const defaultTitles: Record<number, string> = {
  0: 'Connection problem',
  400: 'Validation failed',
  403: 'Access denied',
  404: 'Not found',
  409: 'Conflict',
  422: 'Unable to process request',
  500: 'Server error',
  502: 'Service unavailable',
  503: 'Service unavailable',
  504: 'Service unavailable'
};

export class ApiErrorMapper {
  static map(error: unknown, fallback = 'Request failed.'): ApiError {
    if (isApiError(error)) {
      return error;
    }

    if (error instanceof HttpErrorResponse) {
      const problem = parseProblemDetails(error.error);
      const status = problem?.status ?? error.status;
      const validationErrors = normalizeValidationErrors(problem?.errors);
      const message = selectMessage(status, problem, error.error, fallback);

      return {
        status,
        code: readString(problem, 'code'),
        title: problem?.title ?? defaultTitles[status] ?? 'Request failed',
        message,
        traceId: readString(problem, 'traceId'),
        validationErrors,
        problem,
        originalError: error,
        isApiError: true
      };
    }

    return {
      status: 0,
      title: 'Application error',
      message: fallback,
      originalError: error,
      isApiError: true
    };
  }
}

export function toApiError(error: unknown, fallback = 'Request failed.'): ApiError {
  return ApiErrorMapper.map(error, fallback);
}

export function isExpectedApiError(error: ApiError): boolean {
  return [400, 409, 422].includes(error.status);
}

export function isTemporaryOrUnexpectedApiError(error: ApiError): boolean {
  return error.status === 0 || error.status === 500 || [502, 503, 504].includes(error.status);
}

function parseProblemDetails(error: unknown): ApiProblemDetails | undefined {
  if (!error || typeof error !== 'object' || Array.isArray(error)) {
    return undefined;
  }

  return error as ApiProblemDetails;
}

function selectMessage(status: number, problem: ApiProblemDetails | undefined, rawError: unknown, fallback: string): string {
  if (status >= 500 || status === 0 || [502, 503, 504].includes(status)) {
    return defaultMessages[status] ?? defaultMessages[500];
  }

  const rawMessage = typeof rawError === 'string' && rawError.trim() ? rawError : undefined;
  return problem?.detail ?? problem?.title ?? rawMessage ?? defaultMessages[status] ?? fallback;
}

function normalizeValidationErrors(errors: Record<string, string[]> | undefined): Record<string, string[]> | undefined {
  if (!errors) {
    return undefined;
  }

  return Object.entries(errors).reduce<Record<string, string[]>>((result, [field, messages]) => {
    result[field] = Array.isArray(messages) ? messages : [String(messages)];
    return result;
  }, {});
}

function readString(problem: ApiProblemDetails | undefined, key: string): string | undefined {
  const value = problem?.[key];
  return typeof value === 'string' && value.trim() ? value : undefined;
}