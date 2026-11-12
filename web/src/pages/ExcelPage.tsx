import React, { useState } from 'react';
import { 
  FileSpreadsheet, 
  UploadCloud, 
  CheckCircle2, 
  AlertCircle, 
  Download, 
  FileText, 
  Eye, 
  RefreshCw
} from 'lucide-react';
import type { ExcelImportBatch, ExcelRowError } from '../types';

export const ExcelPage: React.FC = () => {
  const [batches, setBatches] = useState<ExcelImportBatch[]>([
    {
      id: 'bat-001',
      showCode: 'MON-S01',
      templateType: 'DAILY_EXPENSE',
      filename: 'Monsoon_Shoot_Day12_Expenses.xlsx',
      fileSizeBytes: 42560,
      totalRows: 48,
      validRows: 48,
      errorRows: 0,
      status: 'COMMITTED',
      uploadedAt: '2026-11-12 11:20:00',
      completedAt: '2026-11-12 11:20:04',
    },
    {
      id: 'bat-002',
      showCode: 'MON-S01',
      templateType: 'DAILY_EXPENSE',
      filename: 'Monsoon_Shoot_Day13_Expenses_Draft.xlsx',
      fileSizeBytes: 38910,
      totalRows: 34,
      validRows: 31,
      errorRows: 3,
      status: 'REJECTED',
      uploadedAt: '2026-11-12 14:45:10',
    },
    {
      id: 'bat-003',
      showCode: 'KHO-F01',
      templateType: 'BUDGET_LINES',
      filename: 'Khoj_PostProduction_Budget_V1.xlsx',
      fileSizeBytes: 89400,
      totalRows: 120,
      validRows: 120,
      errorRows: 0,
      status: 'COMMITTED',
      uploadedAt: '2026-11-10 09:15:00',
      completedAt: '2026-11-10 09:15:12',
    }
  ]);

  const [selectedBatchId, setSelectedBatchId] = useState<string>('bat-002');
  const [selectedTemplate, setSelectedTemplate] = useState<'DAILY_EXPENSE' | 'BUDGET_LINES' | 'VENDOR_DIRECTORY'>('DAILY_EXPENSE');
  const [targetShow, setTargetShow] = useState('MON-S01');
  const [isUploading, setIsUploading] = useState(false);

  // Mock errors for batch-002
  const sampleErrors: Record<string, ExcelRowError[]> = {
    'bat-002': [
      {
        rowNumber: 14,
        fieldName: 'tax_amount',
        errorCode: 'XLS_TAX_MATH_INVALID',
        rawValue: '1200.0000',
        errorMessage: 'Scale-4 tax calculation mismatch. 18% of ₹10,000.0000 is ₹1,800.0000, but ₹1,200.0000 was supplied.',
      },
      {
        rowNumber: 22,
        fieldName: 'vendor_code',
        errorCode: 'XLS_FK_NOT_FOUND',
        rawValue: 'VEND-UNKNOWN-99',
        errorMessage: 'Foreign key constraint violation: Vendor code not registered or approved in tenant master.',
      },
      {
        rowNumber: 29,
        fieldName: 'document_date',
        errorCode: 'XLS_DATE_OUT_OF_BOUNDS',
        rawValue: '2026-12-25',
        errorMessage: 'Expense date exceeds current shoot schedule window for active season.',
      }
    ]
  };

  const handleSimulateUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    setTimeout(() => {
      const newBatch: ExcelImportBatch = {
        id: `bat-${Date.now().toString().slice(-4)}`,
        showCode: targetShow,
        templateType: selectedTemplate,
        filename: file.name,
        fileSizeBytes: file.size,
        totalRows: 25,
        validRows: 25,
        errorRows: 0,
        status: 'COMMITTED',
        uploadedAt: new Date().toISOString().replace('T', ' ').substring(0, 19),
        completedAt: new Date().toISOString().replace('T', ' ').substring(0, 19),
      };

      setBatches([newBatch, ...batches]);
      setSelectedBatchId(newBatch.id);
      setIsUploading(false);
    }, 1200);
  };

  const selectedBatch = batches.find(b => b.id === selectedBatchId);
  const activeErrors = sampleErrors[selectedBatchId] || [];

  return (
    <div className="p-8 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <FileSpreadsheet className="w-6 h-6 text-sky-400" />
            <h2 className="text-2xl font-bold tracking-tight text-white">Excel Ingestion Engine</h2>
          </div>
          <p className="text-sm text-slate-400 mt-1">
            Automated production spreadsheet parsing, strict scale-4 tax math validation, and two-phase commit ledger staging (MOD-10).
          </p>
        </div>
      </div>

      {/* Two Column Layout: Uploader & Guidelines */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Upload Card */}
        <div className="lg:col-span-2 bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-5">
          <div className="flex items-center justify-between border-b border-slate-800 pb-4">
            <h3 className="font-semibold text-slate-100 flex items-center gap-2">
              <UploadCloud className="w-5 h-5 text-sky-400" />
              Upload & Ingest Spreadsheet
            </h3>
            <span className="text-xs px-2.5 py-0.5 rounded-full bg-sky-500/10 text-sky-400 border border-sky-500/20 font-medium">
              OpenXML / XLSX / CSV
            </span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-400 mb-1">Target Production</label>
              <select
                value={targetShow}
                onChange={(e) => setTargetShow(e.target.value)}
                className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
              >
                <option value="MON-S01">MON-S01 (Monsoon Rhapsody)</option>
                <option value="KHO-F01">KHO-F01 (Khoj — Lost Ledger)</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-400 mb-1">Template Specification</label>
              <select
                value={selectedTemplate}
                onChange={(e) => setSelectedTemplate(e.target.value as any)}
                className="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
              >
                <option value="DAILY_EXPENSE">Daily Expense Ledger (V1.2)</option>
                <option value="BUDGET_LINES">Budget Version Allocations (V1.0)</option>
                <option value="VENDOR_DIRECTORY">Vendor Onboarding Directory (V2.0)</option>
              </select>
            </div>
          </div>

          {/* Drag & Drop Upload Zone */}
          <div className="relative border-2 border-dashed border-slate-700 hover:border-sky-500 rounded-xl p-8 text-center transition-colors bg-slate-900/40">
            <input
              type="file"
              accept=".xlsx,.xls,.csv"
              onChange={handleSimulateUpload}
              disabled={isUploading}
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
            />
            {isUploading ? (
              <div className="flex flex-col items-center justify-center space-y-3">
                <RefreshCw className="w-8 h-8 text-sky-400 animate-spin" />
                <p className="text-sm font-medium text-slate-200">Parsing sheet rows & validating scale-4 GST math...</p>
                <p className="text-xs text-slate-500">Executing foreign-key lookups against vendor and category tables</p>
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center space-y-2">
                <div className="w-12 h-12 rounded-full bg-sky-500/10 flex items-center justify-center text-sky-400 mb-2">
                  <UploadCloud className="w-6 h-6" />
                </div>
                <p className="text-sm font-medium text-slate-200">
                  Click to select or drag and drop spreadsheet
                </p>
                <p className="text-xs text-slate-500">
                  Supports Microsoft Excel (.xlsx), OpenXML, or RFC 4180 CSV (up to 50MB)
                </p>
              </div>
            )}
          </div>
        </div>

        {/* Canonical Templates Card */}
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-4">
          <h3 className="font-semibold text-slate-100 flex items-center gap-2 border-b border-slate-800 pb-3">
            <Download className="w-4 h-4 text-sky-400" />
            Standard Templates
          </h3>
          <p className="text-xs text-slate-400">
            Use official SlateIQ pre-validated formats to guarantee 100% first-pass ingestion compliance:
          </p>

          <div className="space-y-3">
            <div className="p-3 bg-slate-900 rounded-lg border border-slate-800 flex items-center justify-between hover:border-slate-700 transition-colors">
              <div className="flex items-center gap-3">
                <FileText className="w-4 h-4 text-emerald-400" />
                <div>
                  <div className="text-xs font-semibold text-slate-200">Daily Expense Ingest</div>
                  <div className="text-[10px] text-slate-500">Includes GST slab & split columns</div>
                </div>
              </div>
              <button 
                onClick={() => alert('Downloading SlateIQ_Daily_Expense_Template_v1.2.xlsx')}
                className="text-xs text-sky-400 hover:text-sky-300 font-medium cursor-pointer"
              >
                Download
              </button>
            </div>

            <div className="p-3 bg-slate-900 rounded-lg border border-slate-800 flex items-center justify-between hover:border-slate-700 transition-colors">
              <div className="flex items-center gap-3">
                <FileText className="w-4 h-4 text-indigo-400" />
                <div>
                  <div className="text-xs font-semibold text-slate-200">Budget Line Allocations</div>
                  <div className="text-[10px] text-slate-500">Chart of accounts & fringes</div>
                </div>
              </div>
              <button 
                onClick={() => alert('Downloading SlateIQ_Budget_Allocation_Template_v1.0.xlsx')}
                className="text-xs text-sky-400 hover:text-sky-300 font-medium cursor-pointer"
              >
                Download
              </button>
            </div>

            <div className="p-3 bg-slate-900 rounded-lg border border-slate-800 flex items-center justify-between hover:border-slate-700 transition-colors">
              <div className="flex items-center gap-3">
                <FileText className="w-4 h-4 text-amber-400" />
                <div>
                  <div className="text-xs font-semibold text-slate-200">Vendor Directory</div>
                  <div className="text-[10px] text-slate-500">PAN/GSTIN/MSME verification</div>
                </div>
              </div>
              <button 
                onClick={() => alert('Downloading SlateIQ_Vendor_Directory_Template_v2.0.xlsx')}
                className="text-xs text-sky-400 hover:text-sky-300 font-medium cursor-pointer"
              >
                Download
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Batch History & Ingest Ledger */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Batches Table */}
        <div className="lg:col-span-2 bg-slate-950 rounded-xl border border-slate-800 overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
            <h3 className="font-semibold text-slate-100 text-sm">Recent Ingest Batches</h3>
            <span className="text-xs text-slate-400">Select a batch to inspect errors</span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-slate-300">
              <thead className="bg-slate-900/60 text-xs uppercase font-medium text-slate-400 border-b border-slate-800">
                <tr>
                  <th className="px-5 py-3">Batch & File</th>
                  <th className="px-5 py-3">Show</th>
                  <th className="px-5 py-3">Rows (Valid / Err)</th>
                  <th className="px-5 py-3">Status</th>
                  <th className="px-5 py-3 text-right">View</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60">
                {batches.map((batch) => {
                  const isSelected = batch.id === selectedBatchId;
                  return (
                    <tr 
                      key={batch.id} 
                      onClick={() => setSelectedBatchId(batch.id)}
                      className={`cursor-pointer transition-colors ${
                        isSelected ? 'bg-sky-950/30 border-l-2 border-sky-400' : 'hover:bg-slate-900/40'
                      }`}
                    >
                      <td className="px-5 py-3.5">
                        <div className="text-xs font-semibold text-slate-200">{batch.filename}</div>
                        <div className="text-[10px] text-slate-500 font-mono">{batch.id} • {batch.uploadedAt}</div>
                      </td>
                      <td className="px-5 py-3.5 font-bold text-xs text-slate-300">
                        {batch.showCode}
                      </td>
                      <td className="px-5 py-3.5">
                        <span className="text-xs text-emerald-400 font-medium">{batch.validRows}</span>
                        <span className="text-xs text-slate-500"> / </span>
                        <span className={`text-xs font-medium ${batch.errorRows > 0 ? 'text-rose-400' : 'text-slate-500'}`}>
                          {batch.errorRows}
                        </span>
                      </td>
                      <td className="px-5 py-3.5">
                        {batch.status === 'COMMITTED' && (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                            <CheckCircle2 className="w-3 h-3" /> Committed
                          </span>
                        )}
                        {batch.status === 'REJECTED' && (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                            <AlertCircle className="w-3 h-3" /> Rejected
                          </span>
                        )}
                        {batch.status === 'VALIDATING' && (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-sky-500/10 text-sky-400 border border-sky-500/20">
                            <RefreshCw className="w-3 h-3 animate-spin" /> Validating
                          </span>
                        )}
                      </td>
                      <td className="px-5 py-3.5 text-right">
                        <Eye className={`w-4 h-4 inline-block ${isSelected ? 'text-sky-400' : 'text-slate-500'}`} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Row Error Inspector */}
        <div className="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-semibold text-slate-100 text-sm flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-rose-400" />
              Row-Level Error Inspector
            </h3>
            {selectedBatch && (
              <span className="text-[11px] font-mono text-slate-400">
                {selectedBatch.filename}
              </span>
            )}
          </div>

          {activeErrors.length === 0 ? (
            <div className="p-8 text-center space-y-2">
              <CheckCircle2 className="w-8 h-8 text-emerald-400 mx-auto" />
              <p className="text-sm font-medium text-slate-300">Clean Ingest Batch</p>
              <p className="text-xs text-slate-500">
                Zero row or column-level schema violations discovered. All scale-4 invariants satisfied.
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="text-xs text-rose-400 font-medium">
                {activeErrors.length} validation errors blocked automatic commit:
              </div>
              {activeErrors.map((err, idx) => (
                <div key={idx} className="p-3 bg-slate-900 rounded-lg border border-rose-900/30 space-y-1.5">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-rose-400 font-mono">
                      Row {err.rowNumber} • {err.fieldName}
                    </span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-rose-950 text-rose-300 border border-rose-800 font-mono">
                      {err.errorCode}
                    </span>
                  </div>
                  <div className="text-[11px] text-slate-300">{err.errorMessage}</div>
                  <div className="text-[10px] font-mono text-slate-500 truncate">
                    Raw value received: "{err.rawValue}"
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
