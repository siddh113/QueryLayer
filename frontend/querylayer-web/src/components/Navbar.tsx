"use client";

import { useAuth } from "../lib/auth-context";
import { useTheme } from "../lib/theme-context";
import { useRouter } from "next/navigation";

export default function Navbar() {
  const { userId, role, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const router = useRouter();

  const handleLogout = () => {
    logout();
    router.push("/login");
  };

  return (
    <header
      className="h-14 flex items-center justify-between px-6"
      style={{
        background: 'var(--navbar-bg)',
        backdropFilter: 'blur(16px)',
        WebkitBackdropFilter: 'blur(16px)',
        borderBottom: '1px solid var(--border-subtle)',
      }}
    >
      <div />
      <div className="flex items-center gap-4">
        {/* Theme toggle */}
        <button
          onClick={toggleTheme}
          className="theme-toggle w-8 h-8 rounded-lg flex items-center justify-center"
          style={{
            background: 'var(--hover-nav-bg)',
            color: 'var(--text-muted)',
          }}
          title={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
          aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
        >
          {theme === "dark" ? (
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="5" />
              <line x1="12" y1="1" x2="12" y2="3" />
              <line x1="12" y1="21" x2="12" y2="23" />
              <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
              <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
              <line x1="1" y1="12" x2="3" y2="12" />
              <line x1="21" y1="12" x2="23" y2="12" />
              <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
              <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
            </svg>
          ) : (
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
            </svg>
          )}
        </button>

        {userId && (
          <>
            <span
              className="text-[11px] px-3 py-1 rounded-full font-medium tracking-wide"
              style={{
                background: 'var(--active-nav-bg)',
                color: 'var(--badge-text)',
                border: '1px solid var(--badge-border)',
              }}
            >
              {role}
            </span>
            <div className="w-px h-4" style={{ background: 'var(--border)' }} />
            <button
              onClick={handleLogout}
              className="text-xs font-medium transition-colors duration-200"
              style={{ color: 'var(--text-faint)' }}
              onMouseEnter={(e) => { e.currentTarget.style.color = 'var(--text-primary)'; }}
              onMouseLeave={(e) => { e.currentTarget.style.color = 'var(--text-faint)'; }}
            >
              Sign out
            </button>
          </>
        )}
      </div>
    </header>
  );
}
