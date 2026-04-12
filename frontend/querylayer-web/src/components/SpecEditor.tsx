"use client";

import { useState } from "react";
import { updateSpec, validateSchema } from "../services/api";
import type { BackendSpec } from "../types";

interface SpecEditorProps {
  projectId: string;
  initialSpec: BackendSpec | null;
  onSaved: (spec: BackendSpec, version: number) => void;
}

export default function SpecEditor({ projectId, initialSpec, onSaved }: SpecEditorProps) {
  const [text, setText] = useState(
    initialSpec ? JSON.stringify(initialSpec, null, 2) : '{\n  "entities": [],\n  "endpoints": [],\n  "permissions": []\n}'
  );
  const [saving, setSaving] = useState(false);
  const [validating, setValidating] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [validationResult, setValidationResult] = useState<Record<string, unknown> | null>(null);

  const handleSave = async () => {
    setMessage(null);
    let parsed: BackendSpec;
    try {
      parsed = JSON.parse(text);
    } catch {
      setMessage({ type: "error", text: "Invalid JSON" });
      return;
    }

    setSaving(true);
    try {
      const result = await updateSpec(projectId, parsed);
      setMessage({ type: "success", text: `Spec saved (v${result.version})` });
      onSaved(parsed, result.version);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
          : undefined;
      setMessage({ type: "error", text: msg?.details || msg?.error || "Failed to save spec" });
    } finally {
      setSaving(false);
    }
  };

  const handleValidate = async () => {
    setValidating(true);
    setValidationResult(null);
    try {
      const result = await validateSchema(projectId);
      setValidationResult(result);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string } } }).response?.data?.error
          : undefined;
      setMessage({ type: "error", text: msg || "Validation failed" });
    } finally {
      setValidating(false);
    }
  };

  const handleFormat = () => {
    try {
      const parsed = JSON.parse(text);
      setText(JSON.stringify(parsed, null, 2));
      setMessage(null);
    } catch {
      setMessage({ type: "error", text: "Cannot format: invalid JSON" });
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <button
          onClick={handleSave}
          disabled={saving}
          className="bg-blue-600 text-white text-sm px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
        >
          {saving ? "Saving..." : "Save Spec"}
        </button>
        <button
          onClick={handleValidate}
          disabled={validating}
          className="border border-gray-300 text-gray-700 text-sm px-4 py-2 rounded hover:bg-gray-50 disabled:opacity-50 transition-colors"
        >
          {validating ? "Validating..." : "Validate Schema"}
        </button>
        <button
          onClick={handleFormat}
          className="border border-gray-300 text-gray-700 text-sm px-4 py-2 rounded hover:bg-gray-50 transition-colors"
        >
          Format
        </button>
      </div>

      {message && (
        <div
          className={`text-sm rounded px-3 py-2 border ${
            message.type === "success"
              ? "text-green-700 bg-green-50 border-green-200"
              : "text-red-600 bg-red-50 border-red-200"
          }`}
        >
          {message.text}
        </div>
      )}

      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        spellCheck={false}
        className="w-full h-[500px] font-mono text-sm bg-gray-900 text-green-400 rounded-lg p-4 border border-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
      />

      {validationResult && (
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <h3 className="font-semibold text-gray-900 text-sm mb-2">Validation Result</h3>
          <pre className="text-xs font-mono text-gray-700 overflow-auto max-h-60">
            {JSON.stringify(validationResult, null, 2)}
          </pre>
        </div>
      )}
    </div>
  );
}
