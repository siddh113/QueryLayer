"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getProjects } from "../../../services/api";
import type { Project } from "../../../types";

export default function DashboardPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getProjects()
      .then(setProjects)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="fade-in max-w-4xl">
      <div className="mb-7">
        <h1 className="text-lg font-semibold" style={{ color: "#ededed" }}>Overview</h1>
        <p className="text-sm mt-0.5" style={{ color: "#525252" }}>Your QueryLayer workspace</p>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-6">
        <div
          className="rounded-lg p-4"
          style={{ background: "#141414", border: "1px solid #262626" }}
        >
          <div className="text-xs mb-2" style={{ color: "#525252" }}>Total Projects</div>
          <div className="text-2xl font-semibold" style={{ color: "#ededed" }}>
            {loading ? <span className="shimmer inline-block w-6 h-6 rounded" /> : projects.length}
          </div>
        </div>

        <div
          className="rounded-lg p-4"
          style={{ background: "#141414", border: "1px solid #262626" }}
        >
          <div className="text-xs mb-2" style={{ color: "#525252" }}>Status</div>
          <div className="flex items-center gap-2">
            <span className="w-1.5 h-1.5 rounded-full pulse-dot" style={{ background: "#3ecf8e" }} />
            <span className="text-sm font-medium" style={{ color: "#3ecf8e" }}>Operational</span>
          </div>
        </div>

        <div
          className="rounded-lg p-4"
          style={{ background: "#141414", border: "1px solid #262626" }}
        >
          <div className="text-xs mb-2" style={{ color: "#525252" }}>Quick Start</div>
          <Link
            href="/projects/new"
            className="text-sm font-medium transition-colors"
            style={{ color: "#3ecf8e" }}
            onMouseEnter={(e) => { e.currentTarget.style.color = "#5de0a3"; }}
            onMouseLeave={(e) => { e.currentTarget.style.color = "#3ecf8e"; }}
          >
            New project →
          </Link>
        </div>
      </div>

      {/* Recent projects */}
      <div className="rounded-lg overflow-hidden" style={{ background: "#141414", border: "1px solid #262626" }}>
        <div
          className="flex items-center justify-between px-4 py-3"
          style={{ borderBottom: "1px solid #1f1f1f" }}
        >
          <span className="text-sm font-medium" style={{ color: "#ededed" }}>Recent projects</span>
          <Link
            href="/projects"
            className="text-xs transition-colors"
            style={{ color: "#525252" }}
            onMouseEnter={(e) => { e.currentTarget.style.color = "#a1a1a1"; }}
            onMouseLeave={(e) => { e.currentTarget.style.color = "#525252"; }}
          >
            View all
          </Link>
        </div>

        {loading ? (
          <div className="p-4 space-y-2">
            {[1, 2, 3].map((i) => (
              <div key={i} className="shimmer h-9 rounded" style={{ background: "#1a1a1a" }} />
            ))}
          </div>
        ) : projects.length === 0 ? (
          <div className="px-4 py-8 text-center">
            <p className="text-sm mb-3" style={{ color: "#525252" }}>No projects yet</p>
            <Link
              href="/projects/new"
              className="text-sm transition-colors"
              style={{ color: "#3ecf8e" }}
              onMouseEnter={(e) => { e.currentTarget.style.color = "#5de0a3"; }}
              onMouseLeave={(e) => { e.currentTarget.style.color = "#3ecf8e"; }}
            >
              Create your first project →
            </Link>
          </div>
        ) : (
          <div>
            {projects.slice(0, 5).map((p, idx) => (
              <Link
                key={p.id}
                href={`/projects/${p.id}`}
                className="flex items-center justify-between px-4 py-3 transition-colors"
                style={{ borderBottom: idx < Math.min(projects.length, 5) - 1 ? "1px solid #1a1a1a" : "none" }}
                onMouseEnter={(e) => { e.currentTarget.style.background = "#1a1a1a"; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = ""; }}
              >
                <span className="text-sm" style={{ color: "#d4d4d4" }}>{p.name}</span>
                <span className="text-xs" style={{ color: "#525252" }}>
                  {new Date(p.createdAt).toLocaleDateString()}
                </span>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
