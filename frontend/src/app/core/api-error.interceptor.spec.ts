import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from './api-error.interceptor';
import { ApiError } from './api-error';
import { SKIP_GLOBAL_ERROR_NOTIFICATION } from './http-error-context';
import { LoggingService } from './logging.service';
import { NotificationService } from './notification.service';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Router } from '@angular/router';

describe('apiErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let notifications: NotificationService;
  let logger: jasmine.SpyObj<LoggingService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    logger = jasmine.createSpyObj<LoggingService>('LoggingService', ['logHttpError', 'logRuntimeError']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate', 'navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        NotificationService,
        { provide: LoggingService, useValue: logger }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    notifications = TestBed.inject(NotificationService);
  });

  afterEach(() => httpMock.verify());

  it('normalizes 500 errors, logs them, shows one global toast, and keeps the error for subscribers', () => {
    const received: ApiError[] = [];

    http.get('/api/fail').subscribe({ error: (error: ApiError) => received.push(error) });
    http.get('/api/fail-again').subscribe({ error: (error: ApiError) => received.push(error) });

    httpMock.expectOne('/api/fail').flush({ title: 'Server exploded', status: 500, detail: 'stack trace' }, { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/fail-again').flush({ title: 'Server exploded', status: 500, detail: 'stack trace' }, { status: 500, statusText: 'Server Error' });

    expect(received.length).toBe(2);
    expect(received[0].message).toBe('Something went wrong. Please try again.');
    expect(logger.logHttpError).toHaveBeenCalledTimes(2);
    expect(notifications.notifications().length).toBe(1);
  });

  it('does not show a global toast when the request opts into local handling', () => {
    http.get('/api/local', {
      context: new HttpContext().set(SKIP_GLOBAL_ERROR_NOTIFICATION, true)
    }).subscribe({ error: () => undefined });

    httpMock.expectOne('/api/local').flush({}, { status: 503, statusText: 'Unavailable' });

    expect(logger.logHttpError).toHaveBeenCalled();
    expect(notifications.notifications().length).toBe(0);
  });

  it('normalizes 404 ProblemDetails and rethrows it without global UI decisions', () => {
    let received: ApiError | undefined;

    http.get('/api/meals/missing-id').subscribe({ error: (error: ApiError) => received = error });

    httpMock.expectOne('/api/meals/missing-id').flush({
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
      title: 'Resource not found',
      status: 404,
      detail: "Meal with id 'missing-id' was not found.",
      instance: '/api/meals/missing-id',
      traceId: 'trace-404'
    }, { status: 404, statusText: 'Not Found' });

    expect(received?.status).toBe(404);
    expect(received?.title).toBe('Resource not found');
    expect(received?.message).toBe("Meal with id 'missing-id' was not found.");
    expect(received?.traceId).toBe('trace-404');
    expect(notifications.notifications().length).toBe(0);
    expect(logger.logHttpError).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('passes validation errors back without global notification', () => {
    let received: ApiError | undefined;

    http.post('/api/form', {}).subscribe({ error: (error: ApiError) => received = error });

    httpMock.expectOne('/api/form').flush({
      title: 'Validation failed',
      status: 400,
      errors: { Name: ['Required'] }
    }, { status: 400, statusText: 'Bad Request' });

    expect(received?.validationErrors?.['Name']).toEqual(['Required']);
    expect(notifications.notifications().length).toBe(0);
    expect(logger.logHttpError).not.toHaveBeenCalled();
  });
});