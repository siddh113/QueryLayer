"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { getProject, getSpec } from "../../../../services/api";
import SpecEditor from "../../../../components/SpecEditor";
import ApiExplorer from "../../../../components/ApiExplorer";
import type { Project, BackendSpec } from "../../../../types";

type Tab = "overview" | "spec" | "explorer";

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [spec, setSpec] = useState<BackendSpec | null>(null);
  const [specVersion, setSpecVersion] = useState(0);
  const [tab, setTab] = useState<Tab>("overview");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!id) return;
    Promise.all([
      getProject(id).catch(() => null),
      getSpec(id).catch(() => null),
    ]).then(([proj, specData]) => {
      if (proj) setProject(proj);
      else setError("Project not found");
      if (specData) {
        try {
          setSpec(JSON.parse(specData.specJson));
          setSpecVersion(specData.version);
        } catch {
          setSpec({ entities: [], endpoints: [], permissions: [] });
        }
      }
      setLoading(false);
    });
  }, [id]);

  if (loading) return <div className="text-gray-500 text-sm">Loading project...</div>;
  if (error) return <div className="text-red-600 text-sm">{error}</div>;
  if (!project) return null;

  const tabs: { key: Tab; label: string }[] = [
    { key: "overview", label: "Overview" },
    { key: "spec", label: "Backend Spec" },
    { key: "explorer", label: "API Explorer" },
  ];

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">{project.name}</h1>
        <p className="text-xs font-mono text-gray-400 mt-1">{project.id}</p>
      </div>

      <div className="flex gap-1 border-b border-gray-200 mb-6">
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              tab === t.key
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-gray-500 hover:text-gray-700"
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === "overview" && (
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <h2 className="font-semibold text-gray-900 mb-4">Project Info</h2>
          <dl className="space-y-3 text-sm">
            <div>
              <dt className="text-gray-500">Name</dt>
              <dd className="text-gray-900">{project.name}</dd>
            </div>
            <div>
              <dt className="text-gray-500">ID</dt>
              <dd className="font-mono text-gray-900">{project.id}</dd>
            </div>
            <div>
              <dt className="text-gray-500">Created</dt>
              <dd className="text-gray-900">{new Date(project.createdAt).toLocaleString()}</dd>
            </div>
            <div>
              <dt className="text-gray-500">Spec Version</dt>
              <dd className="text-gray-900">{specVersion || "No spec uploaded"}</dd>
            </div>
            <div>
              <dt className="text-gray-500">API Key (Project ID)</dt>
              <dd className="flex items-center gap-2">
                <code className="font-mono text-xs bg-gray-100 px-2 py-1 rounded text-gray-900">
                  {project.id}
                </code>
                <button
                  onClick={() => navigator.clipboard.writeText(project.id)}
                  className="text-xs text-blue-600 hover:text-blue-800"
                >
                  Copy
                </button>
              </dd>
            </div>
          </dl>
        </div>
      )}

      {tab === "spec" && (
        <SpecEditor
          projectId={project.id}
          initialSpec={spec}
          onSaved={(newSpec, version) => {
            setSpec(newSpec);
            setSpecVersion(version);
          }}
        />
      )}

      {tab === "explorer" && (
        <ApiExplorer projectId={project.id} spec={spec} />
      )}
    </div>
  );
}
