import { HttpErrorResponse } from '@angular/common/http';
import { 
  ChangeDetectionStrategy,
  Component,
  inject,
  signal
} from '@angular/core';
import { finalize } from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { LoginRequest } from '../../../core/auth/auth.model';
import { AuthService } from '../../../core/auth/auth.service';
import { LoginPanel } from '../../login/login-panel/login-panel';

@Component({
  selector: 'app-dashboard-page',
  imports: [LoginPanel],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly authService = inject(AuthService);

  protected readonly isLoggingIn = signal(false);
  protected readonly loginError = signal<string | null>(null);

  protected login(request: LoginRequest): void {
    this.loginError.set(null);
    this.isLoggingIn.set(true);

    this.authService
      .login(request)
      .pipe(finalize(() => this.isLoggingIn.set(false)))
      .subscribe({
        error: (error: unknown) => {
          this.loginError.set(this.getErrorMessage(error));
        }
      })
  }

  protected logout(): void {
    this.authService.logout();
    this.loginError.set(null);
  }

  private getErrorMessage(error: unknown): string {
    if (
      error instanceof HttpErrorResponse &&
      this.isApiError(error.error)
    ) {
      return error.error.message;
    }
    return 'Unable to sign in. Check that the API is running and try again.';
  }

  private isApiError(value: unknown): value is ApiError {
    return (
      typeof value === 'object' &&
      value !== null &&
      'message' in value &&
      typeof value.message === 'string'
    );
  }
}
