import { Component, input, output } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';

import { LoginRequest } from '../../../core/auth/auth.model';

@Component({
  selector: 'app-login-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './login-panel.html',
  styleUrl: './login-panel.css',
})

export class LoginPanel {
  readonly isSubmitting = input(false);
  readonly errorMessage = input<string | null>(null);

  readonly loginSubmitted = output<LoginRequest>();

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.email,
      ],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  submit(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.loginSubmitted.emit(this.form.getRawValue());
  }
}