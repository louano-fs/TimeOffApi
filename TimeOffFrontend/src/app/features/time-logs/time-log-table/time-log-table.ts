import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';

import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { DurationPipe } from '../../../shared/pipes/duration-pipe';
import { SessionTimePipe } from '../../../shared/pipes/session-time-pipe';
import { ShiftDatePipe } from '../../../shared/pipes/shift-date-pipe';

@Component({
  selector: 'app-time-log-table',
  imports: [DurationPipe, SessionTimePipe, ShiftDatePipe],
  templateUrl: './time-log-table.html',
  styleUrl: './time-log-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeLogTable {
  readonly timeLogs = input<PagedResponse<WorkSessionResponse> | null>(null);
  readonly eyebrow = input('History');
  readonly title = input('Time Logs');
  readonly emptyMessage = input('Your completed and active work sessions will appear here.');
  readonly caption = input('Employee work sessions and breaks');

  readonly isLoading = input(false);
  readonly errorMessage = input<string | null>(null);

  readonly pageRequested = output<number>();
  readonly refreshRequested = output<void>();

  protected readonly expandedSessionIds = signal<ReadonlySet<number>>(new Set());

  protected toggleSession(sessionId: number): void {
    this.expandedSessionIds.update((currentIds) => {
      const updatedIds = new Set(currentIds);

      if (updatedIds.has(sessionId)) {
        updatedIds.delete(sessionId);
      } else {
        updatedIds.add(sessionId);
      }

      return updatedIds;
    });
  }

  protected isExpanded(sessionId: number): boolean {
    return this.expandedSessionIds().has(sessionId);
  }

  protected requestPreviousPage(): void {
    const response = this.timeLogs();

    if (response && response.page > 1) {
      this.pageRequested.emit(response.page - 1);
    }
  }

  protected requestNextPage(): void {
    const response = this.timeLogs();

    if (response && response.page < response.totalPages) {
      this.pageRequested.emit(response.page + 1);
    }
  }
}
