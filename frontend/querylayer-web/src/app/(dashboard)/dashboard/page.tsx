"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getProjects, deleteProject } from "../../../services/api";
import type { Project } from "../../../types";

export default function DashboardPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState("");

  useEffect(() => {
    getProjects()
      .then(setProjects)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleDelete = async (id: string) => {
    setDeletingId(id);
    setDeleteError("");
    try {
      await deleteProject(id);
      setProjects((prev) => prev.filter((p) => p.id !== id));
      setConfirmingId(null);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string } } }).response?.data?.error
          : undefined;
      setDeleteError(msg || "Failed to delete project");
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="fade-in max-w-5xl">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">Overview</h1>
        <p className="text-sm mt-2" style={{ color: 'var(--text-muted)' }}>Your QueryLayer workspace at a glance</p>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5 mb-8">
        {/* Total Projects */}
        <div className="rounded-2xl p-6 glass-card-hover relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 rounded-full opacity-20"
            style={{ background: 'radial-gradient(circle, rgba(99, 102, 241, 0.3), transparent)', filter: 'blur(20px)' }}
          />
          <div className="relative">
            <div className="flex items-center gap-2 mb-3">
              <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: 'rgba(99, 102, 241, 0.1)', border: '1px solid rgba(99, 102, 241, 0.15)' }}
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#818cf8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
                </svg>
              </div>
              <span className="text-[13px] font-medium" style={{ color: 'var(--text-muted)' }}>Total Projects</span>
            </div>
            <div className="text-3xl font-bold" style={{ color: 'var(--stat-value-color)' }}>
              {loading ? <span className="shimmer inline-block w-10 h-8 rounded-lg" /> : projects.length}
            </div>
          </div>
        </div>

        {/* Status */}
        <div className="rounded-2xl p-6 glass-card-hover relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 rounded-full opacity-20"
            style={{ background: 'radial-gradient(circle, rgba(34, 197, 94, 0.3), transparent)', filter: 'blur(20px)' }}
          />
          <div className="relative">
            <div className="flex items-center gap-2 mb-3">
              <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: 'rgba(34, 197, 94, 0.1)', border: '1px solid rgba(34, 197, 94, 0.15)' }}
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M22 11.08V12a10 10 0 11-5.93-9.14" /><polyline points="22 4 12 14.01 9 11.01" />
                </svg>
              </div>
              <span className="text-[13px] font-medium" style={{ color: 'var(--text-muted)' }}>System Status</span>
            </div>
            <div className="flex items-center gap-2.5">
              <span className="w-2 h-2 rounded-full pulse-dot" style={{ background: 'var(--dot-success)' }} />
              <span className="text-sm font-semibold" style={{ color: 'var(--status-text)' }}>All Systems Operational</span>
            </div>
          </div>
        </div>

        {/* Quick Start */}
        <div className="rounded-2xl p-6 glass-card-hover relative overflow-hidden group cursor-pointer"
          onClick={() => window.location.href = '/projects/new'}
        >
          <div className="absolute top-0 right-0 w-24 h-24 rounded-full opacity-20"
            style={{ background: 'radial-gradient(circle, rgba(6, 182, 212, 0.3), transparent)', filter: 'blur(20px)' }}
          />
          <div className="relative">
            <div className="flex items-center gap-2 mb-3">
              <div className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: 'rgba(6, 182, 212, 0.1)', border: '1px solid rgba(6, 182, 212, 0.15)' }}
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#22d3ee" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
                </svg>
              </div>
              <span className="text-[13px] font-medium" style={{ color: 'var(--text-muted)' }}>Quick Start</span>
            </div>
            <Link href="/projects/new"
              className="text-sm font-semibold transition-colors duration-200 flex items-center gap-1.5"
              style={{ color: '#22d3ee' }}
            >
              Create new project
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
                className="transition-transform duration-200 group-hover:translate-x-1"
              >
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </Link>
          </div>
        </div>
      </div>

      {/* Recent projects */}
      <div className="rounded-2xl overflow-hidden glass-card">
        <div className="flex items-center justify-between px-6 py-4"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div className="flex items-center gap-3">
            <div className="w-1 h-4 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
            <span className="text-sm font-semibold" style={{ color: 'var(--section-title-color)' }}>Recent Projects</span>
          </div>
          <Link
            href="/projects"
            className="text-xs font-medium transition-colors duration-200 flex items-center gap-1"
            style={{ color: 'var(--text-faint)' }}
            onMouseEnter={(e) => { e.currentTarget.style.color = 'var(--link-hover-color)'; }}
            onMouseLeave={(e) => { e.currentTarget.style.color = 'var(--text-faint)'; }}
          >
            View all
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </Link>
        </div>

        {loading ? (
          <div className="p-5 space-y-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="shimmer h-12 rounded-xl" />
            ))}
          </div>
        ) : projects.length === 0 ? (
          <div className="px-6 py-14 text-center">
            <div className="w-14 h-14 mx-auto mb-4 rounded-2xl flex items-center justify-center"
              style={{ background: 'rgba(99, 102, 241, 0.06)', border: '1px solid rgba(99, 102, 241, 0.1)' }}
            >
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"
                style={{ color: 'var(--text-faint)' }}
              >
                <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
              </svg>
            </div>
            <p className="text-sm font-medium mb-1.5" style={{ color: 'var(--text-secondary)' }}>No projects yet</p>
            <p className="text-[13px] mb-5" style={{ color: 'var(--text-faint)' }}>Create your first project to get started</p>
            <Link
              href="/projects/new"
              className="inline-flex items-center gap-2 text-sm font-medium px-5 py-2.5 rounded-xl btn-gradient"
            >
              <span>Create your first project</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </Link>
          </div>
        ) : (
          <div>
            {projects.slice(0, 5).map((p, idx) => (
              <div
                key={p.id}
                className="flex items-center justify-between px-6 py-3.5 transition-all duration-200 group"
                style={{
                  borderBottom: idx < Math.min(projects.length, 5) - 1 ? '1px solid var(--border-subtle)' : 'none',
                }}
                onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--glow-violet)'; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = 'transparent'; }}
              >
                {confirmingId === p.id ? (
                  <div className="flex items-center gap-3 w-full">
                    <span className="text-sm" style={{ color: 'var(--text-primary)' }}>
                      Delete <strong>{p.name}</strong>?
                    </span>
                    {deleteError && <span className="text-xs" style={{ color: 'var(--error-text)' }}>{deleteError}</span>}
                    <div className="ml-auto flex items-center gap-2">
                      <button
                        onClick={() => handleDelete(p.id)}
                        disabled={deletingId === p.id}
                        className="text-xs font-medium px-3 py-1 rounded-lg transition-colors duration-200 disabled:opacity-50"
                        style={{
                          background: 'var(--error-bg)',
                          border: '1px solid var(--error-border)',
                          color: 'var(--error-text)',
                        }}
                      >
                        {deletingId === p.id ? "Deleting..." : "Yes"}
                      </button>
                      <button
                        onClick={() => { setConfirmingId(null); setDeleteError(""); }}
                        className="text-xs font-medium px-3 py-1 rounded-lg transition-colors duration-200"
                        style={{
                          background: 'var(--hover-nav-bg)',
                          border: '1px solid var(--border)',
                          color: 'var(--text-secondary)',
                        }}
                      >
                        No
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <Link
                      href={`/projects/${p.id}`}
                      className="flex items-center gap-3 flex-1 min-w-0"
                    >
                      <div className="w-8 h-8 rounded-lg flex items-center justify-center text-[11px] font-bold"
                        style={{
                          background: 'var(--project-initial-bg)',
                          color: 'var(--project-initial-text)',
                          border: '1px solid var(--project-initial-border)',
                        }}
                      >
                        {p.name.charAt(0).toUpperCase()}
                      </div>
                      <span className="text-sm font-medium" style={{ color: 'var(--project-name-color)' }}>{p.name}</span>
                    </Link>
                    <div className="flex items-center gap-3">
                      <span className="text-xs" style={{ color: 'var(--project-date-color)' }}>
                        {new Date(p.createdAt).toLocaleDateString()}
                      </span>
                      <button
                        onClick={() => { setConfirmingId(p.id); setDeleteError(""); }}
                        className="w-6 h-6 rounded-md flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all duration-200"
                        style={{
                          color: 'var(--error-text)',
                          background: 'var(--error-bg)',
                        }}
                        title="Delete project"
                        aria-label="Delete project"
                      >
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <polyline points="3 6 5 6 21 6" />
                          <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
                        </svg>
                      </button>
                      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
                        className="transition-all duration-200 opacity-0 group-hover:opacity-100 group-hover:translate-x-0.5"
                        style={{ color: 'var(--chevron-color)' }}
                      >
                        <polyline points="9 18 15 12 9 6" />
                      </svg>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
