import React, { useState } from 'react';
import { 
  ShieldCheck, 
  AlertTriangle, 
  AlertOctagon, 
  CheckCircle2, 
  Clock, 
  Plus, 
  X, 
  Search,
  Filter,
  Check
} from 'lucide-react';
import type { AuditFinding, FindingSeverity, FindingStatus } from '../types';

export const AuditPage: React.FC = () => {
  const [findings, setFindings] = useState<AuditFinding[]>([
    {
      id: 'fnd-001',
      findingNumber: 'FND-2026-001',
      showId: '0193b2d4-0001',
      showCode: 'MON-S01',
      severity: 'CRITICAL',
      category: 'MISSING_GST_INVOICE',
      title: 'Missing GST Tax Invoice for Generator Rental',
      description: 'Vendor Reliable Power Solutions billed ₹1,80,000 via unformatted voucher without valid 15-digit GSTIN or digital signature.',
      visibility: 'SHARED_WITH_PRODUCTION',
      status: 'OPEN',
      department: 'CAMERA_ELECTRICAL',
      linkedEntityType: 'EXPENSE',
      linkedEntityRef: 'EXP-26-0002',
      identifiedDate: '2026-11-10',
      dueDate: '2026-11-17',
    },
    {
      id: 'fnd-002',
      findingNumber: 'FND-2026-002',
      showId: '0193b2d4-0001',
      showCode: 'MON-S01',
      severity: 'HIGH',
      category: 'UNAPPROVED_OVERTIME',
      title: 'Unsigned Art Dept Overtime Vouchers',
      description: '3 days of consecutive set-dressing overtime exceeding 14 hrs/day without prior Line Producer authorization.',
      visibility: 'SHARED_WITH_PRODUCTION',
      status: 'UNDER_REVIEW',
      department: 'ART_DEPARTMENT',
      linkedEntityType: 'EXPENSE',
      linkedEntityRef: 'EXP-26-0014',
      identifiedDate: '2026-11-08',
      dueDate: '2026-11-15',
    },
    {
      id: 'fnd-003',
      findingNumber: 'FND-2026-003',
      showId: '0193b2d4-0002',
      showCode: 'KHO-F01',
      severity: 'MEDIUM',
      category: 'OVER_BUDGET_LINE',
      title: 'Wardrobe Acquisition Variance Exceeding 15%',
      description: 'Costume rental commitments stand at 118% of approved version 1.0 baseline allocation.',
      visibility: 'SHARED_WITH_PRODUCTION',
      status: 'OPEN',
      department: 'COSTUME_WARDROBE',
      linkedEntityType: 'BUDGET_LINE',
      linkedEntityRef: 'BL-CST-004',
      identifiedDate: '2026-11-11',
      dueDate: '2026-11-20',
    },
    {
      id: 'fnd-004',
      findingNumber: 'FND-2026-004',
      showId: '0193b2d4-0001',
      showCode: 'MON-S01',
      severity: 'LOW',
      category: 'DOCUMENTATION_INCOMPLETE',
      title: 'Vehicle Logsheet Odometer Mismatch',
      description: 'Unit van logsheet missing start odometer reading for shoot day 12.',
      visibility: 'INTERNAL_AUDIT_ONLY',
      status: 'RESOLVED',
      department: 'TRANSPORTATION',
      linkedEntityType: 'EXPENSE',
      linkedEntityRef: 'EXP-26-0009',
      identifiedDate: '2026-11-05',
      resolutionNotes: 'Updated logsheet signed by Head of Transport attached.',
    }
  ]);

  const [severityFilter, setSeverityFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState('');

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [newCategory, setNewCategory] = useState('MISSING_GST_INVOICE');
  const [newSeverity, setNewSeverity] = useState<FindingSeverity>('HIGH');
  const [newShowCode, setNewShowCode] = useState('MON-S01');
  const [newDepartment, setNewDepartment] = useState('CAMERA_ELECTRICAL');
  const [newDescription, setNewDescription] = useState('');
  const [newLinkedRef, setNewLinkedRef] = useState('');
  const [newDueDate, setNewDueDate] = useState('2026-11-20');

  const filteredFindings = findings.filter(f => {
    const matchesSeverity = severityFilter === 'ALL' || f.severity === severityFilter;
    const matchesStatus = statusFilter === 'ALL' || f.status === statusFilter;
    const matchesQuery = searchQuery === '' || 
      f.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.findingNumber.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.showCode.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesSeverity && matchesStatus && matchesQuery;
  });

  const totalFindings = findings.length;
  const criticalHighCount = findings.filter(f => (f.severity === 'CRITICAL' || f.severity === 'HIGH') && f.status !== 'RESOLVED' && f.status !== 'CLOSED').length;
  const underReviewCount = findings.filter(f => f.status === 'UNDER_REVIEW').length;
  const resolvedCount = findings.filter(f => f.status === 'RESOLVED' || f.status === 'CLOSED').length;

  const handleCreateFinding = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle || !newDescription) return;

    const count = findings.length + 1;
    const pad = count.toString().padStart(3, '0');
    const newFinding: AuditFinding = {
      id: `fnd-${Date.now().toString().slice(-4)}`,
      findingNumber: `FND-2026-${pad}`,
      showId: newShowCode === 'MON-S01' ? '0193b2d4-0001' : '0193b2d4-0002',
      showCode: newShowCode,
      severity: newSeverity,
      category: newCategory,
      title: newTitle,
      description: newDescription,
      visibility: 'SHARED_WITH_PRODUCTION',
      status: 'OPEN',
      department: newDepartment,
      linkedEntityType: newLinkedRef ? 'EXPENSE' : undefined,
      linkedEntityRef: newLinkedRef || undefined,
      identifiedDate: new Date().toISOString().split('T')[0],
      dueDate: newDueDate,
    };

    setFindings([newFinding, ...findings]);
    setIsModalOpen(false);
    setNewTitle('');
    setNewDescription('');
    setNewLinkedRef('');
  };

  const handleResolve = (id: string) => {
    setFindings(findings.map(f => {
      if (f.id === id) {
        return {
          ...f,
          status: 'RESOLVED',
          resolutionNotes: 'Verified and resolved by auditor review.',
        };
      }
      return f;
    }));
  };

  const severityBadge = (severity: FindingSeverity) => {
    switch (severity) {
      case 'CRITICAL':
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20"><AlertOctagon className="w-3 h-3" /> Critical</span>;
      case 'HIGH':
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20"><AlertTriangle className="w-3 h-3" /> High</span>;
      case 'MEDIUM':
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-500/10 text-sky-400 border border-sky-500/20">Medium</span>;
      case 'LOW':
        return <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-500/10 text-slate-400 border border-slate-500/20">Low</span>;
    }
  };

  const statusBadge = (status: FindingStatus) => {
    switch (status) {
      case 'OPEN':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-500/10 text-amber-400 border border-amber-500/20">Open</span>;
      case 'UNDER_REVIEW':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-purple-500/10 text-purple-400 border border-purple-500/20"><Clock className="w-3 h-3" /> Under Review</span>;
      case 'RESOLVED':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"><CheckCircle2 className="w-3 h-3" /> Resolved</span>;
      case 'CLOSED':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-500/10 text-slate-400 border border-slate-500/20">Closed</span>;
      case 'DRAFT':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-600/10 text-slate-300 border border-slate-600/20">Draft</span>;
      case 'WAIVED':
        return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-slate-700/10 text-slate-400 border border-slate-700/20">Waived</span>;
    }
  };

  return (
    <div className="p-8 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <ShieldCheck className="w-6 h-6 text-sky-400" />
            <h2 className="text-2xl font-bold tracking-tight text-white">Audit Operations & Findings</h2>
          </div>
          <p className="text-sm text-slate-400 mt-1">
            Real-time compliance monitoring, variance audit flags, and corrective action lifecycles (MOD-06).
          </p>
        </div>
        <button
          onClick={() => setIsModalOpen(true)}
          className="flex items-center gap-2 px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white font-medium rounded-lg text-sm transition-colors shadow-sm cursor-pointer"
        >
          <Plus className="w-4 h-4" />
          Log Audit Finding
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-slate-950 p-5 rounded-xl border border-slate-800">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium uppercase tracking-wider">
            Total Findings
            <ShieldCheck className="w-4 h-4 text-sky-400" />
          </div>
          <div className="text-2xl font-bold text-white mt-2">{totalFindings}</div>
          <div className="text-xs text-slate-500 mt-1">Across 2 active productions</div>
        </div>

        <div className="bg-slate-950 p-5 rounded-xl border border-slate-800">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium uppercase tracking-wider">
            Critical / High Risk
            <AlertOctagon className="w-4 h-4 text-rose-400" />
          </div>
          <div className="text-2xl font-bold text-rose-400 mt-2">{criticalHighCount}</div>
          <div className="text-xs text-rose-500/80 mt-1">Requires immediate Line Producer review</div>
        </div>

        <div className="bg-slate-950 p-5 rounded-xl border border-slate-800">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium uppercase tracking-wider">
            Under Review
            <Clock className="w-4 h-4 text-purple-400" />
          </div>
          <div className="text-2xl font-bold text-purple-400 mt-2">{underReviewCount}</div>
          <div className="text-xs text-slate-500 mt-1">Pending auditor verification</div>
        </div>

        <div className="bg-slate-950 p-5 rounded-xl border border-slate-800">
          <div className="flex items-center justify-between text-slate-400 text-xs font-medium uppercase tracking-wider">
            Resolved / Cleared
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
          </div>
          <div className="text-2xl font-bold text-emerald-400 mt-2">{resolvedCount}</div>
          <div className="text-xs text-emerald-500/80 mt-1">Full audit evidence archived</div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-wrap gap-4 items-center justify-between">
        <div className="flex items-center gap-3 flex-1 min-w-[280px]">
          <div className="relative flex-1">
            <Search className="w-4 h-4 absolute left-3 top-3 text-slate-500" />
            <input
              type="text"
              placeholder="Search findings by #, show, or title..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-slate-900 border border-slate-800 rounded-lg pl-9 pr-4 py-2 text-sm text-slate-200 placeholder-slate-500 focus:outline-none focus:border-sky-500"
            />
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-slate-400" />
            <span className="text-xs text-slate-400 uppercase tracking-wider font-semibold">Severity:</span>
            <select
              value={severityFilter}
              onChange={(e) => setSeverityFilter(e.target.value)}
              className="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-sky-500"
            >
              <option value="ALL">All Severities</option>
              <option value="CRITICAL">Critical</option>
              <option value="HIGH">High</option>
              <option value="MEDIUM">Medium</option>
              <option value="LOW">Low</option>
            </select>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-400 uppercase tracking-wider font-semibold">Status:</span>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-sky-500"
            >
              <option value="ALL">All Statuses</option>
              <option value="OPEN">Open</option>
              <option value="UNDER_REVIEW">Under Review</option>
              <option value="RESOLVED">Resolved</option>
              <option value="CLOSED">Closed</option>
            </select>
          </div>
        </div>
      </div>

      {/* Findings Table */}
      <div className="bg-slate-950 rounded-xl border border-slate-800 overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-300">
            <thead className="bg-slate-900/60 text-xs uppercase font-medium text-slate-400 border-b border-slate-800">
              <tr>
                <th className="px-6 py-4">Finding Ref</th>
                <th className="px-6 py-4">Show & Dept</th>
                <th className="px-6 py-4">Severity</th>
                <th className="px-6 py-4">Title & Details</th>
                <th className="px-6 py-4">Linked Item</th>
                <th className="px-6 py-4">Status</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {filteredFindings.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-6 py-8 text-center text-slate-500 text-sm">
                    No audit findings match the current filter criteria.
                  </td>
                </tr>
              ) : (
                filteredFindings.map((finding) => (
                  <tr key={finding.id} className="hover:bg-slate-900/40 transition-colors">
                    <td className="px-6 py-4 font-mono text-xs font-semibold text-sky-400 whitespace-nowrap">
                      {finding.findingNumber}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-xs font-bold text-slate-200">{finding.showCode}</div>
                      <div className="text-[11px] text-slate-500">{finding.department?.replace('_', ' ') || 'General'}</div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {severityBadge(finding.severity)}
                    </td>
                    <td className="px-6 py-4 max-w-md">
                      <div className="text-xs font-medium text-slate-200">{finding.title}</div>
                      <div className="text-[11px] text-slate-400 mt-0.5 line-clamp-2">{finding.description}</div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {finding.linkedEntityRef ? (
                        <span className="font-mono text-xs px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-300">
                          {finding.linkedEntityRef}
                        </span>
                      ) : (
                        <span className="text-slate-600 text-xs">—</span>
                      )}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {statusBadge(finding.status)}
                    </td>
                    <td className="px-6 py-4 text-right whitespace-nowrap">
                      {finding.status !== 'RESOLVED' && finding.status !== 'CLOSED' ? (
                        <button
                          onClick={() => handleResolve(finding.id)}
                          className="inline-flex items-center gap-1.5 px-3 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-400 border border-emerald-500/30 rounded text-xs font-medium transition-colors cursor-pointer"
                        >
                          <Check className="w-3.5 h-3.5" />
                          Resolve
                        </button>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-xs text-emerald-400/80">
                          <CheckCircle2 className="w-3.5 h-3.5" /> Cleared
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Log Audit Finding Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-slate-950 border border-slate-800 rounded-xl w-full max-w-lg shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-100">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800">
              <h3 className="font-semibold text-slate-100 flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-sky-400" />
                Log Audit Finding
              </h3>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-slate-500 hover:text-slate-300 p-1 rounded"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateFinding} className="p-6 space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Target Production</label>
                  <select
                    value={newShowCode}
                    onChange={(e) => setNewShowCode(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="MON-S01">MON-S01 (Monsoon Rhapsody)</option>
                    <option value="KHO-F01">KHO-F01 (Khoj — Lost Ledger)</option>
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Department</label>
                  <select
                    value={newDepartment}
                    onChange={(e) => setNewDepartment(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="CAMERA_ELECTRICAL">Camera & Electrical</option>
                    <option value="ART_DEPARTMENT">Art Department</option>
                    <option value="COSTUME_WARDROBE">Costume & Wardrobe</option>
                    <option value="PRODUCTION_OPERATIONS">Production Operations</option>
                    <option value="TRANSPORTATION">Transportation</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Severity</label>
                  <select
                    value={newSeverity}
                    onChange={(e) => setNewSeverity(e.target.value as FindingSeverity)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="CRITICAL">Critical</option>
                    <option value="HIGH">High</option>
                    <option value="MEDIUM">Medium</option>
                    <option value="LOW">Low</option>
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Category</label>
                  <select
                    value={newCategory}
                    onChange={(e) => setNewCategory(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="MISSING_GST_INVOICE">Missing GST Invoice</option>
                    <option value="UNAPPROVED_OVERTIME">Unapproved Overtime</option>
                    <option value="OVER_BUDGET_LINE">Over Budget Line (&gt;10%)</option>
                    <option value="DUPLICATE_PAYMENT">Duplicate Payment Risk</option>
                    <option value="DOCUMENTATION_INCOMPLETE">Incomplete Documentation</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Finding Title</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Missing GSTIN on Generator Invoice"
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Linked Expense / Doc Ref</label>
                  <input
                    type="text"
                    placeholder="e.g. EXP-26-0002"
                    value={newLinkedRef}
                    onChange={(e) => setNewLinkedRef(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">Resolution Due Date</label>
                  <input
                    type="date"
                    value={newDueDate}
                    onChange={(e) => setNewDueDate(e.target.value)}
                    className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Detailed Observation & Audit Note</label>
                <textarea
                  rows={3}
                  required
                  placeholder="Provide precise observation, discrepancy numbers, and required remedial action..."
                  value={newDescription}
                  onChange={(e) => setNewDescription(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="pt-2 flex justify-end gap-3 border-t border-slate-800">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-sm transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded-lg text-sm font-medium transition-colors shadow-sm cursor-pointer"
                >
                  Create Finding
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
