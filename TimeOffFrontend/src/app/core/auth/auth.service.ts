import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { AuthResponse, LoginRequest } from './auth.model';

const SESSION_STORAGE_KEY = 'time-clock-session';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly sessionState = signal<AuthResponse | null>(
    this.restoreSession(),
  );

  readonly session = this.sessionState.asReadonly();

  readonly isAuthenticated = computed(() => this.sessionState() !== null);

  readonly accessToken = computed(
    () => this.sessionState()?.accessToken ?? null,
  );

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  logout(): void {
    sessionStorage.removeItem(SESSION_STORAGE_KEY);
    this.sessionState.set(null);
  }

  private storeSession(session: AuthResponse): void {
    sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(session),
    );

    this.sessionState.set(session);
  }

  private restoreSession(): AuthResponse | null {
    const storedValue = sessionStorage.getItem(SESSION_STORAGE_KEY);

    if (!storedValue) {
      return null;
    }

    try {
      const session = JSON.parse(storedValue) as AuthResponse;
      const expirationTime = Date.parse(session.expiresAt);

      const isInvalid =
        !session.accessToken ||
        Number.isNaN(expirationTime) ||
        expirationTime <= Date.now();

      if (isInvalid) {
        sessionStorage.removeItem(SESSION_STORAGE_KEY);
        return null;
      }

      return session;
    } catch {
      sessionStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }
  }
}