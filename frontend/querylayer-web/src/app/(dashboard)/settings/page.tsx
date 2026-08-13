"use client";

import { useAuth } from "../../../lib/auth-context";

export default function SettingsPage() {
  const { userId, role } = useAuth();

  return (
    <div className="fade-in max-w-lg">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">Settings</h1>
        <p className="text-sm mt-2" style={{ color: 'var(--text-muted)' }}>Manage your account</p>
      </div>

      <div className="rounded-2xl glass-card p-7">
        <div className="flex items-center gap-3 mb-6">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #FF6B4A, #6D5DFC)' }} />
          <h2 className="font-semibold text-[15px]" style={{ color: 'var(--text-primary)' }}>Account Info</h2>
        </div>
        <dl className="space-y-5 text-sm">
          <div className="flex items-start justify-between py-2"
            style={{ borderBottom: '1px solid var(--border-subtle)' }}
          >
            <dt className="font-medium" style={{ color: 'var(--text-muted)' }}>User ID</dt>
            <dd className="font-mono text-[13px]" style={{ color: 'var(--text-primary)' }}>{userId || "-"}</dd>
          </div>
          <div className="flex items-start justify-between py-2">
            <dt className="font-medium" style={{ color: 'var(--text-muted)' }}>Role</dt>
            <dd>
              <span className="text-[11px] px-3 py-1 rounded-full font-medium tracking-wide"
                style={{
                  background: 'linear-gradient(135deg, rgba(255, 107, 74, 0.12), rgba(109, 93, 252, 0.08))',
                  color: 'var(--accent-hover)',
                  border: '1px solid rgba(255, 107, 74, 0.15)',
                }}
              >
                {role || "-"}
              </span>
            </dd>
          </div>
        </dl>
      </div>
    </div>
  );
}
