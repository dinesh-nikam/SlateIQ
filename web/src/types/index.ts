export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  activeTenantId: string;
  roles: string[];
}

export interface Money {
  amount: string;
  currency: string;
}

export interface ShowSummary {
  id: string;
  code: string;
  title: string;
  status: 'PLANNING' | 'ACTIVE' | 'COMPLETED' | 'CLOSING' | 'CLOSED' | 'ARCHIVED';
  totalBudget: Money;
  totalActual: Money;
  totalCommitted: Money;
  variance: Money;
}
