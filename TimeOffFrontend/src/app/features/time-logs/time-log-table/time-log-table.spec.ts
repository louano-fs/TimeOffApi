import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PagedResponse, WorkSessionResponse } from '../../../shared/models/time-log.model';
import { TimeLogTable } from './time-log-table';

describe('TimeLogTable', () => {
  let component: TimeLogTable;
  let fixture: ComponentFixture<TimeLogTable>;

  const response: PagedResponse<WorkSessionResponse> = {
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
        breaks: [
          {
            id: 102,
            start: '2026-08-05T04:00:00Z',
            end: '2026-08-05T05:00:00Z',
            durationMinutes: 60,
          },
        ],
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 21,
    totalPages: 2,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeLogTable],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeLogTable);
    component = fixture.componentInstance;
  });

  it('renders a work session', () => {
    fixture.componentRef.setInput('timeLogs', response);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;

    expect(text).toContain('Work');
    expect(text).toContain('8h');
    expect(text).toContain('Completed');
  });

  it('expands the session breaks', () => {
    fixture.componentRef.setInput('timeLogs', response);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-break-row]')).toBeNull();

    const expandButton: HTMLButtonElement =
      fixture.nativeElement.querySelector('[data-expand-session]');

    expandButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-break-row]')).not.toBeNull();
  });

  it('requests the next page', () => {
    let requestedPage: number | undefined;

    component.pageRequested.subscribe((page) => {
      requestedPage = page;
    });

    fixture.componentRef.setInput('timeLogs', response);
    fixture.detectChanges();

    const nextButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-page-action="next"]',
    );

    nextButton.click();

    expect(requestedPage).toBe(2);
  });

  it('renders the empty state', () => {
    fixture.componentRef.setInput('timeLogs', {
      ...response,
      items: [],
      totalCount: 0,
      totalPages: 0,
    });

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No time logs yet');
  });
});
