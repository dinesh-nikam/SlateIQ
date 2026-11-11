/**
 * SlateIQ Production Cost Intelligence — Frontend API Client
 * Adheres to RFC 9457 ProblemDetails, RFC 7807, and TS-08 Token Rotation
 */

export interface LoginParams {
  email: string;
  password: string;
  tenantCode: string;
  deviceFingerprint?: string;
}

export interface MfaParams {
  mfaChallengeToken: string;
  totpCode: string;
  deviceFingerprint?: string;
}

export interface UserSession {
  id: string;
  orgId: string;
  email: string;
  fullName: string;
  role: string;
}

export interface AuthSuccessResult {
  mfaRequired: false;
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: UserSession;
}

export interface MfaRequiredResult {
  mfaRequired: true;
  mfaChallengeToken: string;
}

export type LoginResult = AuthSuccessResult | MfaRequiredResult;

export class ApiError extends Error {
  public status: number;
  public errorCode?: string;

  constructor(status: number, message: string, errorCode?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errorCode = errorCode;
  }
}

const ACCESS_TOKEN_KEY = 'slateiq_access_token';
const REFRESH_TOKEN_KEY = 'slateiq_refresh_token';
const USER_KEY = 'slateiq_user';

export const tokenStorage = {
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  },
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  },
  getUser(): UserSession | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  },
  setSession(auth: AuthSuccessResult): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
  },
  clear(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }
};

export const api = {
  async login(params: LoginParams): Promise<LoginResult> {
    const response = await fetch('/api/v1/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(params),
    });

    if (!response.ok) {
      const errorJson = await response.json().catch(() => ({}));
      throw new ApiError(response.status, errorJson.message || 'Login failed', errorJson.error);
    }

    const data = await response.json();
    if (data.mfaRequired) {
      return {
        mfaRequired: true,
        mfaChallengeToken: data.mfaChallengeToken,
      };
    }

    const successResult: AuthSuccessResult = {
      mfaRequired: false,
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      expiresIn: data.expiresIn,
      user: data.user,
    };

    tokenStorage.setSession(successResult);
    return successResult;
  },

  async verifyMfa(params: MfaParams): Promise<AuthSuccessResult> {
    const response = await fetch('/api/v1/auth/mfa/challenge', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(params),
    });

    if (!response.ok) {
      const errorJson = await response.json().catch(() => ({}));
      throw new ApiError(response.status, errorJson.message || 'MFA challenge failed', errorJson.error);
    }

    const data = await response.json();
    const result: AuthSuccessResult = {
      mfaRequired: false,
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      expiresIn: data.expiresIn,
      user: data.user,
    };

    tokenStorage.setSession(result);
    return result;
  },

  async refreshToken(): Promise<string> {
    const currentRefreshToken = tokenStorage.getRefreshToken();
    if (!currentRefreshToken) {
      throw new ApiError(401, 'No refresh token available');
    }

    const response = await fetch('/api/v1/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: currentRefreshToken }),
    });

    if (!response.ok) {
      tokenStorage.clear();
      throw new ApiError(response.status, 'Session expired; please log in again');
    }

    const data = await response.json();
    localStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
    return data.accessToken;
  },

  async logout(): Promise<void> {
    const refreshToken = tokenStorage.getRefreshToken();
    if (refreshToken) {
      try {
        await fetch('/api/v1/auth/logout', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        });
      } catch {
        // Ignore network errors during logout
      }
    }
    tokenStorage.clear();
  },

  async fetchWithAuth(url: string, options: RequestInit = {}): Promise<Response> {
    let token = tokenStorage.getAccessToken();

    const headers = new Headers(options.headers || {});
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    let response = await fetch(url, { ...options, headers });

    // Handle token expiration & automatic refresh
    if (response.status === 401 && tokenStorage.getRefreshToken()) {
      try {
        token = await this.refreshToken();
        headers.set('Authorization', `Bearer ${token}`);
        response = await fetch(url, { ...options, headers });
      } catch {
        // Refresh failed, let 401 propagate
      }
    }

    return response;
  }
};
