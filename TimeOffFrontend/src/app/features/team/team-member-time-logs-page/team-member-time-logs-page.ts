import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, Subscription } from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { TeamApiService } from '../../../core/api/team-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { TeamMember } from '../../../shared/models/team.model';
import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { TimeLogTable } from '../../time-logs/time-log-table/time-log-table';

@Component({
  selector: 'app-team-member-time-logs-page',
  imports: [RouterLink, TimeLogTable],
  templateUrl: './team-member-time-logs-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamMemberTimeLogsPage {
  protected readonly authService = inject(AuthService);

  private readonly teamApi = inject(TeamApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly userId = Number(this.route.snapshot.paramMap.get('userId'));

  protected readonly member = signal<TeamMember | null>(null);
  protected readonly timeLogs = signal<PagedResponse<WorkSessionResponse> | null>(null);
  protected readonly isMemberLoading = signal(true);
  protected readonly isTimeLogsLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly currentPage = signal(1);

  private timeLogsRequest: Subscription | null = null;

  constructor() {
    if (!Number.isInteger(this.userId) || this.userId <= 0) {
      this.isMemberLoading.set(false);
      this.errorMessage.set('Team member was not found.');
      return;
    }

    this.teamApi
      .getMembers()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.isMemberLoading.set(false)),
      )
      .subscribe({
        next: (members) => {
          const member = members.find((candidate) => candidate.userId === this.userId);
          if (!member) {
            this.errorMessage.set('Team member was not found.');
            return;
          }

          this.member.set(member);
          this.loadTimeLogs();
        },
        error: (error: unknown) => {
          this.errorMessage.set(this.getErrorMessage(error, 'Unable to load this team member.'));
        },
      });

    this.destroyRef.onDestroy(() => this.cancelTimeLogsRequest());
  }

  protected loadTimeLogs(page = this.currentPage(), force = false): void {
    if (this.isTimeLogsLoading()) {
      if (!force) {
        return;
      }

      this.cancelTimeLogsRequest();
    }

    this.isTimeLogsLoading.set(true);
    this.timeLogsRequest = this.teamApi
      .getTimeLogs(this.userId, page, 20)
      .pipe(finalize(() => this.isTimeLogsLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.timeLogs.set(response);
          this.currentPage.set(response.page);
          this.errorMessage.set(null);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            this.getErrorMessage(error, "Unable to load this employee's time logs."),
          );
        },
      });
  }

  protected logout(): void {
    this.authService.logout();
    void this.router.navigateByUrl('/');
  }

  private cancelTimeLogsRequest(): void {
    this.timeLogsRequest?.unsubscribe();
    this.timeLogsRequest = null;
    this.isTimeLogsLoading.set(false);
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
