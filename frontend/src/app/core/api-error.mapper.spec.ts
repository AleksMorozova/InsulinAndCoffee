import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorMapper } from './api-error.mapper';

const problem = (status: number, body: object) => new HttpErrorResponse({ status, error: body, url: '/api/test' });

describe('ApiErrorMapper', () => {
  it('maps ASP.NET Core ValidationProblemDetails responses', () => {
    const error = ApiErrorMapper.map(problem(400, {
      title: 'Validation failed',
      status: 400,
      detail: 'Please fix the form.',
      traceId: 'trace-123',
      errors: {
        Name: ['Name is required.']
      }
    }));

    expect(error.status).toBe(400);
    expect(error.title).toBe('Validation failed');
    expect(error.message).toBe('Please fix the form.');
    expect(error.traceId).toBe('trace-123');
    expect(error.validationErrors?.['Name']).toEqual(['Name is required.']);
  });

  it('uses safe copy for server errors instead of backend details', () => {
    const error = ApiErrorMapper.map(problem(500, {
      title: 'Npgsql.PostgresException',
      status: 500,
      detail: 'relation "KnownMeals" does not exist'
    }));

    expect(error.message).toBe('Something went wrong. Please try again.');
    expect(error.message).not.toContain('KnownMeals');
  });

  it('maps network failures to a connection message', () => {
    const error = ApiErrorMapper.map(new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') }));

    expect(error.status).toBe(0);
    expect(error.message).toBe('Unable to connect to the server. Check your connection and try again.');
  });
});