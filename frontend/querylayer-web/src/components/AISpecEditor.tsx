"use client";

import { useState } from "react";
import { editSpec, updateSpec } from "../services/api";
import type { BackendSpec } from "../types";

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
    setEditing(true);
    try {
      const result = await editSpec(projectId, instruction);
      setPreview(JSON.stringify(result.spec, null, 2));
      setMessage("Spec updated. Review changes and save below.");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to edit spec.");
    } finally {
      setEditing(false);
    }
  };

  const handleSave = async () => {
    if (!preview) return;
    setError("");
    let parsed: BackendSpec;
    try {
      parsed = JSON.parse(preview);
    } catch {
      setError("Invalid JSON in preview. Fix before saving.");
      return;
    }

    setSaving(true);
    try {
      const result = await updateSpec(projectId, parsed);
      setMessage(`Spec saved (v${result.version}) and schema synchronized.`);
      setPreview(null);
      setInstruction("");
      setShowDiff(false);
      onSpecSaved(parsed, result.version);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to save spec.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      {!currentSpec && (
        <div className="text-sm text-amber-600 bg-amber-50 border border-amber-200 rounded px-3 py-2">
          No spec exists yet. Use the Generator tab to create one first.
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Modification instruction
        </label>
        <textarea
          value={instruction}
          onChange={(e) => setInstruction(e.target.value)}
          placeholder="Add a due_date timestamp field to Task and create a new Category entity with name field..."
          disabled={!currentSpec}
          className="w-full h-24 text-sm rounded-lg border border-gray-300 p-3 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y disabled:bg-gray-100 disabled:text-gray-400"
        />
      </div>

      <button
        onClick={handleEdit}
        disabled={editing || !instruction.trim() || !currentSpec}
        className="bg-purple-600 text-white text-sm px-4 py-2 rounded hover:bg-purple-700 disabled:opacity-50 transition-colors"
      >
        {editing ? (
          <span className="flex items-center gap-2">
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            Applying Changes...
          </span>
        ) : (
          "Apply Changes"
        )}
      </button>

      {error && (
        <div className="text-sm rounded px-3 py-2 border text-red-600 bg-red-50 border-red-200">
          {error}
        </div>
      )}

      {message && !error && (
        <div className="text-sm rounded px-3 py-2 border text-green-700 bg-green-50 border-green-200">
          {message}
        </div>
      )}

      {preview && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <h3 className="text-sm font-semibold text-gray-900">Preview (editable)</h3>
              {oldSpecJson && (
                <button
                  onClick={() => setShowDiff(!showDiff)}
                  className="text-xs text-blue-600 hover:text-blue-800 underline"
                >
                  {showDiff ? "Hide diff" : "Show diff"}
                </button>
              )}
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => { setPreview(null); setMessage(""); setShowDiff(false); }}
                className="border border-gray-300 text-gray-700 text-sm px-3 py-1.5 rounded hover:bg-gray-50 transition-colors"
              >
                Discard
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="bg-blue-600 text-white text-sm px-4 py-1.5 rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
              >
                {saving ? "Saving..." : "Confirm & Save"}
              </button>
            </div>
          </div>

          {showDiff && oldSpecJson && (
            <div className="grid grid-cols-2 gap-2">
              <div>
                <div className="text-xs font-medium text-gray-500 mb-1">Current Spec</div>
                <pre className="text-xs font-mono bg-red-950 text-red-300 rounded-lg p-3 border border-red-800 overflow-auto max-h-[300px]">
                  {oldSpecJson}
                </pre>
              </div>
              <div>
                <div className="text-xs font-medium text-gray-500 mb-1">New Spec</div>
                <pre className="text-xs font-mono bg-green-950 text-green-300 rounded-lg p-3 border border-green-800 overflow-auto max-h-[300px]">
                  {preview}
                </pre>
              </div>
            </div>
          )}

          <textarea
            value={preview}
            onChange={(e) => setPreview(e.target.value)}
            spellCheck={false}
            className="w-full h-[400px] font-mono text-sm bg-gray-900 text-green-400 rounded-lg p-4 border border-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
          />
        </div>
      )}
    </div>
  );
}
