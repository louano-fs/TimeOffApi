import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const accessToken = authService.accessToken();
  const isApiRequest = request.url.startsWith('/api');

  const authorizedRequest =
    isApiRequest && accessToken
      ? request.clone({
          setHeaders: {
            Authorization: `Bearer ${accessToken}`,
          },
        })
      : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (
        isApiRequest &&
        error instanceof HttpErrorResponse &&
        error.status === 401
      ) {
        authService.logout();
      }

      return throwError(() => error);
    }),
  );
};