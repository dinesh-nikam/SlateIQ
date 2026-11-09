import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldCheck, Lock, Mail, KeyRound } from 'lucide-react';

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const [email, setEmail] = useState('admin@monsoonmedia.local');
  const [password, setPassword] = useState('SlateIQ@2026!');
  const [mfaStep, setMfaStep] = useState(false);
  const [totpCode, setTotpCode] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!mfaStep && email.includes('admin')) {
      // Privileged role triggers MFA step (NFR-SEC-003, ADR-047)
      setMfaStep(true);
      return;
    }
    // Navigate to dashboard on success
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-950 flex flex-col justify-center items-center p-4">
      <div className="w-full max-w-md bg-slate-900 border border-slate-800 p-8 rounded-2xl shadow-xl">
        <div className="text-center mb-8">
          <div className="w-12 h-12 bg-gradient-to-tr from-sky-600 to-indigo-500 rounded-xl mx-auto flex items-center justify-center font-bold text-white text-xl shadow-lg mb-3">
            S
          </div>
          <h1 className="text-2xl font-bold text-white tracking-tight">SlateIQ</h1>
          <p className="text-xs text-slate-400 mt-1 uppercase tracking-wider font-medium">Production Cost Intelligence</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-5">
          {!mfaStep ? (
            <>
              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1.5">Corporate Email</label>
                <div className="relative">
                  <Mail className="w-4 h-4 text-slate-500 absolute left-3 top-3" />
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg pl-9 pr-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500 transition-colors"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1.5">Password</label>
                <div className="relative">
                  <Lock className="w-4 h-4 text-slate-500 absolute left-3 top-3" />
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg pl-9 pr-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500 transition-colors"
                  />
                </div>
              </div>
            </>
          ) : (
            <div className="p-4 bg-slate-950 rounded-xl border border-sky-500/30 space-y-4">
              <div className="flex items-center gap-2 text-sky-400 text-xs font-semibold">
                <ShieldCheck className="w-4 h-4" />
                <span>Multi-Factor Authentication Required</span>
              </div>
              <p className="text-xs text-slate-400">
                Privileged access role detected. Enter the 6-digit TOTP code from your authenticator app.
              </p>
              <div className="relative">
                <KeyRound className="w-4 h-4 text-slate-500 absolute left-3 top-3" />
                <input
                  type="text"
                  maxLength={6}
                  placeholder="123456"
                  value={totpCode}
                  onChange={(e) => setTotpCode(e.target.value)}
                  required
                  autoFocus
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg pl-9 pr-3 py-2 text-center text-lg tracking-widest font-mono text-white focus:outline-none focus:border-sky-500"
                />
              </div>
            </div>
          )}

          <button
            type="submit"
            className="w-full bg-sky-600 hover:bg-sky-500 text-white font-medium py-2.5 rounded-lg text-sm transition-colors shadow-md shadow-sky-950"
          >
            {mfaStep ? 'Verify & Sign In' : 'Sign In'}
          </button>
        </form>

        <div className="mt-6 pt-6 border-t border-slate-800 text-center">
          <span className="text-[11px] text-slate-500">
            Enterprise Single-Tenant Isolation &bull; India (ap-south-1)
          </span>
        </div>
      </div>
    </div>
  );
};
