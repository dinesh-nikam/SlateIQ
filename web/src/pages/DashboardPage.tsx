import React, { useState } from 'react';
import { TrendingUp, CheckCircle2, Clock, DollarSign, Plus, X, Film } from 'lucide-react';
import type { ShowSummary } from '../types';

export const DashboardPage: React.FC = () => {
  const [shows, setShows] = useState<ShowSummary[]>([
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
  ]);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [newCode, setNewCode] = useState('');
  const [newTitle, setNewTitle] = useState('');
  const [newType, setNewType] = useState('SERIES');
  const [newBudget, setNewBudget] = useState('25000000');
  const [newShootDays, setNewShootDays] = useState('45');

  const handleCreateShow = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCode || !newTitle) return;

    const formattedBudget = Number(newBudget).toLocaleString('en-IN', {
      minimumFractionDigits: 4,
      maximumFractionDigits: 4,
    });

    const newShow: ShowSummary = {
      id: `0193b2d4-${Date.now().toString().slice(-4)}`,
      code: newCode.toUpperCase(),
      title: newTitle,
      status: 'PLANNING',
      totalBudget: { amount: formattedBudget, currency: 'INR' },
      totalActual: { amount: '0.0000', currency: 'INR' },
      totalCommitted: { amount: '0.0000', currency: 'INR' },
      variance: { amount: formattedBudget, currency: 'INR' },
    };

    setShows([newShow, ...shows]);
    setIsModalOpen(false);
    setNewCode('');
    setNewTitle('');
    setNewBudget('25000000');
  };

  return (
    <div className="space-y-8">
      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Active Portfolio Budget</span>
            <DollarSign className="w-4 h-4 text-sky-400" />
          </div>
          <span className="text-2xl font-bold text-white">₹88,500,000</span>
          <span className="block text-xs text-slate-500 mt-1">{shows.length} Active Shows</span>
        </div>

        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 shadow-sm">
          <div className="flex items-center justify-between text-slate-400 mb-2">
            <span className="text-xs font-medium uppercase tracking-wider">Total Actual Cost</span>
            <TrendingUp className="w-4 h-4 text-amber-400" />
          </div>
          <span className="text-2xl font-bold text-white">₹29,690,210</span>
          <span className="block text-xs text-amber-400/90 mt-1">33.5% of Total Budget</span>
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
          <span className="text-2xl font-bold text-emerald-400">+₹42,909,790</span>
          <span className="block text-xs text-slate-500 mt-1">Within Approved Ceiling</span>
        </div>
      </div>

      {/* Shows Overview Table */}
      <div className="bg-slate-950 rounded-xl border border-slate-800 overflow-hidden shadow-sm">
        <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Film className="w-4 h-4 text-sky-400" />
            <h2 className="text-base font-semibold text-slate-200">Production Shows (MOD-03)</h2>
          </div>
          <button
            onClick={() => setIsModalOpen(true)}
            className="px-3 py-1.5 bg-sky-600 hover:bg-sky-500 text-white rounded-md text-xs font-medium transition-colors flex items-center gap-1.5 shadow-sm"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>New Show</span>
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
                    <span className="text-xs text-slate-500 font-mono">{show.code}</span>
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

      {/* Create Show Modal Dialog */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <Film className="w-5 h-5 text-sky-400" />
                <span>Create Production Show</span>
              </h3>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-slate-400 hover:text-slate-200 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateShow} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Show Code (A2-05)</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. MNS01"
                    maxLength={12}
                    value={newCode}
                    onChange={(e) => setNewCode(e.target.value.toUpperCase())}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white font-mono uppercase focus:outline-none focus:border-sky-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Show Type</label>
                  <select
                    value={newType}
                    onChange={(e) => setNewType(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="SERIES">SERIES</option>
                    <option value="FILM">FILM</option>
                    <option value="REALITY">REALITY</option>
                    <option value="DOCUMENTARY">DOCUMENTARY</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1">Production Title</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Citadel: Honey Bunny (Season 2)"
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Initial Budget (INR)</label>
                  <input
                    type="number"
                    required
                    min={0}
                    step={1000}
                    value={newBudget}
                    onChange={(e) => setNewBudget(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white font-mono focus:outline-none focus:border-sky-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Planned Shoot Days</label>
                  <input
                    type="number"
                    min={1}
                    value={newShootDays}
                    onChange={(e) => setNewShootDays(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-sky-500"
                  />
                </div>
              </div>

              <div className="pt-3 border-t border-slate-800 flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded-lg text-xs font-medium transition-colors shadow-sm"
                >
                  Create Show
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
