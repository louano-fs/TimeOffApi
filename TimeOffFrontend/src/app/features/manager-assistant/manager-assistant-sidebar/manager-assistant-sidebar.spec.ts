import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ManagerAssistantSidebar } from './manager-assistant-sidebar';

describe('ManagerAssistantSidebar', () => {
  let fixture: ComponentFixture<ManagerAssistantSidebar>;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManagerAssistantSidebar],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ManagerAssistantSidebar);
    fixture.detectChanges();
  });

  afterEach(() => httpTesting.verify());

  it('stays visible for a manager when provider capability is disabled', () => {
    flushCapabilities(false);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('aside')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Team assistant');
  });

  it('sends a manager question and renders verified threshold evidence', () => {
    flushCapabilities(true);
    fixture.detectChanges();

    const textarea: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    textarea.value = 'Who worked less than 8 hours today?';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit'));

    const request = httpTesting.expectOne('/api/manager-assistant/messages');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      message: 'Who worked less than 8 hours today?',
      history: [],
    });
    request.flush({
      messageId: 'assistant-1',
      answer: 'One employee is below eight hours today.',
      asOf: '2026-08-07T04:00:00Z',
      parts: [
        {
          type: 'teamWorkedTimeThreshold',
          startDate: '2026-08-07',
          endDate: '2026-08-07',
          thresholdSeconds: 28_800,
          matchingMemberCount: 1,
          members: [
            {
              employeeNumber: 'EMP-1001',
              displayName: 'Taylor Employee',
              isActive: true,
              workedSeconds: 18_000,
              breakSeconds: 1_800,
              clockStatus: 'ClockedOut',
              rank: null,
            },
          ],
        },
      ],
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('One employee is below eight hours today.');
    expect(fixture.nativeElement.textContent).toContain('Taylor Employee');
    expect(fixture.nativeElement.textContent).toContain('5h');
  });

  it('downloads an export returned by the assistant', () => {
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:assistant-export');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    flushCapabilities(true);
    fixture.detectChanges();

    const textarea: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    textarea.value = 'Export team logs for August 5 through August 21.';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    httpTesting.expectOne('/api/manager-assistant/messages').flush({
      messageId: 'assistant-export',
      answer: 'The workbook is ready.',
      asOf: '2026-08-07T04:00:00Z',
      parts: [
        {
          type: 'teamTimeLogExport',
          startDate: '2026-08-05',
          endDate: '2026-08-21',
          fileName: 'team-time-logs-2026-08-05-to-2026-08-21.xlsx',
          downloadUrl:
            '/api/team/time-logs/export?startDate=2026-08-05&endDate=2026-08-21&includeInactive=false&format=xlsx',
        },
      ],
    });
    fixture.detectChanges();

    const downloadButton = [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.includes('Download Excel'),
    ) as HTMLButtonElement;
    downloadButton.click();
    const downloadRequest = httpTesting.expectOne(
      (request) => request.url.startsWith('/api/team/time-logs/export?'),
    );
    expect(downloadRequest.request.responseType).toBe('blob');
    downloadRequest.flush(new Blob(['xlsx']), {
      headers: new HttpHeaders({
        'Content-Disposition':
          "attachment; filename*=UTF-8''team-time-logs-2026-08-05-to-2026-08-21.xlsx",
      }),
    });

    expect(createObjectUrl).toHaveBeenCalled();
  });

  function flushCapabilities(enabled: boolean): void {
    httpTesting.expectOne('/api/manager-assistant/capabilities').flush({
      enabled,
      audience: enabled ? 'Manager' : null,
      scope: enabled ? 'directReports' : null,
      streaming: false,
      maxMessageLength: 1_000,
    });
  }
});
