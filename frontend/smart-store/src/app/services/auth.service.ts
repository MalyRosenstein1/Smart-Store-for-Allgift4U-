import { Injectable } from '@angular/core';

const USERS: Record<string, string> = {
  admin:  '0534186791',
  yossi:  'Staff123!',
  michal: 'Rose456@',
  david:  'Store789#',
  noa:    'Gift2024$'
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _loggedIn = false;
  private _username = '';

  login(username: string, password: string): boolean {
    if (USERS[username] && USERS[username] === password) {
      this._loggedIn = true;
      this._username = username;
      return true;
    }
    return false;
  }

  logout(): void {
    this._loggedIn = false;
    this._username = '';
  }

  get isLoggedIn(): boolean { return this._loggedIn; }
  get username(): string    { return this._username; }
}
