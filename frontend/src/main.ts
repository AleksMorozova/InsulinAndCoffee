import { ErrorHandler } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { apiErrorInterceptor } from './app/core/api-error.interceptor';
import { GlobalErrorHandler } from './app/core/global-error-handler';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([apiErrorInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    { provide: ErrorHandler, useClass: GlobalErrorHandler }
  ]
}).catch((err) => console.error(err));
