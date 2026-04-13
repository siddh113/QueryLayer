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
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Projects</h1>
        <Link
          href="/projects/new"
          className="bg-blue-600 text-white text-sm px-4 py-2 rounded hover:bg-blue-700 transition-colors"
        >
          Create Project
        </Link>
      </div>

      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded px-4 py-3 mb-4">
          {error}
        </div>
      )}

      {loading ? (
        <div className="text-gray-500 text-sm">Loading projects...</div>
      ) : projects.length === 0 ? (
        <div className="bg-white rounded-lg border border-gray-200 p-8">
          <h2 className="text-lg font-semibold text-gray-900 mb-2">Welcome to QueryLayer</h2>
          <p className="text-sm text-gray-500 mb-6">
            Create your first project to get a fully generated backend — database, API, and docs — in minutes.
          </p>
          <ol className="space-y-4 mb-8">
            {[
              { step: "1", title: "Create a project", desc: "Give it a name to get started." },
              { step: "2", title: "Describe your backend", desc: "Use AI to generate a spec from plain English." },
              { step: "3", title: "Save the spec", desc: "Tables are created and APIs go live instantly." },
              { step: "4", title: "Test your API", desc: "Use the API Explorer to run requests." },
            ].map(({ step, title, desc }) => (
              <li key={step} className="flex gap-4 items-start">
                <span className="flex-shrink-0 w-7 h-7 rounded-full bg-blue-600 text-white text-sm font-bold flex items-center justify-center">
                  {step}
                </span>
                <div>
                  <p className="text-sm font-medium text-gray-900">{title}</p>
                  <p className="text-xs text-gray-500">{desc}</p>
                </div>
              </li>
            ))}
          </ol>
          <Link
            href="/projects/new"
            className="inline-block bg-blue-600 text-white text-sm px-5 py-2 rounded hover:bg-blue-700 transition-colors"
          >
            Create your first project
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {projects.map((p) => (
            <ProjectCard key={p.id} project={p} />
          ))}
        </div>
      )}
    </div>
  );
}
