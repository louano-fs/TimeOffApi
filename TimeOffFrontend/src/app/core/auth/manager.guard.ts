import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const managerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.session()?.role === 'Manager' ? true : inject(Router).createUrlTree(['/']);
};
