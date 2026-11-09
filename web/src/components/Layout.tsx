import React from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import { 
  Film, 
  LayoutDashboard, 
  Receipt, 
  ShieldCheck, 
  FileSpreadsheet, 
  Settings, 
  LogOut,
  Building2
} from 'lucide-react';

export const Layout: React.FC = () => {
  const location = useLocation();

  const navigation = [
    { name: 'Dashboard', href: '/', icon: LayoutDashboard },
    { name: 'Shows', href: '/shows', icon: Film },
    { name: 'Expenses', href: '/expenses', icon: Receipt },
    { name: 'Audit & Compliance', href: '/audit', icon: ShieldCheck },
    { name: 'Excel Ingest', href: '/excel', icon: FileSpreadsheet },
    { name: 'Settings', href: '/settings', icon: Settings },
  ];

  return (
    <div className="min-h-screen flex bg-slate-900 text-slate-100">
      {/* Sidebar */}
      <aside className="w-64 bg-slate-950 border-r border-slate-800 flex flex-col justify-between">
        <div>
          {/* Logo / Brand */}
          <div className="h-16 flex items-center px-6 border-b border-slate-800 gap-3">
            <div className="w-8 h-8 rounded bg-gradient-to-tr from-sky-600 to-indigo-500 flex items-center justify-center font-bold text-white shadow-lg">
              S
            </div>
            <div>
              <span className="font-bold tracking-tight text-white text-lg">SlateIQ</span>
              <span className="block text-[10px] text-sky-400 uppercase tracking-widest font-semibold">Production Intelligence</span>
            </div>
          </div>

          {/* Active Tenant Badge */}
          <div className="p-4 mx-3 my-3 bg-slate-900 rounded-lg border border-slate-800/80 flex items-center gap-3">
            <Building2 className="w-5 h-5 text-sky-400" />
            <div className="overflow-hidden">
              <span className="block text-xs text-slate-400 font-medium">Active Organization</span>
              <span className="block text-sm font-semibold truncate text-slate-200">Monsoon Media Works</span>
            </div>
          </div>

          {/* Nav Links */}
          <nav className="px-3 space-y-1">
            {navigation.map((item) => {
              const isActive = location.pathname === item.href;
              const Icon = item.icon;
              return (
                <Link
                  key={item.name}
                  to={item.href}
                  className={`flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                    isActive
                      ? 'bg-sky-600/20 text-sky-400 border border-sky-500/30'
                      : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900'
                  }`}
                >
                  <Icon className="w-4 h-4" />
                  {item.name}
                </Link>
              );
            })}
          </nav>
        </div>

        {/* User Footer */}
        <div className="p-4 border-t border-slate-800 flex items-center justify-between">
          <div className="flex items-center gap-3 overflow-hidden">
            <div className="w-8 h-8 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-xs font-medium text-slate-300">
              AD
            </div>
            <div className="overflow-hidden">
              <span className="block text-xs font-medium text-slate-200 truncate">Dev Admin</span>
              <span className="block text-[10px] text-slate-500 truncate">admin@monsoonmedia.local</span>
            </div>
          </div>
          <Link to="/login" className="text-slate-500 hover:text-rose-400 transition-colors p-1.5 rounded">
            <LogOut className="w-4 h-4" />
          </Link>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col min-w-0 bg-slate-900">
        <header className="h-16 border-b border-slate-800 flex items-center justify-between px-8 bg-slate-950/50 backdrop-blur">
          <h1 className="text-lg font-semibold text-slate-200">Production Cost Intelligence</h1>
          <div className="flex items-center gap-4">
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              Region: ap-south-1 (IN)
            </span>
            <span className="text-xs text-slate-500">v1.0.0-pilot</span>
          </div>
        </header>
        <div className="flex-1 p-8 overflow-y-auto">
          <Outlet />
        </div>
      </main>
    </div>
  );
};
