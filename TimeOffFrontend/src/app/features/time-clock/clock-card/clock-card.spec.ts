import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { ClockStatus, ClockStatusResponse } from '../../../shared/models/clock.model';
import { ClockCard } from './clock-card';

describe('ClockCard', () => {
  let component: ClockCard;
  let fixture: ComponentFixture<ClockCard>;

  beforeEach(async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-05T01:00:00Z'));

    await TestBed.configureTestingModule({
      imports: [ClockCard],
    }).compileComponents();

    fixture = TestBed.createComponent(ClockCard);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
    vi.useRealTimers();
  });

  it('shows only Clock In when clocked out', () => {
    setStatus('ClockedOut');

    expect(actionNames()).toEqual(['clockIn']);
  });

  it('shows Take Break and Clock Out while working', () => {
    setStatus('Working');

    expect(actionNames()).toEqual(['startBreak', 'clockOut']);
  });

  it('shows only End Break while on break', () => {
    setStatus('OnBreak');

    expect(actionNames()).toEqual(['endBreak']);
  });

  it('emits the selected action', () => {
    let emittedAction: string | undefined;

    component.actionRequested.subscribe((action) => {
      emittedAction = action;
    });

    setStatus('ClockedOut');

    actionButtons()[0].click();

    expect(emittedAction).toBe('clockIn');
  });

  it('disables every action while one is pending', () => {
    fixture.componentRef.setInput('status', createStatus('Working'));
    fixture.componentRef.setInput('pendingAction', 'startBreak');
    fixture.detectChanges();

    expect(actionButtons().every((button) => button.disabled)).toBe(true);
  });

  it('updates worked time every second while working', () => {
    fixture.componentRef.setInput(
      'status',
      createStatus('Working', {
        workedSecondsToday: 3_723,
      }),
    );
    fixture.detectChanges();

    expect(liveTimer('worked')).toBe('01:02:03');

    vi.advanceTimersByTime(2_000);
    fixture.detectChanges();

    expect(liveTimer('worked')).toBe('01:02:05');
  });

  it('updates only break time while on break', () => {
    fixture.componentRef.setInput(
      'status',
      createStatus('OnBreak', {
        workedSecondsToday: 3_600,
        breakSecondsToday: 30,
      }),
    );
    fixture.detectChanges();

    vi.advanceTimersByTime(3_000);
    fixture.detectChanges();

    expect(liveTimer('worked')).toBe('01:00:00');
    expect(liveTimer('break')).toBe('00:00:33');
  });

  it('keeps completed totals still while clocked out', () => {
    fixture.componentRef.setInput(
      'status',
      createStatus('ClockedOut', {
        workedSecondsToday: 28_800,
        breakSecondsToday: 3_600,
      }),
    );
    fixture.detectChanges();

    vi.advanceTimersByTime(5_000);
    fixture.detectChanges();

    expect(liveTimer('worked')).toBe('08:00:00');
    expect(liveTimer('break')).toBe('01:00:00');
  });

  it('resets at the server-provided local day boundary and requests status', () => {
    let refreshCount = 0;
    component.refreshRequested.subscribe(() => refreshCount++);
    fixture.componentRef.setInput(
      'status',
      createStatus('Working', {
        asOf: '2026-08-05T15:59:58Z',
        currentDayEndsAt: '2026-08-05T16:00:00Z',
        workedSecondsToday: 28_800,
      }),
    );
    fixture.detectChanges();

    vi.advanceTimersByTime(3_000);
    fixture.detectChanges();

    expect(liveTimer('worked')).toBe('00:00:01');
    expect(liveTimer('break')).toBe('00:00:00');
    expect(refreshCount).toBe(1);
  });

  function setStatus(status: ClockStatus): void {
    fixture.componentRef.setInput('status', createStatus(status));

    fixture.detectChanges();
  }

  function createStatus(
    status: ClockStatus,
    overrides: Partial<ClockStatusResponse> = {},
  ): ClockStatusResponse {
    return {
      status,
      asOf: '2026-08-05T01:00:00Z',
      currentDayEndsAt: '2026-08-05T16:00:00Z',
      workedMinutesToday: 480,
      breakMinutesToday: 60,
      workedSecondsToday: 28_800,
      breakSecondsToday: 3_600,
      ...overrides,
    };
  }

  function liveTimer(type: 'worked' | 'break'): string {
    return fixture.nativeElement.querySelector(`[data-live-timer="${type}"]`).textContent.trim();
  }

  function actionButtons(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[data-clock-action]'));
  }

  function actionNames(): string[] {
    return actionButtons().map((button) => button.getAttribute('data-clock-action') ?? '');
  }
});
