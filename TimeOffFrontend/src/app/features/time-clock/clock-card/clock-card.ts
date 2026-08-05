import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, timer } from 'rxjs';

import { ClockAction, ClockStatusResponse } from '../../../shared/models/clock.model';
import { ClockDurationPipe } from '../../../shared/pipes/clock-duration-pipe';

interface ClockBaseline {
  status: ClockStatusResponse;
  receivedAtMilliseconds: number;
  dayEndsAfterMilliseconds: number;
}

@Component({
  selector: 'app-clock-card',
  imports: [ClockDurationPipe],
  templateUrl: './clock-card.html',
  styleUrl: './clock-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClockCard {
  readonly status = input<ClockStatusResponse | null>(null);
  readonly isLoading = input(false);
  readonly pendingAction = input<ClockAction | null>(null);
  readonly errorMessage = input<string | null>(null);

  readonly actionRequested = output<ClockAction>();
  readonly refreshRequested = output<void>();

  private readonly destroyRef = inject(DestroyRef);
  private readonly nowMilliseconds = signal(Date.now());
  private readonly clockBaseline = signal<ClockBaseline | null>(null);
  private dayBoundaryTimer: Subscription | null = null;

  protected readonly liveWorkedSeconds = computed(() => this.liveSeconds('worked'));
  protected readonly liveBreakSeconds = computed(() => this.liveSeconds('break'));

  constructor() {
    effect(() => {
      const status = this.status();

      untracked(() => {
        const receivedAtMilliseconds = Date.now();
        this.cancelDayBoundaryTimer();
        this.nowMilliseconds.set(receivedAtMilliseconds);

        if (!status) {
          this.clockBaseline.set(null);
          return;
        }

        const dayEndsAfterMilliseconds =
          Date.parse(status.currentDayEndsAt) - Date.parse(status.asOf);
        const safeDayEndsAfterMilliseconds = Number.isFinite(dayEndsAfterMilliseconds)
          ? Math.max(0, dayEndsAfterMilliseconds)
          : Number.POSITIVE_INFINITY;
        this.clockBaseline.set({
          status,
          receivedAtMilliseconds,
          dayEndsAfterMilliseconds: safeDayEndsAfterMilliseconds,
        });

        if (Number.isFinite(safeDayEndsAfterMilliseconds)) {
          this.dayBoundaryTimer = timer(safeDayEndsAfterMilliseconds)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(() => this.refreshRequested.emit());
        }
      });
    });

    timer(1_000, 1_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.nowMilliseconds.set(Date.now()));
  }

  protected requestAction(action: ClockAction): void {
    if (this.pendingAction() !== null) {
      return;
    }

    this.actionRequested.emit(action);
  }

  private liveSeconds(type: 'worked' | 'break'): number {
    const baseline = this.clockBaseline();

    if (!baseline) {
      return 0;
    }

    const elapsedSeconds = Math.max(
      0,
      Math.floor((this.nowMilliseconds() - baseline.receivedAtMilliseconds) / 1_000),
    );
    const shouldAdvance =
      (type === 'worked' && baseline.status.status === 'Working') ||
      (type === 'break' && baseline.status.status === 'OnBreak');
    const baselineSeconds =
      type === 'worked' ? baseline.status.workedSecondsToday : baseline.status.breakSecondsToday;

    if (
      this.nowMilliseconds() - baseline.receivedAtMilliseconds >=
      baseline.dayEndsAfterMilliseconds
    ) {
      const secondsSinceDayStart = Math.max(
        0,
        Math.floor(
          (this.nowMilliseconds() -
            baseline.receivedAtMilliseconds -
            baseline.dayEndsAfterMilliseconds) /
            1_000,
        ),
      );

      return shouldAdvance ? secondsSinceDayStart : 0;
    }

    return baselineSeconds + (shouldAdvance ? elapsedSeconds : 0);
  }

  private cancelDayBoundaryTimer(): void {
    this.dayBoundaryTimer?.unsubscribe();
    this.dayBoundaryTimer = null;
  }
}
