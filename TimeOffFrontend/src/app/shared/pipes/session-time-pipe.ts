import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'sessionTime',
})
export class SessionTimePipe implements PipeTransform {
  transform(value: string | undefined, timeZone: string): string {
    if (!value) {
      return 'N/A';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return 'N/A';
    }

    try {
      return new Intl.DateTimeFormat('en-US', {
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
        timeZone,
      }).format(date);
    } catch {
      return 'N/A';
    }
  }
}
