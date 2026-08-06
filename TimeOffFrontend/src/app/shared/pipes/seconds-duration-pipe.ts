import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'secondsDuration',
})
export class SecondsDurationPipe implements PipeTransform {
  transform(totalSeconds: number | null | undefined): string {
    const safeSeconds = Math.max(0, Math.floor(totalSeconds ?? 0));
    const hours = Math.floor(safeSeconds / 3_600);
    const minutes = Math.floor((safeSeconds % 3_600) / 60);
    const seconds = safeSeconds % 60;
    const parts: string[] = [];

    if (hours > 0) {
      parts.push(`${hours}h`);
    }
    if (minutes > 0) {
      parts.push(`${minutes}m`);
    }
    if (seconds > 0 || parts.length === 0) {
      parts.push(`${seconds}s`);
    }

    return parts.join(' ');
  }
}
