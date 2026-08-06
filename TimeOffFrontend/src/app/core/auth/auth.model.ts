export interface LoginRequest {
  email: string;
  password: string;
}

export type UserRole = 'Employee' | 'Manager' | 'Administrator';

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  userId: number;
  employeeId: number;
  employeeNumber: string;
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
}
