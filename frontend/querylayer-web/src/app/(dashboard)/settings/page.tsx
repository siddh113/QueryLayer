"use client";

import { useAuth } from "../../../lib/auth-context";

export default function SettingsPage() {
  const { userId, role } = useAuth();

  return (
    <div className="fade-in max-w-lg">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">Settings</h1>
        <p className="text-sm mt-2" style={{ color: '#71717a' }}>Manage your account</p>
      </div>

      <div className="rounded-2xl glass-card p-7"
        style={{ boxShadow: '0 0 30px rgba(0, 0, 0, 0.2)' }}
      >
        <div className="flex items-center gap-3 mb-6">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="font-semibold text-[15px]" style={{ color: '#f0f0f5' }}>Account Info</h2>
        </div>
        <dl className="space-y-5 text-sm">
          <div className="flex items-start justify-between py-2"
            style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.03)' }}
          >
            <dt className="font-medium" style={{ color: '#52525b' }}>User ID</dt>
            <dd className="font-mono text-[13px]" style={{ color: '#e4e4e7' }}>{userId || "-"}</dd>
          </div>
          <div className="flex items-start justify-between py-2">
            <dt className="font-medium" style={{ color: '#52525b' }}>Role</dt>
            <dd>
              <span className="text-[11px] px-3 py-1 rounded-full font-medium tracking-wide"
                style={{
                  background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.12), rgba(6, 182, 212, 0.08))',
                  color: '#a5b4fc',
                  border: '1px solid rgba(99, 102, 241, 0.15)',
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
