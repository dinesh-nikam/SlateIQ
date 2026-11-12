import React, { useState } from 'react';
import { Receipt, Plus, X } from 'lucide-react';

interface ExpenseItem {
  id: string;
  expenseNumber: string;
  showCode: string;
  vendorName: string;
  documentType: string;
  documentNumber: string;
  expenseDate: string;
  netAmount: string;
  taxAmount: string;
  grossAmount: string;
  status: 'DRAFT' | 'SUBMITTED' | 'APPROVED' | 'POSTED';
}

export const ExpensesPage: React.FC = () => {
  const [expenses, setExpenses] = useState<ExpenseItem[]>([
    {
      id: 'exp-001',
      expenseNumber: 'EXP-26-0001',
      showCode: 'MON-S01',
      vendorName: 'ARRI India Equipment Pvt Ltd',
      documentType: 'TAX_INVOICE',
      documentNumber: 'INV-2026-8812',
      expenseDate: '2026-11-10',
      netAmount: '150,000.0000',
      taxAmount: '27,000.0000',
      grossAmount: '177,000.0000',
      status: 'APPROVED',
    },
    {
      id: 'exp-002',
      expenseNumber: 'EXP-26-0002',
      showCode: 'MON-S01',
      vendorName: 'Reliable Fuel & Logistics',
      documentType: 'TAX_INVOICE',
      documentNumber: 'FL-99120',
      expenseDate: '2026-11-11',
      netAmount: '45,000.0000',
      taxAmount: '8,100.0000',
      grossAmount: '53,100.0000',
      status: 'SUBMITTED',
    },
    {
      id: 'exp-003',
      expenseNumber: 'EXP-26-0003',
      showCode: 'KHO-F01',
      vendorName: 'City Catering Services',
      documentType: 'BILL',
      documentNumber: 'CAT-104',
      expenseDate: '2026-11-12',
      netAmount: '22,500.0000',
      taxAmount: '1,125.0000',
      grossAmount: '23,625.0000',
      status: 'DRAFT',
    }
  ]);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [showCode, setShowCode] = useState('MON-S01');
  const [vendorName, setVendorName] = useState('');
  const [docType, setDocType] = useState('TAX_INVOICE');
  const [docNumber, setDocNumber] = useState('');
  const [netAmount, setNetAmount] = useState('50000');
  const [taxRate, setTaxRate] = useState('18');
  const [description, setDescription] = useState('');

  const calculatedTax = (Number(netAmount || 0) * (Number(taxRate || 0) / 100)).toFixed(4);
  const calculatedGross = (Number(netAmount || 0) + Number(calculatedTax)).toFixed(4);

  const handleCreateExpense = (e: React.FormEvent) => {
    e.preventDefault();
    if (!vendorName || !description) return;

    const formattedNet = Number(netAmount).toLocaleString('en-IN', { minimumFractionDigits: 4, maximumFractionDigits: 4 });
    const formattedTax = Number(calculatedTax).toLocaleString('en-IN', { minimumFractionDigits: 4, maximumFractionDigits: 4 });
    const formattedGross = Number(calculatedGross).toLocaleString('en-IN', { minimumFractionDigits: 4, maximumFractionDigits: 4 });

    const newExpense: ExpenseItem = {
      id: `exp-${Date.now().toString().slice(-4)}`,
      expenseNumber: `EXP-26-${(expenses.length + 1).toString().padStart(4, '0')}`,
      showCode,
      vendorName,
      documentType: docType,
      documentNumber: docNumber || 'N/A',
      expenseDate: new Date().toISOString().slice(0, 10),
      netAmount: formattedNet,
      taxAmount: formattedTax,
      grossAmount: formattedGross,
      status: 'SUBMITTED',
    };

    setExpenses([newExpense, ...expenses]);
    setIsModalOpen(false);
    setVendorName('');
    setDocNumber('');
    setDescription('');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center gap-2">
            <Receipt className="w-5 h-5 text-sky-400" />
            <span>Expense Capture & Audit Ledger (MOD-07)</span>
          </h1>
          <p className="text-xs text-slate-400 mt-1">
            Real-time daily expense tracking, GST/TDS tax calculation, and split cost accounting.
          </p>
        </div>
        <button
          onClick={() => setIsModalOpen(true)}
          className="px-3.5 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5 shadow-sm"
        >
          <Plus className="w-4 h-4" />
          <span>Capture Expense</span>
        </button>
      </div>

      {/* Expenses Table */}
      <div className="bg-slate-950 rounded-xl border border-slate-800 overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-300">
            <thead className="bg-slate-900/60 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
              <tr>
                <th className="px-6 py-3">Expense Ref</th>
                <th className="px-6 py-3">Show</th>
                <th className="px-6 py-3">Vendor / Payee</th>
                <th className="px-6 py-3">Doc Type & No</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3 text-right">Net Amount</th>
                <th className="px-6 py-3 text-right">Tax (GST)</th>
                <th className="px-6 py-3 text-right">Gross Total (INR)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {expenses.map((exp) => (
                <tr key={exp.id} className="hover:bg-slate-900/30 transition-colors">
                  <td className="px-6 py-4 font-mono font-medium text-white">{exp.expenseNumber}</td>
                  <td className="px-6 py-4 font-mono text-xs text-sky-400">{exp.showCode}</td>
                  <td className="px-6 py-4 font-medium text-slate-200">{exp.vendorName}</td>
                  <td className="px-6 py-4 text-xs">
                    <span className="block text-slate-300 font-mono">{exp.documentNumber}</span>
                    <span className="text-slate-500">{exp.documentType}</span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex px-2 py-0.5 rounded text-[11px] font-semibold ${
                      exp.status === 'POSTED'
                        ? 'bg-purple-500/10 text-purple-400 border border-purple-500/20'
                        : exp.status === 'APPROVED'
                        ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                        : exp.status === 'SUBMITTED'
                        ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20'
                        : 'bg-slate-800 text-slate-400 border border-slate-700'
                    }`}>
                      {exp.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right font-mono text-slate-300">₹{exp.netAmount}</td>
                  <td className="px-6 py-4 text-right font-mono text-slate-400">₹{exp.taxAmount}</td>
                  <td className="px-6 py-4 text-right font-mono font-semibold text-white">₹{exp.grossAmount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Dialog */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <Receipt className="w-5 h-5 text-sky-400" />
                <span>Capture New Production Expense</span>
              </h3>
              <button onClick={() => setIsModalOpen(false)} className="text-slate-400 hover:text-slate-200 transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateExpense} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Production Show</label>
                  <select
                    value={showCode}
                    onChange={(e) => setShowCode(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="MON-S01">MON-S01 (Monsoon Rhapsody)</option>
                    <option value="KHO-F01">KHO-F01 (Khoj)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Document Type</label>
                  <select
                    value={docType}
                    onChange={(e) => setDocType(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="TAX_INVOICE">TAX INVOICE</option>
                    <option value="BILL">BILL</option>
                    <option value="RECEIPT">RECEIPT</option>
                    <option value="PETTY_CASH_VOUCHER">PETTY CASH VOUCHER</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Vendor / Payee</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. ARRI India"
                    value={vendorName}
                    onChange={(e) => setVendorName(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-sky-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Document #</label>
                  <input
                    type="text"
                    placeholder="e.g. INV-2026-901"
                    value={docNumber}
                    onChange={(e) => setDocNumber(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-sky-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-300 mb-1">Description</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Prime lens package hire for outdoor shoot"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">Net Amount (INR)</label>
                  <input
                    type="number"
                    required
                    min={0}
                    step={100}
                    value={netAmount}
                    onChange={(e) => setNetAmount(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white font-mono focus:outline-none focus:border-sky-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1">GST Tax Rate</label>
                  <select
                    value={taxRate}
                    onChange={(e) => setTaxRate(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-sky-500"
                  >
                    <option value="0">0% (Exempt)</option>
                    <option value="5">5% GST</option>
                    <option value="12">12% GST</option>
                    <option value="18">18% GST (Standard)</option>
                    <option value="28">28% GST</option>
                  </select>
                </div>
              </div>

              {/* Real-time Calculation Summary */}
              <div className="p-3 bg-slate-950 rounded-lg border border-slate-800 space-y-1 text-xs">
                <div className="flex justify-between text-slate-400">
                  <span>Net Amount:</span>
                  <span className="font-mono text-slate-300">₹{Number(netAmount || 0).toLocaleString('en-IN', { minimumFractionDigits: 4 })}</span>
                </div>
                <div className="flex justify-between text-slate-400">
                  <span>Tax Amount ({taxRate}%):</span>
                  <span className="font-mono text-amber-400">₹{Number(calculatedTax).toLocaleString('en-IN', { minimumFractionDigits: 4 })}</span>
                </div>
                <div className="flex justify-between text-white font-semibold pt-1 border-t border-slate-800">
                  <span>Gross Total (INV-001):</span>
                  <span className="font-mono text-sky-400">₹{Number(calculatedGross).toLocaleString('en-IN', { minimumFractionDigits: 4 })}</span>
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
                  Submit Expense
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
