import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Observable, Subscription, timer } from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { TimeClockApiService } from '../../../core/api/time-clock-api.service';
import { TimeLogsApiService } from '../../../core/api/time-logs-api.service';
import { LoginRequest } from '../../../core/auth/auth.model';
import { AuthService } from '../../../core/auth/auth.service';
import {
  ClockAction,
  ClockStatusResponse,
  TimeLogResponse,
} from '../../../shared/models/clock.model';
import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { LoginPanel } from '../../login/login-panel/login-panel';
import { ClockCard } from '../../time-clock/clock-card/clock-card';
import { TimeLogTable } from '../../time-logs/time-log-table/time-log-table';

@Component({
  selector: 'app-dashboard-page',
  imports: [LoginPanel, ClockCard, TimeLogTable],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly authService = inject(AuthService);

  private readonly clockApi = inject(TimeClockApiService);
  private readonly timeLogsApi = inject(TimeLogsApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoggingIn = signal(false);
  protected readonly loginError = signal<string | null>(null);

  protected readonly clockStatus = signal<ClockStatusResponse | null>(null);
  protected readonly isStatusLoading = signal(false);
  protected readonly clockError = signal<string | null>(null);
  protected readonly pendingClockAction = signal<ClockAction | null>(null);

  protected readonly timeLogs = signal<PagedResponse<WorkSessionResponse> | null>(null);
  protected readonly isTimeLogsLoading = signal(false);
  protected readonly timeLogsError = signal<string | null>(null);
  protected readonly currentTimeLogsPage = signal(1);

  private statusTimer: Subscription | null = null;
  private statusRequest: Subscription | null = null;
  private clockActionRequest: Subscription | null = null;
  private timeLogsRequest: Subscription | null = null;

  constructor() {
    effect(() => {
      const isAuthenticated = this.authService.isAuthenticated();

      untracked(() => {
        if (isAuthenticated) {
          this.startAuthenticatedDashboard();
        } else {
          this.resetDashboardState();
        }
      });
    });

    this.destroyRef.onDestroy(() => {
      this.stopStatusRefresh();
      this.cancelStatusRequest();
      this.cancelClockActionRequest();
      this.cancelTimeLogsRequest();
    });
  }

  protected login(request: LoginRequest): void {
    this.loginError.set(null);
    this.isLoggingIn.set(true);

    this.authService
      .login(request)
      .pipe(finalize(() => this.isLoggingIn.set(false)))
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
      .pipe(finalize(() => this.isStatusLoading.set(false)))
      .subscribe({
        next: (status) => {
          this.clockStatus.set(status);
          this.clockError.set(null);
        },
        error: (error: unknown) => {
          this.clockError.set(this.getErrorMessage(error, 'Unable to load your clock status.'));
        },
      });
  }

  protected loadTimeLogs(page = this.currentTimeLogsPage(), force = false): void {
    if (this.isTimeLogsLoading()) {
      if (!force) {
        return;
      }

      this.cancelTimeLogsRequest();
    }

    this.isTimeLogsLoading.set(true);

    this.timeLogsRequest = this.timeLogsApi
      .getMyTimeLogs(page, 20)
      .pipe(finalize(() => this.isTimeLogsLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.timeLogs.set(response);
          this.currentTimeLogsPage.set(response.page);
          this.timeLogsError.set(null);
        },
        error: (error: unknown) => {
          this.timeLogsError.set(this.getErrorMessage(error, 'Unable to load your time logs.'));
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

    this.clockActionRequest = this.createActionRequest(action, dateTime)
      .pipe(finalize(() => this.pendingClockAction.set(null)))
      .subscribe({
        next: () => {
          this.refreshDashboardData();
        },
        error: (error: unknown) => {
          this.clockError.set(
            this.getErrorMessage(error, 'The clock action could not be completed.'),
          );

          if (error instanceof HttpErrorResponse && error.status === 409) {
            this.refreshDashboardData();
          }
        },
      });
  }

  private createActionRequest(action: ClockAction, dateTime: string): Observable<TimeLogResponse> {
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

  private startAuthenticatedDashboard(): void {
    this.startStatusRefresh();
    this.loadTimeLogs(1, true);
  }

  private refreshDashboardData(): void {
    this.loadClockStatus(true);
    this.loadTimeLogs(1, true);
  }

  private startStatusRefresh(): void {
    this.stopStatusRefresh();
    this.loadClockStatus();

    this.statusTimer = timer(60_000, 60_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadClockStatus());
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

  private cancelClockActionRequest(): void {
    this.clockActionRequest?.unsubscribe();
    this.clockActionRequest = null;
    this.pendingClockAction.set(null);
  }

  private cancelTimeLogsRequest(): void {
    this.timeLogsRequest?.unsubscribe();
    this.timeLogsRequest = null;
    this.isTimeLogsLoading.set(false);
  }

  private resetClockState(): void {
    this.stopStatusRefresh();
    this.cancelStatusRequest();
    this.cancelClockActionRequest();

    this.clockStatus.set(null);
    this.clockError.set(null);
  }

  private resetTimeLogState(): void {
    this.cancelTimeLogsRequest();

    this.timeLogs.set(null);
    this.timeLogsError.set(null);
    this.currentTimeLogsPage.set(1);
  }

  private resetDashboardState(): void {
    this.resetClockState();
    this.resetTimeLogState();
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse && this.isApiError(error.error)) {
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
