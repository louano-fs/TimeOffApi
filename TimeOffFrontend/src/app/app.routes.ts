import { Routes } from '@angular/router';
import { DashboardPage } from './features/dashboard/dashboard-page/dashboard-page';
import { managerGuard } from './core/auth/manager.guard';
import { TeamMemberTimeLogsPage } from './features/team/team-member-time-logs-page/team-member-time-logs-page';

export const routes: Routes = [
  {
    path: '',
    component: DashboardPage,
  },
  {
    path: 'team/:userId/time-logs',
    component: TeamMemberTimeLogsPage,
    canActivate: [managerGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
