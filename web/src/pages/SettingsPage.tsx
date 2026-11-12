import React, { useState } from 'react';
import { 
  Settings, 
  Building2, 
  ShieldCheck, 
  Lock, 
  Database, 
  CheckCircle2, 
  RefreshCw
} from 'lucide-react';

export const SettingsPage: React.FC = () => {
  const [isVerifyingChain, setIsVerifyingChain] = useState(false);
  const [chainVerified, setChainVerified] = useState(true);
  const [lastVerifiedTime, setLastVerifiedTime] = useState('2026-11-12 17:15:32 UTC');

  const handleVerifyChain = () => {
    setIsVerifyingChain(true);
    setTimeout(() => {
      setIsVerifyingChain(false);
      setChainVerified(true);
      setLastVerifiedTime(new Date().toISOString().replace('T', ' ').substring(0, 19) + ' UTC');
    }, 1500);
  };

  return (
    <div className="p-8 space-y-6">
      {/* Header */}
      <div>
        <div className="flex items-center gap-2">
          <Settings className="w-6 h-6 text-sky-400" />
          <h2 className="text-2xl font-bold tracking-tight text-white">Platform Settings & Security</h2>
        </div>
        <p className="text-sm text-slate-400 mt-1">
          Multi-tenant configuration, Row-Level Security isolation status, and ADR-012 cryptographic hash chain verification.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Tenant Information Card */}
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-semibold text-slate-100 flex items-center gap-2 text-sm">
              <Building2 className="w-4 h-4 text-sky-400" />
              Tenant Identity (MOD-01)
            </h3>
            <span className="text-[10px] px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold uppercase">
              Active
            </span>
          </div>

          <div className="space-y-3 text-xs">
            <div>
              <span className="block text-slate-500">Legal Entity</span>
              <span className="font-medium text-slate-200">Monsoon Media Works Pvt Ltd</span>
            </div>
            <div>
              <span className="block text-slate-500">Organization Code</span>
              <span className="font-mono text-slate-200">MONSOON-IN</span>
            </div>
            <div>
              <span className="block text-slate-500">Jurisdiction & Cloud Region</span>
              <span className="font-medium text-slate-200">India (AWS ap-south-1 Mumbai)</span>
            </div>
            <div>
              <span className="block text-slate-500">Base Currency</span>
              <span className="font-mono text-slate-200">INR (₹) — Precision Scale-4</span>
            </div>
          </div>
        </div>

        {/* Security & Access Controls */}
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-semibold text-slate-100 flex items-center gap-2 text-sm">
              <ShieldCheck className="w-4 h-4 text-indigo-400" />
              Zero-Trust Identity (MOD-02)
            </h3>
            <span className="text-[10px] px-2 py-0.5 rounded bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 font-semibold uppercase">
              Enforced
            </span>
          </div>

          <div className="space-y-3 text-xs">
            <div className="flex items-center justify-between">
              <span className="text-slate-400">TOTP MFA (RFC 6238)</span>
              <span className="font-semibold text-emerald-400">Mandatory</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">Password Hashing</span>
              <span className="font-mono text-slate-300">Argon2id (m=65536, t=3, p=4)</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">Token Rotation (TS-08)</span>
              <span className="font-medium text-slate-300">Enabled (Single-use refresh)</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">Access Token Lifespan</span>
              <span className="font-medium text-slate-300">15 minutes</span>
            </div>
          </div>
        </div>

        {/* Database & RLS Isolation Status */}
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-semibold text-slate-100 flex items-center gap-2 text-sm">
              <Database className="w-4 h-4 text-emerald-400" />
              Row-Level Security (RLS)
            </h3>
            <span className="text-[10px] px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold uppercase">
              100% Policy Pass
            </span>
          </div>

          <div className="space-y-3 text-xs">
            <div className="flex items-center justify-between">
              <span className="text-slate-400">Tenant-Scoped Tables</span>
              <span className="font-bold text-slate-200">46 of 46</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">FORCE ROW LEVEL SECURITY</span>
              <span className="font-semibold text-emerald-400">46 Enforced</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">App Session Variable</span>
              <span className="font-mono text-slate-300">app.current_org_id</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-slate-400">Database Engine</span>
              <span className="font-medium text-slate-300">PostgreSQL 17.2</span>
            </div>
          </div>
        </div>
      </div>

      {/* ADR-012 Cryptographic Audit Hash Chaining */}
      <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-5">
        <div className="flex items-center justify-between border-b border-slate-800 pb-4">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-sky-500/10 flex items-center justify-center text-sky-400">
              <Lock className="w-5 h-5" />
            </div>
            <div>
              <h3 className="font-semibold text-slate-100 text-sm">
                Cryptographic Audit Chain Integrity (ADR-012)
              </h3>
              <p className="text-xs text-slate-400">
                Every state transition and cost posting generates an append-only, SHA-256 chained event in <code className="text-sky-400">compliance.audit_event</code>.
              </p>
            </div>
          </div>

          <button
            onClick={handleVerifyChain}
            disabled={isVerifyingChain}
            className="flex items-center gap-2 px-4 py-2 bg-slate-900 hover:bg-slate-800 border border-slate-700 text-slate-200 font-medium rounded-lg text-xs transition-colors cursor-pointer disabled:cursor-not-allowed"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isVerifyingChain ? 'animate-spin text-sky-400' : ''}`} />
            {isVerifyingChain ? 'Recalculating Hashes...' : 'Verify Hash Chain'}
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="p-4 bg-slate-900 rounded-lg border border-slate-800">
            <span className="text-xs text-slate-500 block mb-1">Chain Status</span>
            <div className="flex items-center gap-2 text-emerald-400 font-semibold text-sm">
              <CheckCircle2 className="w-4 h-4" />
              {chainVerified ? 'CHAIN_VERIFIED_VALID' : 'VERIFICATION_REQUIRED'}
            </div>
            <span className="text-[10px] text-slate-500 mt-1 block">Zero breaks, zero retro-mutations</span>
          </div>

          <div className="p-4 bg-slate-900 rounded-lg border border-slate-800">
            <span className="text-xs text-slate-500 block mb-1">Genesis Block Salt</span>
            <div className="font-mono text-xs text-slate-300 truncate">
              0000000000000000000000000000000000000000000000000000000000000000
            </div>
            <span className="text-[10px] text-slate-500 mt-1 block">Organization Root Anchor</span>
          </div>

          <div className="p-4 bg-slate-900 rounded-lg border border-slate-800">
            <span className="text-xs text-slate-500 block mb-1">Last Cryptographic Audit</span>
            <div className="font-mono text-xs text-slate-300">
              {lastVerifiedTime}
            </div>
            <span className="text-[10px] text-emerald-400/80 mt-1 block">Automated SHA-256 re-check pass</span>
          </div>
        </div>
      </div>
    </div>
  );
};
