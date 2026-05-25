"use client";

import { useState } from "react";
import { generateSpec, updateSpec } from "../services/api";
import type { BackendSpec } from "../types";
import SchemaChangesModal from "./SchemaChangesModal";
import SpecSaveProgressIndicator, { type SavePhase } from "./SpecSaveProgressIndicator";

interface AISpecGeneratorProps {
  projectId: string;
  onSpecSaved: (spec: BackendSpec, version: number) => void;
}

export default function AISpecGenerator({ projectId, onSpecSaved }: AISpecGeneratorProps) {
  const [prompt, setPrompt] = useState("");
  const [generating, setGenerating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);
  const [previewSpec, setPreviewSpec] = useState<BackendSpec | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [savePhase, setSavePhase] = useState<SavePhase>("idle");

  const handleGenerate = async () => {
    if (!prompt.trim()) return;
    setError("");
    setMessage("");
    setPreview(null);
    setSavePhase("generating");
    setGenerating(true);
    try {
      const result = await generateSpec(projectId, prompt);
      setPreview(JSON.stringify(result.spec, null, 2));
      setPreviewSpec(result.spec);
      setMessage("Spec generated. Review below, then confirm to preview schema changes.");
      setSavePhase("idle");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to generate spec.");
      setSavePhase("error");
    } finally {
      setGenerating(false);
    }
  };

  const handleOpenModal = () => {
    if (!preview) return;
    try {
      JSON.parse(preview);
    } catch {
      setError("Invalid JSON in preview. Fix before saving.");
      return;
    }
    setError("");
    setSavePhase("previewing");
    setShowModal(true);
  };

  const handleApply = async (editedSpec: BackendSpec) => {
    setSaving(true);
    setSavePhase("applying");
    try {
      const result = await updateSpec(projectId, editedSpec);
      setSavePhase("done");
      setMessage(`Spec saved (v${result.version}) and schema synchronized.`);
      setPreview(null);
      setPreviewSpec(null);
      setPrompt("");
      setShowModal(false);
      onSpecSaved(editedSpec, result.version);
      setTimeout(() => setSavePhase("idle"), 2000);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string; detail?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.detail || msg?.error || "Failed to save spec.");
      setSavePhase("error");
      setShowModal(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
    {showModal && preview && (
      <SchemaChangesModal
        projectId={projectId}
        specJson={preview}
        onApply={handleApply}
        onBack={() => { setShowModal(false); setSavePhase("idle"); }}
        isApplying={saving}
      />
    )}
    <div className="space-y-5">
      <div>
        <label className="block text-[13px] font-medium mb-2" style={{ color: '#a1a1aa' }}>
          Describe your backend
        </label>
        <textarea
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          placeholder="I want a task management app where users can create tasks, assign them to team members, and track progress with statuses like todo, in-progress, and done..."
          className="w-full h-36 text-sm rounded-xl p-4 resize-y input-glow focus:outline-none"
          style={{
            background: 'rgba(7, 7, 14, 0.6)',
            border: '1px solid rgba(255, 255, 255, 0.08)',
            color: '#f0f0f5',
          }}
        />
      </div>

      <button
        onClick={handleGenerate}
        disabled={generating || !prompt.trim()}
        className="text-sm px-5 py-2.5 rounded-xl font-semibold btn-gradient disabled:opacity-50 disabled:cursor-not-allowed"
        style={{ marginBottom: savePhase !== "idle" ? "0" : undefined }}
      >
        <span className="flex items-center gap-2">
          {generating ? (
            <>
              <span className="animate-spin w-4 h-4 rounded-full"
                style={{ border: '2px solid rgba(255,255,255,0.3)', borderTopColor: 'white' }}
              />
              Generating...
            </>
          ) : (
            <>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
              </svg>
              Generate Spec
            </>
          )}
        </span>
      </button>

      <SpecSaveProgressIndicator phase={savePhase} errorMessage={error} />

      {error && savePhase !== "error" && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: 'rgba(239, 68, 68, 0.08)', border: '1px solid rgba(239, 68, 68, 0.15)', color: '#f87171' }}
        >
          {error}
        </div>
      )}

      {message && !error && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: 'rgba(34, 197, 94, 0.06)', border: '1px solid rgba(34, 197, 94, 0.12)', color: '#4ade80' }}
        >
          {message}
        </div>
      )}

      {preview && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-1 h-4 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
              <h3 className="text-sm font-semibold" style={{ color: '#f0f0f5' }}>Preview (editable)</h3>
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => { setPreview(null); setPreviewSpec(null); setMessage(""); }}
                className="text-sm px-4 py-2 rounded-xl font-medium transition-all duration-200"
                style={{
                  background: 'rgba(255, 255, 255, 0.04)',
                  border: '1px solid rgba(255, 255, 255, 0.08)',
                  color: '#a1a1aa',
                }}
                onMouseEnter={(e) => { e.currentTarget.style.background = 'rgba(255, 255, 255, 0.06)'; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = 'rgba(255, 255, 255, 0.04)'; }}
              >
                Discard
              </button>
              <button
                onClick={handleOpenModal}
                disabled={saving || generating}
                className="text-sm px-5 py-2 rounded-xl font-semibold btn-gradient disabled:opacity-50"
              >
                <span>Review & Save</span>
              </button>
            </div>
          </div>
          <textarea
            value={preview}
            onChange={(e) => setPreview(e.target.value)}
            spellCheck={false}
            className="w-full h-[420px] font-mono text-sm rounded-xl p-5 resize-y focus:outline-none input-glow"
            style={{
              background: 'rgba(7, 7, 14, 0.8)',
              border: '1px solid rgba(255, 255, 255, 0.06)',
              color: '#4ade80',
            }}
          />
        </div>
      )}
    </div>
    </>
  );
}
