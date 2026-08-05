import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';

import {
  ClockAction,
  ClockStatusResponse,
} from '../../../shared/models/clock.model';
import { DurationPipe } from '../../../shared/pipes/duration-pipe';

@Component({
  selector: 'app-clock-card',
  imports: [DurationPipe],
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

  protected requestAction(action: ClockAction): void {
    if (this.pendingAction() !== null) {
      return;
    }

    this.actionRequested.emit(action);
  }
}