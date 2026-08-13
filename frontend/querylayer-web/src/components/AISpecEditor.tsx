"use client";

import { useState } from "react";
import { editSpec, updateSpec } from "../services/api";
import type { BackendSpec } from "../types";
import SchemaChangesModal from "./SchemaChangesModal";
import SpecSaveProgressIndicator, { type SavePhase } from "./SpecSaveProgressIndicator";

interface AISpecEditorProps {
  projectId: string;
  currentSpec: BackendSpec | null;
  onSpecSaved: (spec: BackendSpec, version: number) => void;
}

export default function AISpecEditor({ projectId, currentSpec, onSpecSaved }: AISpecEditorProps) {
  const [instruction, setInstruction] = useState("");
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [showDiff, setShowDiff] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [savePhase, setSavePhase] = useState<SavePhase>("idle");

  const oldSpecJson = currentSpec ? JSON.stringify(currentSpec, null, 2) : "";

  const handleEdit = async () => {
    if (!instruction.trim()) return;
    if (!currentSpec) {
      setError("No existing spec to edit. Generate one first.");
      return;
    }
    setError("");
    setMessage("");
    setPreview(null);
    setSavePhase("generating");
    setEditing(true);
    try {
      const result = await editSpec(projectId, instruction);
      setPreview(JSON.stringify(result.spec, null, 2));
      setMessage("Spec updated. Review changes, then confirm to preview schema changes.");
      setSavePhase("idle");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to edit spec.");
      setSavePhase("error");
    } finally {
      setEditing(false);
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
      setInstruction("");
      setShowDiff(false);
      setShowModal(false);
      onSpecSaved(editedSpec, result.version);
      setTimeout(() => setSavePhase("idle"), 2000);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to save spec.");
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
      {!currentSpec && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: 'var(--warning-bg)', border: '1px solid var(--warning-border)', color: 'var(--warning-text)' }}
        >
          No spec exists yet. Use the Generator tab to create one first.
        </div>
      )}

      <div>
        <label className="block text-[13px] font-medium mb-2" style={{ color: 'var(--text-muted)' }}>
          Modification instruction
        </label>
        <textarea
          value={instruction}
          onChange={(e) => setInstruction(e.target.value)}
          placeholder="Add a due_date timestamp field to Task and create a new Category entity with name field..."
          disabled={!currentSpec}
          className="w-full h-28 text-sm rounded-xl p-4 resize-y focus:outline-none input-glow disabled:opacity-40 disabled:cursor-not-allowed"
          style={{
            background: 'var(--input-bg)',
            border: '1px solid var(--input-border)',
            color: 'var(--text-primary)',
          }}
        />
      </div>

      <button
        onClick={handleEdit}
        disabled={editing || !instruction.trim() || !currentSpec}
        className="text-sm px-5 py-2.5 rounded-xl font-semibold btn-gradient disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <span className="flex items-center gap-2">
          {editing ? (
            <>
              <span className="animate-spin w-4 h-4 rounded-full"
                style={{ border: '2px solid currentColor', borderTopColor: 'transparent', opacity: 0.85 }}
              />
              Applying Changes...
            </>
          ) : (
            <>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
              Apply Changes
            </>
          )}
        </span>
      </button>

      <SpecSaveProgressIndicator phase={savePhase} errorMessage={error} />

      {error && savePhase !== "error" && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error-text)' }}
        >
          {error}
        </div>
      )}

      {message && !error && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: 'var(--success-bg)', border: '1px solid var(--success-border)', color: 'var(--success-text)' }}
        >
          {message}
        </div>
      )}

      {preview && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-1 h-4 rounded-full" style={{ background: 'linear-gradient(to bottom, #FF6B4A, #6D5DFC)' }} />
              <h3 className="text-sm font-semibold" style={{ color: 'var(--text-primary)' }}>Preview (editable)</h3>
              {oldSpecJson && (
                <button
                  onClick={() => setShowDiff(!showDiff)}
                  className="text-xs font-medium transition-colors duration-200 px-2.5 py-1 rounded-lg"
                  style={{
                    color: showDiff ? 'var(--accent-cyan-light)' : 'var(--text-muted)',
                    background: showDiff ? 'var(--glow-cyan)' : 'transparent',
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = 'var(--accent-cyan-light)'; }}
                  onMouseLeave={(e) => { if (!showDiff) e.currentTarget.style.color = 'var(--text-muted)'; }}
                >
                  {showDiff ? "Hide diff" : "Show diff"}
                </button>
              )}
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => { setPreview(null); setMessage(""); setShowDiff(false); }}
                className="text-sm px-4 py-2 rounded-xl font-medium transition-all duration-200"
                style={{
                  background: 'var(--hover-nav-bg)',
                  border: '1px solid var(--border)',
                  color: 'var(--text-muted)',
                }}
                onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--bg-hover)'; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = 'var(--hover-nav-bg)'; }}
              >
                Discard
              </button>
              <button
                onClick={handleOpenModal}
                disabled={saving || editing}
                className="text-sm px-5 py-2 rounded-xl font-semibold btn-gradient disabled:opacity-50"
              >
                <span>Review & Save</span>
              </button>
            </div>
          </div>

          {showDiff && oldSpecJson && (
            <div className="grid grid-cols-2 gap-4">
              <div>
                <div className="text-xs font-medium mb-2 flex items-center gap-2" style={{ color: 'var(--text-muted)' }}>
                  <div className="w-2 h-2 rounded-full" style={{ background: 'var(--danger)' }} />
                  Current Spec
                </div>
                <pre className="text-xs font-mono rounded-xl p-4 overflow-auto max-h-[300px]"
                  style={{
                    background: 'var(--diff-old-bg)',
                    border: '1px solid var(--diff-old-border)',
                    color: 'var(--diff-old-text)',
                  }}
                >
                  {oldSpecJson}
                </pre>
              </div>
              <div>
                <div className="text-xs font-medium mb-2 flex items-center gap-2" style={{ color: 'var(--text-muted)' }}>
                  <div className="w-2 h-2 rounded-full" style={{ background: 'var(--success)' }} />
                  New Spec
                </div>
                <pre className="text-xs font-mono rounded-xl p-4 overflow-auto max-h-[300px]"
                  style={{
                    background: 'var(--diff-new-bg)',
                    border: '1px solid var(--diff-new-border)',
                    color: 'var(--diff-new-text)',
                  }}
                >
                  {preview}
                </pre>
              </div>
            </div>
          )}

          <textarea
            value={preview}
            onChange={(e) => setPreview(e.target.value)}
            spellCheck={false}
            className="w-full h-[420px] font-mono text-sm rounded-xl p-5 resize-y focus:outline-none input-glow"
            style={{
              background: 'var(--code-bg)',
              border: '1px solid var(--border)',
              color: 'var(--success-text)',
            }}
          />
        </div>
      )}
    </div>
    </>
  );
}
