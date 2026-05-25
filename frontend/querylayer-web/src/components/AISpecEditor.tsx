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
          style={{ background: 'rgba(245, 158, 11, 0.06)', border: '1px solid rgba(245, 158, 11, 0.12)', color: '#fbbf24' }}
        >
          No spec exists yet. Use the Generator tab to create one first.
        </div>
      )}

      <div>
        <label className="block text-[13px] font-medium mb-2" style={{ color: '#a1a1aa' }}>
          Modification instruction
        </label>
        <textarea
          value={instruction}
          onChange={(e) => setInstruction(e.target.value)}
          placeholder="Add a due_date timestamp field to Task and create a new Category entity with name field..."
          disabled={!currentSpec}
          className="w-full h-28 text-sm rounded-xl p-4 resize-y focus:outline-none input-glow disabled:opacity-40 disabled:cursor-not-allowed"
          style={{
            background: 'rgba(7, 7, 14, 0.6)',
            border: '1px solid rgba(255, 255, 255, 0.08)',
            color: '#f0f0f5',
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
                style={{ border: '2px solid rgba(255,255,255,0.3)', borderTopColor: 'white' }}
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
              {oldSpecJson && (
                <button
                  onClick={() => setShowDiff(!showDiff)}
                  className="text-xs font-medium transition-colors duration-200 px-2.5 py-1 rounded-lg"
                  style={{
                    color: showDiff ? '#a5b4fc' : '#52525b',
                    background: showDiff ? 'rgba(99, 102, 241, 0.08)' : 'transparent',
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = '#a5b4fc'; }}
                  onMouseLeave={(e) => { if (!showDiff) e.currentTarget.style.color = '#52525b'; }}
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
                <div className="text-xs font-medium mb-2 flex items-center gap-2" style={{ color: '#52525b' }}>
                  <div className="w-2 h-2 rounded-full" style={{ background: '#ef4444' }} />
                  Current Spec
                </div>
                <pre className="text-xs font-mono rounded-xl p-4 overflow-auto max-h-[300px]"
                  style={{
                    background: 'rgba(127, 29, 29, 0.08)',
                    border: '1px solid rgba(239, 68, 68, 0.1)',
                    color: '#fca5a5',
                  }}
                >
                  {oldSpecJson}
                </pre>
              </div>
              <div>
                <div className="text-xs font-medium mb-2 flex items-center gap-2" style={{ color: '#52525b' }}>
                  <div className="w-2 h-2 rounded-full" style={{ background: '#22c55e' }} />
                  New Spec
                </div>
                <pre className="text-xs font-mono rounded-xl p-4 overflow-auto max-h-[300px]"
                  style={{
                    background: 'rgba(5, 46, 22, 0.15)',
                    border: '1px solid rgba(34, 197, 94, 0.1)',
                    color: '#86efac',
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
