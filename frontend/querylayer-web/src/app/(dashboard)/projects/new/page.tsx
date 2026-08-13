"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { createProject } from "../../../../services/api";

export default function NewProjectPage() {
  const [name, setName] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const router = useRouter();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const project = await createProject(name);
      router.push(`/projects/${project.id}`);
    } catch (err: unknown) {
      const data =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; detail?: string } } }).response?.data
          : undefined;
      setError(data?.detail || data?.error || "Failed to create project");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-lg fade-in">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero">New project</h1>
        <p className="text-sm mt-2" style={{ color: 'var(--text-muted)' }}>Configure your new backend project</p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="rounded-2xl p-7 space-y-6 glass-card"
      >
        {error && (
          <div className="text-sm px-4 py-3 rounded-xl"
            style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error-text)' }}
          >
            {error}
          </div>
        )}

        <div>
          <label className="block text-[13px] font-medium mb-2" style={{ color: 'var(--text-muted)' }}>Project name</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="my-backend"
            className="w-full rounded-xl px-4 py-2.5 text-sm input-glow focus:outline-none"
            style={{
              background: 'var(--input-bg)',
              border: '1px solid var(--input-border)',
              color: 'var(--text-primary)',
            }}
          />
          <p className="text-[12px] mt-2" style={{ color: 'var(--text-faint)' }}>Use lowercase letters, numbers, and hyphens</p>
        </div>

        <div className="flex gap-3 pt-1">
          <button
            type="submit"
            disabled={loading}
            className="px-5 py-2.5 rounded-xl text-sm font-semibold btn-gradient disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <span className="flex items-center gap-2">
              {loading ? (
                <>
                  <span className="animate-spin w-3.5 h-3.5 rounded-full"
                    style={{ border: '2px solid currentColor', borderTopColor: 'transparent', opacity: 0.85 }}
                  />
                  Creating...
                </>
              ) : (
                "Create project"
              )}
            </span>
          </button>
          <button
            type="button"
            onClick={() => router.back()}
            className="px-5 py-2.5 rounded-xl text-sm font-medium transition-all duration-200"
            style={{
              background: 'var(--hover-nav-bg)',
              border: '1px solid var(--border)',
              color: 'var(--text-muted)',
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = 'var(--bg-hover)';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = 'var(--hover-nav-bg)';
            }}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
