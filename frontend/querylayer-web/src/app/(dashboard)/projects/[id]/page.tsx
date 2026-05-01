"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { getProject, getSpec, getExamples } from "../../../../services/api";
import type { EndpointExample } from "../../../../services/api";
import SpecEditor from "../../../../components/SpecEditor";
import ApiExplorer from "../../../../components/ApiExplorer";
import AISpecGenerator from "../../../../components/AISpecGenerator";
import AISpecEditor from "../../../../components/AISpecEditor";
import ApiExamplesPanel from "../../../../components/ApiExamplesPanel";
import ProjectKeysPanel from "../../../../components/ProjectKeysPanel";
import FrontendIntegrationGuide from "../../../../components/FrontendIntegrationGuide";
import type { Project, BackendSpec } from "../../../../types";

type Tab = "overview" | "ai" | "spec" | "explorer" | "examples" | "keys" | "integration";

const tabIcons: Record<Tab, React.ReactNode> = {
  overview: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  ),
  ai: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
    </svg>
  ),
  spec: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
    </svg>
  ),
  explorer: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z" />
    </svg>
  ),
  examples: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" />
    </svg>
  ),
  keys: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 11-7.778 7.778 5.5 5.5 0 010-7.778L11.39 4m3.93 8.39L19.5 8.22" />
    </svg>
  ),
  integration: (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10 13a5 5 0 007.54.54l3-3a5 5 0 00-7.07-7.07l-1.72 1.71" /><path d="M14 11a5 5 0 00-7.54-.54l-3 3a5 5 0 007.07 7.07l1.71-1.71" />
    </svg>
  ),
};

export default function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [spec, setSpec] = useState<BackendSpec | null>(null);
  const [specVersion, setSpecVersion] = useState(0);
  const [examples, setExamples] = useState<EndpointExample[]>([]);
  const [tab, setTab] = useState<Tab>("overview");
  const [aiSubTab, setAiSubTab] = useState<"generate" | "edit">("generate");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!id) return;
    Promise.all([
      getProject(id).catch(() => null),
      getSpec(id).catch(() => null),
      getExamples(id).catch(() => []),
    ]).then(([proj, specData, exampleData]) => {
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
      if (exampleData) setExamples(exampleData);
      setLoading(false);
    });
  }, [id]);

  if (loading) {
    return (
      <div className="flex items-center gap-3 text-sm" style={{ color: '#52525b' }}>
        <span className="animate-spin w-4 h-4 rounded-full"
          style={{ border: '2px solid rgba(99, 102, 241, 0.15)', borderTopColor: '#6366f1' }}
        />
        Loading project...
      </div>
    );
  }
  if (error) return (
    <div className="text-sm px-5 py-3.5 rounded-xl"
      style={{ background: 'rgba(239, 68, 68, 0.08)', border: '1px solid rgba(239, 68, 68, 0.15)', color: '#f87171' }}
    >
      {error}
    </div>
  );
  if (!project) return null;

  const tabs: { key: Tab; label: string }[] = [
    { key: "overview", label: "Overview" },
    { key: "ai", label: "AI Assistant" },
    { key: "spec", label: "Backend Spec" },
    { key: "explorer", label: "API Explorer" },
    { key: "examples", label: "Code Examples" },
    { key: "keys", label: "API Keys" },
    { key: "integration", label: "Integration" },
  ];

  return (
    <div className="fade-in">
      {/* Project header */}
      <div className="mb-8">
        <div className="flex items-center gap-4 mb-2">
          <div className="w-10 h-10 rounded-xl flex items-center justify-center text-sm font-bold"
            style={{
              background: 'linear-gradient(135deg, rgba(99, 102, 241, 0.15), rgba(6, 182, 212, 0.08))',
              color: '#a5b4fc',
              border: '1px solid rgba(99, 102, 241, 0.15)',
            }}
          >
            {project.name.charAt(0).toUpperCase()}
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight" style={{ color: '#f0f0f5' }}>{project.name}</h1>
            <p className="text-[11px] font-mono mt-0.5" style={{ color: '#3f3f46' }}>{project.id}</p>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 mb-8 pb-px overflow-x-auto"
        style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.06)' }}
      >
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className="flex items-center gap-2 px-4 py-3 text-[13px] font-medium transition-all duration-200 whitespace-nowrap relative"
            style={{
              color: tab === t.key ? '#a5b4fc' : '#52525b',
              borderBottom: tab === t.key ? '2px solid transparent' : '2px solid transparent',
              borderImage: tab === t.key ? 'linear-gradient(to right, #6366f1, #06b6d4) 1' : 'none',
            }}
            onMouseEnter={(e) => {
              if (tab !== t.key) e.currentTarget.style.color = '#a1a1aa';
            }}
            onMouseLeave={(e) => {
              if (tab !== t.key) e.currentTarget.style.color = '#52525b';
            }}
          >
            <span style={{ opacity: tab === t.key ? 1 : 0.6 }}>{tabIcons[t.key]}</span>
            {t.label}
          </button>
        ))}
      </div>

      {tab === "overview" && (
        <div className="rounded-2xl glass-card p-7 max-w-2xl"
          style={{ boxShadow: '0 0 30px rgba(0, 0, 0, 0.2)' }}
        >
          <div className="flex items-center gap-3 mb-6">
            <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
            <h2 className="font-semibold text-[15px]" style={{ color: '#f0f0f5' }}>Project Info</h2>
          </div>
          <dl className="space-y-5 text-sm">
            {[
              { label: "Name", value: project.name },
              { label: "ID", value: project.id, mono: true },
              { label: "Created", value: new Date(project.createdAt).toLocaleString() },
              { label: "Spec Version", value: specVersion ? `v${specVersion}` : "No spec uploaded" },
            ].map(({ label, value, mono }) => (
              <div key={label} className="flex items-start justify-between py-2"
                style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.03)' }}
              >
                <dt className="font-medium" style={{ color: '#52525b' }}>{label}</dt>
                <dd className={`${mono ? 'font-mono text-[13px]' : ''}`} style={{ color: '#e4e4e7' }}>
                  {value}
                </dd>
              </div>
            ))}
            <div className="flex items-start justify-between py-2">
              <dt className="font-medium" style={{ color: '#52525b' }}>API Key (Project ID)</dt>
              <dd className="flex items-center gap-3">
                <code className="font-mono text-xs px-3 py-1.5 rounded-lg"
                  style={{
                    background: 'rgba(7, 7, 14, 0.6)',
                    border: '1px solid rgba(255, 255, 255, 0.06)',
                    color: '#e4e4e7',
                  }}
                >
                  {project.id}
                </code>
                <button
                  onClick={() => navigator.clipboard.writeText(project.id)}
                  className="text-xs font-medium transition-colors duration-200"
                  style={{ color: '#818cf8' }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = '#a5b4fc'; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = '#818cf8'; }}
                >
                  Copy
                </button>
              </dd>
            </div>
          </dl>
        </div>
      )}

      {tab === "ai" && (
        <div className="space-y-5">
          <div className="flex gap-2 mb-6">
            {(["generate", "edit"] as const).map((sub) => (
              <button
                key={sub}
                onClick={() => setAiSubTab(sub)}
                className="px-4 py-2.5 text-[13px] font-medium rounded-xl transition-all duration-200"
                style={{
                  background: aiSubTab === sub
                    ? 'linear-gradient(135deg, rgba(99, 102, 241, 0.12), rgba(6, 182, 212, 0.06))'
                    : 'rgba(255, 255, 255, 0.03)',
                  color: aiSubTab === sub ? '#a5b4fc' : '#52525b',
                  border: aiSubTab === sub
                    ? '1px solid rgba(99, 102, 241, 0.2)'
                    : '1px solid rgba(255, 255, 255, 0.04)',
                  boxShadow: aiSubTab === sub ? '0 0 12px rgba(99, 102, 241, 0.06)' : 'none',
                }}
              >
                {sub === "generate" ? "Generate Spec" : "Edit Spec"}
              </button>
            ))}
          </div>

          {aiSubTab === "generate" && (
            <AISpecGenerator
              projectId={project.id}
              onSpecSaved={(newSpec, version) => {
                setSpec(newSpec);
                setSpecVersion(version);
              }}
            />
          )}

          {aiSubTab === "edit" && (
            <AISpecEditor
              projectId={project.id}
              currentSpec={spec}
              onSpecSaved={(newSpec, version) => {
                setSpec(newSpec);
                setSpecVersion(version);
              }}
            />
          )}
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

      {tab === "examples" && (
        <ApiExamplesPanel examples={examples} />
      )}

      {tab === "keys" && (
        <ProjectKeysPanel projectId={project.id} />
      )}

      {tab === "integration" && (
        <FrontendIntegrationGuide projectId={project.id} />
      )}
    </div>
  );
}
