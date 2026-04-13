"use client";

import { useState } from "react";
import { generateSpec, updateSpec } from "../services/api";
import type { BackendSpec } from "../types";

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

  const handleGenerate = async () => {
    if (!prompt.trim()) return;
    setError("");
    setMessage("");
    setPreview(null);
    setGenerating(true);
    try {
      const result = await generateSpec(projectId, prompt);
      setPreview(JSON.stringify(result.spec, null, 2));
      setPreviewSpec(result.spec);
      setMessage("Spec generated. Review and save below.");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.error || "Failed to generate spec.");
    } finally {
      setGenerating(false);
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
      setPreviewSpec(null);
      setPrompt("");
      onSpecSaved(parsed, result.version);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string; detail?: string } } }).response?.data
          : undefined;
      setError(msg?.details || msg?.detail || msg?.error || "Failed to save spec.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Describe your backend
        </label>
        <textarea
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          placeholder="I want a task management app where users can create tasks, assign them to team members, and track progress with statuses like todo, in-progress, and done..."
          className="w-full h-32 text-sm rounded-lg border border-gray-300 p-3 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
        />
      </div>

      <button
        onClick={handleGenerate}
        disabled={generating || !prompt.trim()}
        className="bg-purple-600 text-white text-sm px-4 py-2 rounded hover:bg-purple-700 disabled:opacity-50 transition-colors"
      >
        {generating ? (
          <span className="flex items-center gap-2">
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            Generating...
          </span>
        ) : (
          "Generate Spec"
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
            <h3 className="text-sm font-semibold text-gray-900">Preview (editable)</h3>
            <div className="flex gap-2">
              <button
                onClick={() => { setPreview(null); setPreviewSpec(null); setMessage(""); }}
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
