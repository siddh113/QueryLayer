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
        <p className="text-sm mt-2" style={{ color: '#71717a' }}>Configure your new backend project</p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="rounded-2xl p-7 space-y-6 glass-card"
        style={{ boxShadow: '0 0 40px rgba(0, 0, 0, 0.3)' }}
      >
        {error && (
          <div className="text-sm px-4 py-3 rounded-xl"
            style={{ background: 'rgba(239, 68, 68, 0.08)', border: '1px solid rgba(239, 68, 68, 0.15)', color: '#f87171' }}
          >
            {error}
          </div>
        )}

        <div>
          <label className="block text-[13px] font-medium mb-2" style={{ color: '#a1a1aa' }}>Project name</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="my-backend"
            className="w-full rounded-xl px-4 py-2.5 text-sm input-glow focus:outline-none"
            style={{
              background: 'rgba(7, 7, 14, 0.6)',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              color: '#f0f0f5',
            }}
          />
          <p className="text-[12px] mt-2" style={{ color: '#3f3f46' }}>Use lowercase letters, numbers, and hyphens</p>
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
                    style={{ border: '2px solid rgba(255,255,255,0.3)', borderTopColor: 'white' }}
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
              background: 'rgba(255, 255, 255, 0.04)',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              color: '#a1a1aa',
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = 'rgba(255, 255, 255, 0.06)';
              e.currentTarget.style.borderColor = 'rgba(255, 255, 255, 0.12)';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = 'rgba(255, 255, 255, 0.04)';
              e.currentTarget.style.borderColor = 'rgba(255, 255, 255, 0.08)';
            }}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
