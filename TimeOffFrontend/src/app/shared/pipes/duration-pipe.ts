import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'duration',
})
export class DurationPipe implements PipeTransform {
  transform(totalMinutes: number | null | undefined): string {
    const safeMinutes = Math.max(
      0,
      Math.floor(totalMinutes ?? 0),
    );

    const hours = Math.floor(safeMinutes / 60);
    const minutes = safeMinutes % 60;

    if (hours === 0) {
      return `${minutes}m`;
    }

    if (minutes === 0) {
      return `${hours}h`;
    }

    return `${hours}h ${minutes}m`;
  }
}