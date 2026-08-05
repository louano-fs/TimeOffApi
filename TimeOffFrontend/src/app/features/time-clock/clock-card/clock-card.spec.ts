import { ComponentFixture, TestBed } from '@angular/core/testing';

import {
  ClockStatus,
  ClockStatusResponse,
} from '../../../shared/models/clock.model';
import { ClockCard } from './clock-card';

describe('ClockCard', () => {
  let component: ClockCard;
  let fixture: ComponentFixture<ClockCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClockCard],
    }).compileComponents();

    fixture = TestBed.createComponent(ClockCard);
    component = fixture.componentInstance;
  });

  it('shows only Clock In when clocked out', () => {
    setStatus('ClockedOut');

    expect(actionNames()).toEqual(['clockIn']);
  });

  it('shows Take Break and Clock Out while working', () => {
    setStatus('Working');

    expect(actionNames()).toEqual([
      'startBreak',
      'clockOut',
    ]);
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
    fixture.componentRef.setInput(
      'status',
      createStatus('Working'),
    );
    fixture.componentRef.setInput(
      'pendingAction',
      'startBreak',
    );
    fixture.detectChanges();

    expect(
      actionButtons().every((button) => button.disabled),
    ).toBe(true);
  });

  function setStatus(status: ClockStatus): void {
    fixture.componentRef.setInput(
      'status',
      createStatus(status),
    );

    fixture.detectChanges();
  }

  function createStatus(
    status: ClockStatus,
  ): ClockStatusResponse {
    return {
      status,
      workedMinutesToday: 480,
      breakMinutesToday: 60,
    };
  }

  function actionButtons(): HTMLButtonElement[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll(
        '[data-clock-action]',
      ),
    );
  }

  function actionNames(): string[] {
    return actionButtons().map(
      (button) =>
        button.getAttribute('data-clock-action') ?? '',
    );
  }
});