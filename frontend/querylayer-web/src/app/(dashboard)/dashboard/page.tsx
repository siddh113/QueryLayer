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
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Dashboard</h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <div className="text-sm text-gray-500">Total Projects</div>
          <div className="text-3xl font-bold text-gray-900 mt-1">
            {loading ? "-" : projects.length}
          </div>
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <div className="text-sm text-gray-500">Status</div>
          <div className="text-lg font-semibold text-green-600 mt-1">Active</div>
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <div className="text-sm text-gray-500">Quick Actions</div>
          <Link
            href="/projects/new"
            className="inline-block mt-2 text-sm text-blue-600 hover:text-blue-800"
          >
            Create new project
          </Link>
        </div>
      </div>

      <div className="bg-white rounded-lg border border-gray-200">
        <div className="px-5 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="font-semibold text-gray-900">Recent Projects</h2>
          <Link href="/projects" className="text-sm text-blue-600 hover:text-blue-800">
            View all
          </Link>
        </div>
        {loading ? (
          <div className="p-5 text-gray-500 text-sm">Loading projects...</div>
        ) : projects.length === 0 ? (
          <div className="p-5 text-gray-500 text-sm">
            No projects yet.{" "}
            <Link href="/projects/new" className="text-blue-600 hover:text-blue-800">
              Create one
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {projects.slice(0, 5).map((p) => (
              <Link
                key={p.id}
                href={`/projects/${p.id}`}
                className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors"
              >
                <span className="text-sm font-medium text-gray-900">{p.name}</span>
                <span className="text-xs text-gray-400">
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
