export interface ApiProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  code?: string;
  [key: string]: unknown;
}

export interface ApiError {
  status: number;
  code?: string;
  title: string;
  message: string;
  traceId?: string;
  validationErrors?: Record<string, string[]>;
  problem?: ApiProblemDetails;
  originalError?: unknown;
  isApiError: true;
}

export function isApiError(error: unknown): error is ApiError {
  return !!error && typeof error === 'object' && (error as Partial<ApiError>).isApiError === true;
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  return isApiError(error) ? error.message : fallback;
}