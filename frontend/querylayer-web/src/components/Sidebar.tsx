"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navItems = [
  {
    label: "Dashboard",
    href: "/dashboard",
    icon: (
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" />
        <rect x="14" y="14" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" />
      </svg>
    ),
  },
  {
    label: "Projects",
    href: "/projects",
    icon: (
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
      </svg>
    ),
  },
  {
    label: "API Explorer",
    href: "/explorer",
    icon: (
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
      </svg>
    ),
  },
  {
    label: "Docs",
    href: "/docs",
    icon: (
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
        <polyline points="14 2 14 8 20 8" />
        <line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" />
      </svg>
    ),
  },
  {
    label: "Settings",
    href: "/settings",
    icon: (
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="3" />
        <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" />
      </svg>
    ),
  },
];

export default function Sidebar() {
  const pathname = usePathname();

  return (
    <aside
      className="w-60 flex flex-col min-h-screen border-r"
      style={{
        background: 'var(--sidebar-bg)',
        backdropFilter: 'blur(20px)',
        WebkitBackdropFilter: 'blur(20px)',
        borderColor: 'var(--border)',
      }}
    >
      {/* Logo */}
      <div className="px-5 py-5 flex items-center gap-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <div
          className="w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold flex-shrink-0"
          style={{ background: 'linear-gradient(135deg, #6366f1, #4f46e5)' }}
        >
          <span className="text-white">Q</span>
        </div>
        <span className="text-[15px] font-semibold gradient-text tracking-tight">
          QueryLayer
        </span>
      </div>

      {/* Nav */}
      <nav className="flex-1 py-4 px-3">
        <div className="space-y-1">
          {navItems.map((item) => {
            const active = pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-[13px] transition-all duration-200"
                style={{
                  background: active ? 'var(--active-nav-bg)' : 'transparent',
                  color: active ? 'var(--active-nav-text)' : 'var(--text-muted)',
                  boxShadow: active ? '0 0 12px var(--glow-violet)' : 'none',
                }}
                onMouseEnter={(e) => {
                  if (!active) {
                    e.currentTarget.style.background = 'var(--hover-nav-bg)';
                    e.currentTarget.style.color = 'var(--hover-nav-text)';
                  }
                }}
                onMouseLeave={(e) => {
                  if (!active) {
                    e.currentTarget.style.background = 'transparent';
                    e.currentTarget.style.color = 'var(--text-muted)';
                  }
                }}
              >
                <span className="flex-shrink-0" style={{ color: active ? 'var(--active-nav-icon)' : 'inherit' }}>
                  {item.icon}
                </span>
                <span className={active ? "font-medium" : "font-normal"}>
                  {item.label}
                </span>
                {active && (
                  <div
                    className="ml-auto w-1.5 h-1.5 rounded-full"
                    style={{ background: 'linear-gradient(135deg, #6366f1, #06b6d4)' }}
                  />
                )}
              </Link>
            );
          })}
        </div>
      </nav>

      {/* Footer */}
      <div className="px-5 py-4" style={{ borderTop: '1px solid var(--border-subtle)' }}>
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full pulse-dot" style={{ background: 'var(--dot-success)' }} />
          <span className="text-[11px] font-medium" style={{ color: 'var(--text-faint)' }}>All systems operational</span>
        </div>
      </div>
    </aside>
  );
}
