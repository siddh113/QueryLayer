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
      <div className="mb-6">
        <h1 className="text-lg font-semibold" style={{ color: "#ededed" }}>New project</h1>
        <p className="text-sm mt-0.5" style={{ color: "#525252" }}>Configure your new backend project</p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="rounded-lg p-5 space-y-4"
        style={{ background: "#141414", border: "1px solid #262626" }}
      >
        {error && (
          <div
            className="text-sm px-3 py-2 rounded-md"
            style={{
              background: "rgba(248,113,113,0.06)",
              border: "1px solid rgba(248,113,113,0.15)",
              color: "#f87171",
            }}
          >
            {error}
          </div>
        )}

        <div>
          <label className="block text-sm mb-1.5" style={{ color: "#a1a1a1" }}>Project name</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="my-backend"
            style={{
              width: "100%",
              background: "#0f0f0f",
              border: "1px solid #262626",
              borderRadius: "6px",
              padding: "8px 12px",
              fontSize: "14px",
              color: "#ededed",
              outline: "none",
              transition: "border-color 0.15s",
            }}
            onFocus={(e) => { e.target.style.borderColor = "#3ecf8e"; }}
            onBlur={(e) => { e.target.style.borderColor = "#262626"; }}
          />
        </div>

        <div className="flex gap-2 pt-1">
          <button
            type="submit"
            disabled={loading}
            className="px-4 py-2 rounded-md text-sm font-medium transition-colors"
            style={{
              background: loading ? "#1f1f1f" : "#3ecf8e",
              color: loading ? "#525252" : "#0f0f0f",
              cursor: loading ? "not-allowed" : "pointer",
              border: "none",
            }}
            onMouseEnter={(e) => { if (!loading) e.currentTarget.style.background = "#5de0a3"; }}
            onMouseLeave={(e) => { if (!loading) e.currentTarget.style.background = loading ? "#1f1f1f" : "#3ecf8e"; }}
          >
            {loading ? (
              <span className="flex items-center gap-2">
                <span className="animate-spin w-3.5 h-3.5 border border-current border-t-transparent rounded-full" />
                Creating...
              </span>
            ) : (
              "Create project"
            )}
          </button>
          <button
            type="button"
            onClick={() => router.back()}
            className="px-4 py-2 rounded-md text-sm transition-colors"
            style={{ background: "transparent", border: "1px solid #262626", color: "#737373", cursor: "pointer" }}
            onMouseEnter={(e) => { e.currentTarget.style.borderColor = "#333333"; e.currentTarget.style.color = "#a1a1a1"; }}
            onMouseLeave={(e) => { e.currentTarget.style.borderColor = "#262626"; e.currentTarget.style.color = "#737373"; }}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
