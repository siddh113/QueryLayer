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
        <div className="bg-white rounded-lg border border-gray-200 p-8 text-center">
          <p className="text-gray-500 mb-4">No projects yet.</p>
          <Link
            href="/projects/new"
            className="text-blue-600 hover:text-blue-800 text-sm"
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
