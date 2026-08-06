import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { finalize, Subscription } from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { DownloadResponse } from '../../../core/api/download-response';
import { TeamApiService } from '../../../core/api/team-api.service';
import { TimeLogsApiService } from '../../../core/api/time-logs-api.service';
import {
  PersonalTimeReport,
  TeamMemberTimeReport,
  TeamTimeReport,
  TimeReportRange,
} from '../../../shared/models/time-report.model';
import { SecondsDurationPipe } from '../../../shared/pipes/seconds-duration-pipe';

type TeamView = 'all' | 'belowEightHours';

@Component({
  selector: 'app-time-insights-panel',
  imports: [SecondsDurationPipe],
  templateUrl: './time-insights-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeInsightsPanel implements OnInit {
  readonly isManager = input(false);

  private readonly timeLogsApi = inject(TimeLogsApiService);
  private readonly teamApi = inject(TeamApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly personalReport = signal<PersonalTimeReport | null>(null);
  protected readonly personalLabel = signal('Today');
  protected readonly personalLoading = signal(false);
  protected readonly personalError = signal<string | null>(null);
  protected readonly personalExporting = signal(false);
  protected readonly personalExportError = signal<string | null>(null);
  protected readonly personalStartDate = signal('');
  protected readonly personalEndDate = signal('');

  protected readonly teamReport = signal<TeamTimeReport | null>(null);
  protected readonly teamLabel = signal('Today');
  protected readonly teamView = signal<TeamView>('all');
  protected readonly teamLoading = signal(false);
  protected readonly teamError = signal<string | null>(null);
  protected readonly teamExporting = signal(false);
  protected readonly teamExportError = signal<string | null>(null);
  protected readonly teamStartDate = signal('');
  protected readonly teamEndDate = signal('');
  protected readonly includeInactive = signal(false);

  protected readonly visibleTeamMembers = computed<readonly TeamMemberTimeReport[]>(() => {
    const members = this.teamReport()?.members ?? [];
    if (this.teamView() === 'all') {
      return members;
    }

    return [...members]
      .filter((member) => member.workedSeconds < 28_800)
      .sort(
        (first, second) =>
          first.workedSeconds - second.workedSeconds ||
          first.lastName.localeCompare(second.lastName) ||
          first.firstName.localeCompare(second.firstName) ||
          first.employeeNumber.localeCompare(second.employeeNumber),
      );
  });

  private personalToday = '';
  private teamToday = '';
  private personalRequest: Subscription | null = null;
  private teamRequest: Subscription | null = null;
  private personalExportRequest: Subscription | null = null;
  private teamExportRequest: Subscription | null = null;
  private readonly activeObjectUrls = new Set<string>();

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.personalRequest?.unsubscribe();
      this.teamRequest?.unsubscribe();
      this.personalExportRequest?.unsubscribe();
      this.teamExportRequest?.unsubscribe();
      for (const url of this.activeObjectUrls) {
        URL.revokeObjectURL(url);
      }
      this.activeObjectUrls.clear();
    });
  }

  ngOnInit(): void {
    this.loadPersonalToday();
    if (this.isManager()) {
      this.loadTeamToday();
    }
  }

  protected loadPersonalToday(): void {
    this.loadPersonal(undefined, 'Today');
  }

  protected loadPersonalWeek(): void {
    if (!this.personalToday) {
      this.loadPersonalToday();
      return;
    }
    this.loadPersonal(this.weekRange(this.personalToday), 'This week');
  }

  protected loadPersonalMonth(): void {
    if (!this.personalToday) {
      this.loadPersonalToday();
      return;
    }
    this.loadPersonal(this.monthRange(this.personalToday), 'This month');
  }

  protected applyPersonalRange(): void {
    const range = this.customRange(this.personalStartDate(), this.personalEndDate());
    if (!range) {
      this.personalError.set('Choose a valid start date that is not after the end date.');
      return;
    }
    this.loadPersonal(range, 'Custom range');
  }

  protected exportPersonal(): void {
    const report = this.personalReport();
    if (!report || this.personalExporting()) {
      return;
    }

    this.personalExportRequest?.unsubscribe();
    this.personalExporting.set(true);
    this.personalExportError.set(null);
    this.personalExportRequest = this.timeLogsApi
      .downloadExport(this.reportRange(report))
      .pipe(finalize(() => this.personalExporting.set(false)))
      .subscribe({
        next: (download) => this.saveDownload(download),
        error: (error: unknown) =>
          this.personalExportError.set(
            this.errorMessage(error, 'Unable to export your time logs.'),
          ),
      });
  }

  protected loadTeamToday(): void {
    this.loadTeam(undefined, 'Today', 'all');
  }

  protected loadTeamWeek(): void {
    if (!this.teamToday) {
      this.loadTeamToday();
      return;
    }
    this.loadTeam(this.weekRange(this.teamToday), 'This week', 'all');
  }

  protected loadBelowEightHoursToday(): void {
    if (!this.teamToday) {
      this.loadTeamToday();
      return;
    }
    this.loadTeam(
      { startDate: this.teamToday, endDate: this.teamToday },
      'Below 8 hours today',
      'belowEightHours',
    );
  }

  protected applyTeamRange(): void {
    const range = this.customRange(this.teamStartDate(), this.teamEndDate());
    if (!range) {
      this.teamError.set('Choose a valid start date that is not after the end date.');
      return;
    }
    this.loadTeam(range, 'Custom range', 'all');
  }

  protected toggleInactive(event: Event): void {
    this.includeInactive.set((event.target as HTMLInputElement).checked);
    const report = this.teamReport();
    if (report) {
      this.loadTeam(this.reportRange(report), this.teamLabel(), this.teamView());
    }
  }

  protected exportTeam(): void {
    const report = this.teamReport();
    if (!report || this.teamExporting()) {
      return;
    }

    this.teamExportRequest?.unsubscribe();
    this.teamExporting.set(true);
    this.teamExportError.set(null);
    this.teamExportRequest = this.teamApi
      .downloadExport(this.reportRange(report), this.includeInactive())
      .pipe(finalize(() => this.teamExporting.set(false)))
      .subscribe({
        next: (download) => this.saveDownload(download),
        error: (error: unknown) =>
          this.teamExportError.set(this.errorMessage(error, 'Unable to export team time logs.')),
      });
  }

  protected updatePersonalStart(event: Event): void {
    this.personalStartDate.set((event.target as HTMLInputElement).value);
  }

  protected updatePersonalEnd(event: Event): void {
    this.personalEndDate.set((event.target as HTMLInputElement).value);
  }

  protected updateTeamStart(event: Event): void {
    this.teamStartDate.set((event.target as HTMLInputElement).value);
  }

  protected updateTeamEnd(event: Event): void {
    this.teamEndDate.set((event.target as HTMLInputElement).value);
  }

  protected formatAsOf(asOf: string, timezone: string): string {
    try {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
        timeZone: timezone,
      }).format(new Date(asOf));
    } catch {
      return asOf;
    }
  }

  private loadPersonal(range: TimeReportRange | undefined, label: string): void {
    this.personalRequest?.unsubscribe();
    this.personalLoading.set(true);
    this.personalError.set(null);
    this.personalRequest = this.timeLogsApi
      .getReport(range)
      .pipe(finalize(() => this.personalLoading.set(false)))
      .subscribe({
        next: (report) => {
          this.personalReport.set(report);
          this.personalLabel.set(label);
          this.personalStartDate.set(report.startDate);
          this.personalEndDate.set(report.endDate);
          if (!this.personalToday || label === 'Today') {
            this.personalToday = report.endDate;
          }
        },
        error: (error: unknown) =>
          this.personalError.set(this.errorMessage(error, 'Unable to load your time insights.')),
      });
  }

  private loadTeam(range: TimeReportRange | undefined, label: string, view: TeamView): void {
    this.teamRequest?.unsubscribe();
    this.teamLoading.set(true);
    this.teamError.set(null);
    this.teamRequest = this.teamApi
      .getReport(range, this.includeInactive())
      .pipe(finalize(() => this.teamLoading.set(false)))
      .subscribe({
        next: (report) => {
          this.teamReport.set(report);
          this.teamLabel.set(label);
          this.teamView.set(view);
          this.teamStartDate.set(report.startDate);
          this.teamEndDate.set(report.endDate);
          if (!this.teamToday || label === 'Today') {
            this.teamToday = report.endDate;
          }
        },
        error: (error: unknown) =>
          this.teamError.set(this.errorMessage(error, 'Unable to load team time insights.')),
      });
  }

  private reportRange(report: PersonalTimeReport | TeamTimeReport): TimeReportRange {
    return { startDate: report.startDate, endDate: report.endDate };
  }

  private weekRange(today: string): TimeReportRange {
    const date = this.parseDate(today);
    const daysSinceMonday = (date.getUTCDay() + 6) % 7;
    const start = new Date(date);
    start.setUTCDate(start.getUTCDate() - daysSinceMonday);
    return { startDate: this.formatDate(start), endDate: today };
  }

  private monthRange(today: string): TimeReportRange {
    return { startDate: `${today.slice(0, 7)}-01`, endDate: today };
  }

  private customRange(startDate: string, endDate: string): TimeReportRange | null {
    const isIsoDate = /^\d{4}-\d{2}-\d{2}$/;
    if (!isIsoDate.test(startDate) || !isIsoDate.test(endDate) || startDate > endDate) {
      return null;
    }
    return { startDate, endDate };
  }

  private parseDate(value: string): Date {
    return new Date(`${value}T00:00:00Z`);
  }

  private formatDate(value: Date): string {
    return value.toISOString().slice(0, 10);
  }

  private saveDownload(download: DownloadResponse): void {
    const url = URL.createObjectURL(download.blob);
    this.activeObjectUrls.add(url);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = download.fileName;
    anchor.hidden = true;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    queueMicrotask(() => {
      URL.revokeObjectURL(url);
      this.activeObjectUrls.delete(url);
    });
  }

  private errorMessage(error: unknown, fallback: string): string {
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
