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

  return (
    <div className="fade-in max-w-5xl">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-lg font-semibold" style={{ color: "#ededed" }}>Projects</h1>
          <p className="text-sm mt-0.5" style={{ color: "#525252" }}>All your backend projects</p>
        </div>
        <Link
          href="/projects/new"
          className="text-sm px-3 py-1.5 rounded-md transition-colors font-medium"
          style={{ background: "#3ecf8e", color: "#0f0f0f" }}
          onMouseEnter={(e) => { e.currentTarget.style.background = "#5de0a3"; }}
          onMouseLeave={(e) => { e.currentTarget.style.background = "#3ecf8e"; }}
        >
          New project
        </Link>
      </div>

      {error && (
        <div
          className="text-sm px-4 py-3 rounded-md mb-4"
          style={{
            background: "rgba(248,113,113,0.06)",
            border: "1px solid rgba(248,113,113,0.15)",
            color: "#f87171",
          }}
        >
          {error}
        </div>
      )}

      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="shimmer rounded-lg" style={{ height: "96px", background: "#141414", border: "1px solid #262626" }} />
          ))}
        </div>
      ) : projects.length === 0 ? (
        <div className="rounded-lg p-8" style={{ background: "#141414", border: "1px solid #262626" }}>
          <h2 className="text-base font-semibold mb-1.5" style={{ color: "#ededed" }}>No projects yet</h2>
          <p className="text-sm mb-6" style={{ color: "#525252" }}>
            Create your first project to get a fully generated backend — database, API, and docs — in minutes.
          </p>
          <ol className="space-y-3 mb-7">
            {[
              { step: "1", title: "Create a project", desc: "Give it a name to get started." },
              { step: "2", title: "Describe your backend", desc: "Use AI to generate a spec from plain English." },
              { step: "3", title: "Save the spec", desc: "Tables are created and APIs go live instantly." },
              { step: "4", title: "Test your API", desc: "Use the API Explorer to run requests." },
            ].map(({ step, title, desc }) => (
              <li key={step} className="flex gap-3 items-start">
                <span
                  className="flex-shrink-0 w-5 h-5 rounded text-xs font-bold flex items-center justify-center mt-0.5"
                  style={{ background: "#1f1f1f", color: "#3ecf8e", border: "1px solid #2a2a2a" }}
                >
                  {step}
                </span>
                <div>
                  <p className="text-sm font-medium" style={{ color: "#d4d4d4" }}>{title}</p>
                  <p className="text-xs mt-0.5" style={{ color: "#525252" }}>{desc}</p>
                </div>
              </li>
            ))}
          </ol>
          <Link
            href="/projects/new"
            className="inline-block text-sm px-4 py-2 rounded-md font-medium transition-colors"
            style={{ background: "#3ecf8e", color: "#0f0f0f" }}
            onMouseEnter={(e) => { e.currentTarget.style.background = "#5de0a3"; }}
            onMouseLeave={(e) => { e.currentTarget.style.background = "#3ecf8e"; }}
          >
            Create first project
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {projects.map((p) => (
            <ProjectCard key={p.id} project={p} />
          ))}
        </div>
      )}
    </div>
  );
}
