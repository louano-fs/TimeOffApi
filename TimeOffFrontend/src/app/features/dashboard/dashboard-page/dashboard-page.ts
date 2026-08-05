import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  finalize,
  Observable,
  Subscription,
  timer,
} from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { TimeClockApiService } from '../../../core/api/time-clock-api.service';
import { LoginRequest } from '../../../core/auth/auth.model';
import { AuthService } from '../../../core/auth/auth.service';
import {
  ClockAction,
  ClockStatusResponse,
  TimeLogResponse,
} from '../../../shared/models/clock.model';
import { LoginPanel } from '../../login/login-panel/login-panel';
import { ClockCard } from '../../time-clock/clock-card/clock-card';

@Component({
  selector: 'app-dashboard-page',
  imports: [
    LoginPanel,
    ClockCard,
  ],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly authService = inject(AuthService);

  private readonly clockApi = inject(TimeClockApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoggingIn = signal(false);
  protected readonly loginError = signal<string | null>(null);

  protected readonly clockStatus =
    signal<ClockStatusResponse | null>(null);

  protected readonly isStatusLoading = signal(false);
  protected readonly clockError = signal<string | null>(null);
  protected readonly pendingClockAction =
    signal<ClockAction | null>(null);

  private statusTimer: Subscription | null = null;
  private statusRequest: Subscription | null = null;

  constructor() {
    effect(() => {
      if (this.authService.isAuthenticated()) {
        this.startStatusRefresh();
      } else {
        this.resetClockState();
      }
    });

    this.destroyRef.onDestroy(() => {
      this.stopStatusRefresh();
      this.cancelStatusRequest();
    });
  }

  protected login(request: LoginRequest): void {
    this.loginError.set(null);
    this.isLoggingIn.set(true);

    this.authService
      .login(request)
      .pipe(
        finalize(() => this.isLoggingIn.set(false)),
      )
      .subscribe({
        error: (error: unknown) => {
          this.loginError.set(
            this.getErrorMessage(
              error,
              'Unable to sign in. Check that the API is running and try again.',
            ),
          );
        },
      });
  }

  protected logout(): void {
    this.authService.logout();
    this.loginError.set(null);
  }

  protected loadClockStatus(force = false): void {
    if (this.isStatusLoading()) {
      if (!force) {
        return;
      }

      this.cancelStatusRequest();
    }

    this.isStatusLoading.set(true);

    this.statusRequest = this.clockApi
      .getStatus()
      .pipe(
        finalize(() => {
          this.isStatusLoading.set(false);
        }),
      )
      .subscribe({
        next: (status) => {
          this.clockStatus.set(status);
          this.clockError.set(null);
        },
        error: (error: unknown) => {
          this.clockError.set(
            this.getErrorMessage(
              error,
              'Unable to load your clock status.',
            ),
          );
        },
      });
  }

  protected performClockAction(action: ClockAction): void {
    if (this.pendingClockAction() !== null) {
      return;
    }

    this.pendingClockAction.set(action);
    this.clockError.set(null);

    const dateTime = new Date().toISOString();

    this.createActionRequest(action, dateTime)
      .pipe(
        finalize(() => {
          this.pendingClockAction.set(null);
        }),
      )
      .subscribe({
        next: () => {
          this.loadClockStatus(true);
        },
        error: (error: unknown) => {
          this.clockError.set(
            this.getErrorMessage(
              error,
              'The clock action could not be completed.',
            ),
          );

          if (
            error instanceof HttpErrorResponse &&
            error.status === 409
          ) {
            this.loadClockStatus(true);
          }
        },
      });
  }

  private createActionRequest(
    action: ClockAction,
    dateTime: string,
  ): Observable<TimeLogResponse> {
    switch (action) {
      case 'clockIn':
        return this.clockApi.clockIn(dateTime);

      case 'startBreak':
        return this.clockApi.startBreak(dateTime);

      case 'endBreak':
        return this.clockApi.endBreak(dateTime);

      case 'clockOut':
        return this.clockApi.clockOut(dateTime);
    }
  }

  private startStatusRefresh(): void {
    this.stopStatusRefresh();

    this.statusTimer = timer(0, 60_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadClockStatus();
      });
  }

  private stopStatusRefresh(): void {
    this.statusTimer?.unsubscribe();
    this.statusTimer = null;
  }

  private cancelStatusRequest(): void {
    this.statusRequest?.unsubscribe();
    this.statusRequest = null;
    this.isStatusLoading.set(false);
  }

  private resetClockState(): void {
    this.stopStatusRefresh();
    this.cancelStatusRequest();

    this.clockStatus.set(null);
    this.clockError.set(null);
    this.pendingClockAction.set(null);
  }

  private getErrorMessage(
    error: unknown,
    fallback: string,
  ): string {
    if (
      error instanceof HttpErrorResponse &&
      this.isApiError(error.error)
    ) {
      return error.error.message;
    }

    return fallback;
  }

  private isApiError(value: unknown): value is ApiError {
    return (
      typeof value === 'object' &&
      value !== null &&
      'message' in value &&
      typeof value.message === 'string'
    );
  }
}