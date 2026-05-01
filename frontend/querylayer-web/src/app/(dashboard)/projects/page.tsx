"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getProjects } from "../../../services/api";
import ProjectCard from "../../../components/ProjectCard";
import type { Project } from "../../../types";

export default function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getProjects()
      .then(setProjects)
      .catch((err) => setError(err.response?.data?.error || "Failed to load projects"))
      .finally(() => setLoading(false));
  }, []);

  const handleProjectDeleted = (id: string) => {
    setProjects((prev) => prev.filter((p) => p.id !== id));
  };

  return (
    <div className="fade-in max-w-6xl">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">Projects</h1>
          <p className="text-sm mt-2" style={{ color: 'var(--text-muted)' }}>All your backend projects in one place</p>
        </div>
        <Link
          href="/projects/new"
          className="text-sm px-5 py-2.5 rounded-xl font-semibold btn-gradient flex items-center gap-2"
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          <span>New project</span>
        </Link>
      </div>

      {error && (
        <div className="text-sm px-5 py-3.5 rounded-xl mb-5"
          style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error-text)' }}
        >
          {error}
        </div>
      )}

      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {[1, 2, 3].map((i) => (
            <div key={i} className="shimmer rounded-2xl h-32 glass-card" />
          ))}
        </div>
      ) : projects.length === 0 ? (
        <div className="rounded-2xl p-10 glass-card relative overflow-hidden">
          {/* Background glow */}
          <div className="absolute top-0 right-0 w-64 h-64 rounded-full opacity-30"
            style={{ background: 'radial-gradient(circle, rgba(99, 102, 241, 0.12), transparent)', filter: 'blur(40px)' }}
          />

          <div className="relative">
            <div className="w-14 h-14 rounded-2xl flex items-center justify-center mb-5"
              style={{ background: 'rgba(99, 102, 241, 0.08)', border: '1px solid rgba(99, 102, 241, 0.12)' }}
            >
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#818cf8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
              </svg>
            </div>

            <h2 className="text-lg font-semibold mb-2" style={{ color: 'var(--section-title-color)' }}>No projects yet</h2>
            <p className="text-sm mb-8" style={{ color: 'var(--text-muted)' }}>
              Create your first project to get a fully generated backend -- database, API, and docs -- in minutes.
            </p>

            <ol className="space-y-4 mb-8">
              {[
                { step: "1", title: "Create a project", desc: "Give it a name to get started.", color: '#6366f1' },
                { step: "2", title: "Describe your backend", desc: "Use AI to generate a spec from plain English.", color: '#818cf8' },
                { step: "3", title: "Save the spec", desc: "Tables are created and APIs go live instantly.", color: '#a5b4fc' },
                { step: "4", title: "Test your API", desc: "Use the API Explorer to run requests.", color: '#06b6d4' },
              ].map(({ step, title, desc, color }) => (
                <li key={step} className="flex gap-4 items-start">
                  <span className="flex-shrink-0 w-7 h-7 rounded-lg text-xs font-bold flex items-center justify-center mt-0.5"
                    style={{ background: `${color}15`, color, border: `1px solid ${color}25` }}
                  >
                    {step}
                  </span>
                  <div>
                    <p className="text-sm font-medium" style={{ color: 'var(--project-name-color)' }}>{title}</p>
                    <p className="text-[13px] mt-0.5" style={{ color: 'var(--text-faint)' }}>{desc}</p>
                  </div>
                </li>
              ))}
            </ol>

            <Link
              href="/projects/new"
              className="inline-flex items-center gap-2 text-sm px-6 py-3 rounded-xl font-semibold btn-gradient"
            >
              <span>Create first project</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </Link>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {projects.map((p) => (
            <ProjectCard key={p.id} project={p} onDeleted={handleProjectDeleted} />
          ))}
        </div>
      )}
    </div>
  );
}
