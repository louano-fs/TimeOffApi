import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TeamMember } from '../../../shared/models/team.model';

@Component({
  selector: 'app-team-section',
  imports: [RouterLink],
  templateUrl: './team-section.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamSection {
  readonly members = input<readonly TeamMember[] | null>(null);
  readonly isMembersLoading = input(false);
  readonly membersError = input<string | null>(null);

  readonly membersRefreshRequested = output<void>();
}
