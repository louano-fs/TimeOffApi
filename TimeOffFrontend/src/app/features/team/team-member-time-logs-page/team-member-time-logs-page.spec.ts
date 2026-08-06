import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { AuthResponse } from '../../../core/auth/auth.model';
import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { TeamMemberTimeLogsPage } from './team-member-time-logs-page';

describe('TeamMemberTimeLogsPage', () => {
  let fixture: ComponentFixture<TeamMemberTimeLogsPage>;
  let httpTesting: HttpTestingController;

  const managerSession: AuthResponse = {
    accessToken: 'test-token',
    expiresAt: '2099-08-05T00:00:00Z',
    userId: 8,
    employeeId: 8000,
    employeeNumber: 'MGR-DEV',
    email: 'manager@example.com',
    firstName: 'Morgan',
    lastName: 'Manager',
    role: 'Manager',
  };

  const timeLogs: PagedResponse<WorkSessionResponse> = {
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  };

  beforeEach(async () => {
    sessionStorage.setItem('time-clock-session', JSON.stringify(managerSession));
    await TestBed.configureTestingModule({
      imports: [TeamMemberTimeLogsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ userId: '21' }) } },
        },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('loads the selected team member on its dedicated URL page', () => {
    fixture = TestBed.createComponent(TeamMemberTimeLogsPage);
    fixture.detectChanges();

    httpTesting.expectOne('/api/team').flush([
      {
        userId: 21,
        employeeId: 1001,
        employeeNumber: 'EMP-1001',
        email: 'employee@example.com',
        firstName: 'Taylor',
        lastName: 'Employee',
        isActive: true,
      },
    ]);
    const logsRequest = httpTesting.expectOne(
      (candidate) => candidate.url === '/api/team/21/time-logs',
    );
    expect(logsRequest.request.params.get('page')).toBe('1');
    expect(logsRequest.request.params.get('pageSize')).toBe('20');
    logsRequest.flush(timeLogs);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Taylor Employee time logs');
    expect(fixture.nativeElement.textContent).toContain('No time logs yet');
  });
});
