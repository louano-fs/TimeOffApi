import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'clockDuration',
})
export class ClockDurationPipe implements PipeTransform {
  transform(totalSeconds: number | null | undefined): string {
    const safeSeconds = Math.max(0, Math.floor(totalSeconds ?? 0));
    const hours = Math.floor(safeSeconds / 3_600);
    const minutes = Math.floor((safeSeconds % 3_600) / 60);
    const seconds = safeSeconds % 60;

    return [hours, minutes, seconds].map((value) => value.toString().padStart(2, '0')).join(':');
  }
}
