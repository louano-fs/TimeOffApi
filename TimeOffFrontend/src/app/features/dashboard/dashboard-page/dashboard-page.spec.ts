import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AuthResponse } from '../../../core/auth/auth.model';
import { ClockStatusResponse, TimeLogResponse } from '../../../shared/models/clock.model';
import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { DashboardPage } from './dashboard-page';

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let httpTesting: HttpTestingController;

  const authenticatedSession: AuthResponse = {
    accessToken: 'test-token',
    expiresAt: '2099-08-05T00:00:00Z',
    userId: 1,
    employeeId: 1001,
    employeeNumber: 'EMP-1001',
    email: 'employee@example.com',
    firstName: 'Test',
    lastName: 'Employee',
    role: 'Employee',
  };

  const clockedOutStatus: ClockStatusResponse = {
    status: 'ClockedOut',
    workedMinutesToday: 480,
    breakMinutesToday: 60,
  };

  const timeLogs: PagedResponse<WorkSessionResponse> = {
    items: [
      {
        id: 101,
        userId: 1,
        employeeId: 1001,
        shiftDate: '2026-08-05',
        start: '2026-08-05T00:00:00Z',
        end: '2026-08-05T09:00:00Z',
        status: 'Completed',
        timezone: 'Asia/Manila',
        totalElapsedMinutes: 540,
        totalBreakMinutes: 60,
        totalWorkedMinutes: 480,
        breaks: [],
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  };

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('shows the login panel when unauthenticated', () => {
    createDashboard();

    expect(fixture.nativeElement.textContent).toContain('Sign in to your time clock');
  });

  it('loads status and time logs for an authenticated employee', () => {
    storeAuthenticatedSession();
    createDashboard();

    flushDashboardRequests();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Clocked Out');
    expect(fixture.nativeElement.textContent).toContain('Time Logs');
    expect(fixture.nativeElement.textContent).toContain('Aug 5, 2026');

    fixture.destroy();
  });

  it('refreshes status and logs after a clock action', () => {
    storeAuthenticatedSession();
    createDashboard();
    flushDashboardRequests();
    fixture.detectChanges();

    const clockInButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-clock-action="clockIn"]',
    );
    clockInButton.click();

    const actionRequest = httpTesting.expectOne('/api/time-clock/clock-in');
    expect(actionRequest.request.method).toBe('POST');
    expect(Object.keys(actionRequest.request.body)).toEqual(['dateTime']);
    expect(Number.isNaN(Date.parse(actionRequest.request.body.dateTime))).toBe(false);

    const actionResponse: TimeLogResponse = {
      id: 102,
      type: 'Work',
      status: 'Working',
      shiftDate: '2026-08-05',
      start: actionRequest.request.body.dateTime,
      timezone: 'Asia/Manila',
      workedMinutes: 0,
    };
    actionRequest.flush(actionResponse);

    const workingStatus: ClockStatusResponse = {
      ...clockedOutStatus,
      status: 'Working',
      activeWorkLogId: 102,
    };
    httpTesting.expectOne('/api/time-clock/status').flush(workingStatus);
    expectTimeLogsRequest().flush(timeLogs);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Working');

    fixture.destroy();
  });

  function createDashboard(): void {
    fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
  }

  function storeAuthenticatedSession(): void {
    sessionStorage.setItem('time-clock-session', JSON.stringify(authenticatedSession));
  }

  function flushDashboardRequests(): void {
    httpTesting.expectOne('/api/time-clock/status').flush(clockedOutStatus);
    expectTimeLogsRequest().flush(timeLogs);
  }

  function expectTimeLogsRequest() {
    const request = httpTesting.expectOne((candidate) => candidate.url === '/api/time-logs');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    return request;
  }
});
