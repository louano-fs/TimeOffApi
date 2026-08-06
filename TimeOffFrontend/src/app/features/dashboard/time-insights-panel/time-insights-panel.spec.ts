import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PersonalTimeReport, TeamTimeReport } from '../../../shared/models/time-report.model';
import { TimeInsightsPanel } from './time-insights-panel';

describe('TimeInsightsPanel', () => {
  let fixture: ComponentFixture<TimeInsightsPanel>;
  let httpTesting: HttpTestingController;

  const personalToday: PersonalTimeReport = {
    startDate: '2026-08-06',
    endDate: '2026-08-06',
    reportingTimezone: 'Asia/Manila',
    asOf: '2026-08-06T04:00:00Z',
    workedSeconds: 29_125,
    breakSeconds: 3_600,
    workSessionCount: 2,
    daily: [{ date: '2026-08-06', workedSeconds: 29_125, breakSeconds: 3_600 }],
  };

  const teamToday: TeamTimeReport = {
    startDate: '2026-08-06',
    endDate: '2026-08-06',
    reportingTimezone: 'Asia/Manila',
    asOf: '2026-08-06T04:00:00Z',
    includedMemberCount: 3,
    excludedInactiveCount: 1,
    totalWorkedSeconds: 57_599,
    totalBreakSeconds: 3_600,
    averageWorkedSeconds: 19_199.666,
    members: [
      {
        userId: 21,
        employeeId: 1001,
        employeeNumber: 'EMP-1001',
        firstName: 'Eight',
        lastName: 'Hours',
        isActive: true,
        workedSeconds: 28_800,
        breakSeconds: 3_600,
        workSessionCount: 1,
      },
      {
        userId: 22,
        employeeId: 1002,
        employeeNumber: 'EMP-1002',
        firstName: 'Almost',
        lastName: 'Eight',
        isActive: true,
        workedSeconds: 28_799,
        breakSeconds: 0,
        workSessionCount: 1,
      },
      {
        userId: 23,
        employeeId: 1003,
        employeeNumber: 'EMP-1003',
        firstName: 'Zero',
        lastName: 'Time',
        isActive: true,
        workedSeconds: 0,
        breakSeconds: 0,
        workSessionCount: 0,
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeInsightsPanel],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    fixture?.destroy();
    httpTesting.verify();
    vi.restoreAllMocks();
  });

  it('shows exact personal insights to an employee without manager controls', () => {
    createPanel(false);
    expectPersonalReportRequest().flush(personalToday);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('My time insights');
    expect(fixture.nativeElement.textContent).toContain('8h 5m 25s');
    expect(fixture.nativeElement.textContent).not.toContain('Team time insights');
    httpTesting.expectNone('/api/team/report');
  });

  it('anchors this week to the local day returned by the server', () => {
    createPanel(false);
    expectPersonalReportRequest().flush(personalToday);
    fixture.detectChanges();

    const weekButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-personal-preset="week"]',
    );
    weekButton.click();

    const request = expectPersonalReportRequest();
    expect(request.request.params.get('startDate')).toBe('2026-08-03');
    expect(request.request.params.get('endDate')).toBe('2026-08-06');
    request.flush({ ...personalToday, startDate: '2026-08-03' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('This week');
  });

  it('validates a custom range before requesting it', () => {
    createPanel(false);
    expectPersonalReportRequest().flush(personalToday);
    fixture.detectChanges();

    setInputValue('#personal-start-date', '2026-08-10');
    setInputValue('#personal-end-date', '2026-08-01');
    const applyButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-personal-custom-range]',
    );
    applyButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Choose a valid start date that is not after the end date.',
    );
    httpTesting.expectNone('/api/time-logs/report');
  });

  it('shows only employees below the exact eight-hour boundary in manager mode', () => {
    createPanel(true);
    flushInitialManagerReports();
    fixture.detectChanges();

    const thresholdButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-team-preset="below-eight"]',
    );
    thresholdButton.click();
    const request = expectTeamReportRequest();
    expect(request.request.params.get('startDate')).toBe('2026-08-06');
    expect(request.request.params.get('endDate')).toBe('2026-08-06');
    request.flush(teamToday);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-team-insight-member="21"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-team-insight-member="22"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-team-insight-member="23"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain(
      'not an attendance or performance judgment',
    );
  });

  it('reloads the current team range when inactive employees are included', () => {
    createPanel(true);
    flushInitialManagerReports();
    fixture.detectChanges();

    const checkbox: HTMLInputElement =
      fixture.nativeElement.querySelector('input[type="checkbox"]');
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));

    const request = expectTeamReportRequest(true);
    expect(request.request.params.get('includeInactive')).toBe('true');
    expect(request.request.params.get('startDate')).toBe('2026-08-06');
    request.flush({ ...teamToday, excludedInactiveCount: 0 });
  });

  it('downloads personal and team workbooks with authenticated API requests', async () => {
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test-download');
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    createPanel(true);
    flushInitialManagerReports();
    fixture.detectChanges();

    const personalExport: HTMLButtonElement =
      fixture.nativeElement.querySelector('[data-personal-export]');
    personalExport.click();
    const personalRequest = httpTesting.expectOne(
      (candidate) => candidate.url === '/api/time-logs/export',
    );
    expect(personalRequest.request.params.get('startDate')).toBe('2026-08-06');
    personalRequest.flush(new Blob(['personal']), {
      headers: new HttpHeaders({
        'Content-Disposition': 'attachment; filename="my-time-logs.xlsx"',
      }),
    });

    const teamExport: HTMLButtonElement = fixture.nativeElement.querySelector('[data-team-export]');
    teamExport.click();
    const teamRequest = httpTesting.expectOne(
      (candidate) => candidate.url === '/api/team/time-logs/export',
    );
    expect(teamRequest.request.params.get('includeInactive')).toBe('false');
    teamRequest.flush(new Blob(['team']), {
      headers: new HttpHeaders({
        'Content-Disposition': 'attachment; filename="team-time-logs.xlsx"',
      }),
    });

    await Promise.resolve();
    expect(createObjectUrl).toHaveBeenCalledTimes(2);
    expect(revokeObjectUrl).toHaveBeenCalledTimes(2);
  });

  it('shows a safe API error without removing the last successful report', () => {
    createPanel(false);
    expectPersonalReportRequest().flush(personalToday);
    fixture.detectChanges();

    const monthButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-personal-preset="month"]',
    );
    monthButton.click();
    expectPersonalReportRequest().flush(
      { code: 'REPORT_UNAVAILABLE', message: 'The report is temporarily unavailable.' },
      { status: 503, statusText: 'Unavailable' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('The report is temporarily unavailable.');
    expect(fixture.nativeElement.textContent).toContain('8h 5m 25s');
  });

  function createPanel(isManager: boolean): void {
    fixture = TestBed.createComponent(TimeInsightsPanel);
    fixture.componentRef.setInput('isManager', isManager);
    fixture.detectChanges();
  }

  function flushInitialManagerReports(): void {
    expectPersonalReportRequest().flush(personalToday);
    expectTeamReportRequest().flush(teamToday);
  }

  function expectPersonalReportRequest() {
    return httpTesting.expectOne((candidate) => candidate.url === '/api/time-logs/report');
  }

  function expectTeamReportRequest(includeInactive = false) {
    const request = httpTesting.expectOne((candidate) => candidate.url === '/api/team/report');
    expect(request.request.params.get('includeInactive')).toBe(String(includeInactive));
    return request;
  }

  function setInputValue(selector: string, value: string): void {
    const input: HTMLInputElement = fixture.nativeElement.querySelector(selector);
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }
});
