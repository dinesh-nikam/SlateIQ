import React from 'react';
import { TrendingUp, AlertTriangle, CheckCircle2, Clock, DollarSign } from 'lucide-react';
import type { ShowSummary } from '../types';

export const DashboardPage: React.FC = () => {
  const shows: ShowSummary[] = [
    {
      id: '0193b2d4-0001',
      code: 'MON-S01',
      title: 'Monsoon Rhapsody (Season 1)',
      status: 'ACTIVE',
      totalBudget: { amount: '45,000,000.0000', currency: 'INR' },
      totalActual: { amount: '28,450,210.5000', currency: 'INR' },
      totalCommitted: { amount: '12,100,000.0000', currency: 'INR' },
      variance: { amount: '4,449,789.5000', currency: 'INR' },
    },
    {
      id: '0193b2d4-0002',
      code: 'KHO-F01',
      title: 'Khoj — The Lost Ledger',
      status: 'PLANNING',
      totalBudget: { amount: '18,500,000.0000', currency: 'INR' },
      totalActual: { amount: '1,240,000.0000', currency: 'INR' },
      totalCommitted: { amount: '3,800,000.0000', currency: 'INR' },
      variance: { amount: '13,460,000.0000', currency: 'INR' },
    }
  ];

  return (
    <div className="space-y-8">
      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Active Portfolio Budget</span>
            <DollarSign className="w-4 h-4 text-sky-400" />
          </div>
          <span className="text-2xl font-bold text-white">₹63,500,000</span>
          <span className="block text-xs text-slate-500 mt-1">2 Active Shows</span>
        </div>

        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Total Actual Cost</span>
            <TrendingUp className="w-4 h-4 text-amber-400" />
          </div>
          <span className="text-2xl font-bold text-white">₹29,690,210</span>
          <span className="block text-xs text-amber-400/90 mt-1">46.7% of Total Budget</span>
        </div>

        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Committed POs</span>
            <Clock className="w-4 h-4 text-indigo-400" />
          </div>
          <span className="text-2xl font-bold text-white">₹15,900,000</span>
          <span className="block text-xs text-slate-500 mt-1">18 Open Commitments</span>
        </div>

        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Projected Variance</span>
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
          </div>
          <span className="text-2xl font-bold text-emerald-400">+₹17,909,790</span>
          <span className="block text-xs text-slate-500 mt-1">Within Approved Ceiling</span>
        </div>
      </div>

      {/* Shows Overview Table */}
      <div className="bg-slate-950 rounded-xl border border-slate-800 overflow-hidden shadow-sm">
        <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-200">Active Production Shows</h2>
          <button className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded-md text-xs font-medium transition-colors">
            + New Show
          </button>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-300">
            <thead className="bg-slate-900/60 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
              <tr>
                <th className="px-6 py-3">Show</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3 text-right">Budget (INR)</th>
                <th className="px-6 py-3 text-right">Actual Cost</th>
                <th className="px-6 py-3 text-right">Committed</th>
                <th className="px-6 py-3 text-right">Variance</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {shows.map((show) => (
                <tr key={show.id} className="hover:bg-slate-900/30 transition-colors">
                  <td className="px-6 py-4">
                    <span className="font-semibold text-white block">{show.title}</span>
                    <span className="text-xs text-slate-500">{show.code}</span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex px-2 py-0.5 rounded text-[11px] font-semibold ${
                      show.status === 'ACTIVE'
                        ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                        : 'bg-sky-500/10 text-sky-400 border border-sky-500/20'
                    }`}>
                      {show.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right font-mono">{show.totalBudget.amount}</td>
                  <td className="px-6 py-4 text-right font-mono text-amber-300">{show.totalActual.amount}</td>
                  <td className="px-6 py-4 text-right font-mono text-slate-400">{show.totalCommitted.amount}</td>
                  <td className="px-6 py-4 text-right font-mono font-medium text-emerald-400">
                    +{show.variance.amount}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
