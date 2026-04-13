"use client";

import { useEffect, useState } from "react";
import { getKeys, generateKey, revokeKey, rotateKey } from "../services/api";
import type { ApiKeyRecord, GeneratedKey } from "../services/api";

interface Props {
  projectId: string;
}

function KeyTypeBadge({ type }: { type: string }) {
  return (
    <span className={`text-xs font-medium px-2 py-0.5 rounded ${
      type === "secret"
        ? "bg-orange-100 text-orange-700"
        : "bg-blue-100 text-blue-700"
    }`}>
      {type === "secret" ? "Secret" : "Public"}
    </span>
  );
}

function RevealableSecret({ rawKey }: { rawKey: string }) {
  const [visible, setVisible] = useState(false);
  const [copied, setCopied] = useState(false);

  const copy = () => {
    navigator.clipboard.writeText(rawKey);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="mt-3 bg-yellow-50 border border-yellow-300 rounded p-3 space-y-2">
      <p className="text-xs font-semibold text-yellow-800">
        Save this key now — it will never be shown again.
      </p>
      <div className="flex items-center gap-2">
        <code className="flex-1 font-mono text-xs bg-white border border-yellow-200 rounded px-2 py-1 text-gray-800 break-all">
          {visible ? rawKey : "•".repeat(Math.min(rawKey.length, 40))}
        </code>
        <button
          onClick={() => setVisible((v) => !v)}
          className="text-xs text-yellow-700 hover:text-yellow-900 px-2 py-1 border border-yellow-300 rounded"
        >
          {visible ? "Hide" : "Reveal"}
        </button>
        <button
          onClick={copy}
          className="text-xs bg-yellow-600 text-white hover:bg-yellow-700 px-2 py-1 rounded"
        >
          {copied ? "Copied!" : "Copy"}
        </button>
      </div>
      <p className="text-xs text-red-600 font-medium">
        Do not expose secret keys in frontend code.
      </p>
    </div>
  );
}

export default function ProjectKeysPanel({ projectId }: Props) {
  const [keys, setKeys] = useState<ApiKeyRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [newKeyType, setNewKeyType] = useState<"public" | "secret">("secret");
  const [newKeyName, setNewKeyName] = useState("");
  const [justGenerated, setJustGenerated] = useState<GeneratedKey | null>(null);
  const [error, setError] = useState("");

  const load = () => {
    setLoading(true);
    getKeys(projectId)
      .then(setKeys)
      .catch(() => setError("Failed to load keys"))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [projectId]);

  const handleGenerate = async () => {
    setGenerating(true);
    setJustGenerated(null);
    setError("");
    try {
      const result = await generateKey(projectId, newKeyType, newKeyName || undefined);
      setJustGenerated(result);
      setNewKeyName("");
      load();
    } catch {
      setError("Failed to generate key");
    } finally {
      setGenerating(false);
    }
  };

  const handleRevoke = async (keyId: string) => {
    if (!confirm("Revoke this key? All requests using it will immediately fail.")) return;
    try {
      await revokeKey(projectId, keyId);
      load();
    } catch {
      setError("Failed to revoke key");
    }
  };

  const handleRotate = async (keyId: string) => {
    if (!confirm("Rotate this key? The old key will be revoked immediately.")) return;
    setJustGenerated(null);
    try {
      const result = await rotateKey(projectId, keyId);
      setJustGenerated(result);
      load();
    } catch {
      setError("Failed to rotate key");
    }
  };

  return (
    <div className="space-y-6">
      {/* Generate new key */}
      <div className="bg-white rounded-lg border border-gray-200 p-5 space-y-4">
        <h3 className="font-semibold text-gray-900 text-sm">Generate New Key</h3>
        <div className="flex gap-3 flex-wrap">
          <select
            value={newKeyType}
            onChange={(e) => setNewKeyType(e.target.value as "public" | "secret")}
            className="border border-gray-300 rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="secret">Secret Key (server-to-server)</option>
            <option value="public">Public Key (client identifier)</option>
          </select>
          <input
            type="text"
            placeholder="Key name (optional)"
            value={newKeyName}
            onChange={(e) => setNewKeyName(e.target.value)}
            className="border border-gray-300 rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <button
            onClick={handleGenerate}
            disabled={generating}
            className="bg-blue-600 text-white text-sm px-4 py-1.5 rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {generating ? "Generating…" : "Generate Key"}
          </button>
        </div>

        {newKeyType === "secret" && (
          <p className="text-xs text-orange-600">
            Secret keys are for server use only. Never include them in frontend code.
          </p>
        )}

        {justGenerated && justGenerated.keyType === "secret" && (
          <RevealableSecret rawKey={justGenerated.rawKey} />
        )}
        {justGenerated && justGenerated.keyType === "public" && (
          <div className="mt-3 bg-blue-50 border border-blue-200 rounded p-3">
            <p className="text-xs text-blue-700 mb-1 font-medium">Public key generated:</p>
            <code className="font-mono text-xs text-gray-800">{justGenerated.rawKey}</code>
            <button
              onClick={() => navigator.clipboard.writeText(justGenerated.rawKey)}
              className="ml-2 text-xs text-blue-600 hover:text-blue-800"
            >
              Copy
            </button>
          </div>
        )}
      </div>

      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}

      {/* Key list */}
      <div className="bg-white rounded-lg border border-gray-200 divide-y divide-gray-100">
        <div className="px-5 py-3">
          <h3 className="font-semibold text-gray-900 text-sm">API Keys</h3>
        </div>

        {loading ? (
          <div className="px-5 py-4 text-sm text-gray-500">Loading…</div>
        ) : keys.length === 0 ? (
          <div className="px-5 py-4 text-sm text-gray-500">No keys yet.</div>
        ) : (
          keys.map((key) => (
            <div key={key.id} className="px-5 py-3 flex items-center gap-4 flex-wrap">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-0.5">
                  <KeyTypeBadge type={key.keyType} />
                  <code className="text-xs font-mono text-gray-700">{key.keyPrefix}***</code>
                  {key.name && <span className="text-xs text-gray-400">{key.name}</span>}
                  {!key.isActive && (
                    <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded">Revoked</span>
                  )}
                </div>
                <div className="text-xs text-gray-400">
                  Created {new Date(key.createdAt).toLocaleDateString()}
                  {key.lastUsedAt && ` · Last used ${new Date(key.lastUsedAt).toLocaleDateString()}`}
                  {key.revokedAt && ` · Revoked ${new Date(key.revokedAt).toLocaleDateString()}`}
                </div>
              </div>

              {key.isActive && (
                <div className="flex gap-2">
                  {key.keyType === "secret" && (
                    <button
                      onClick={() => handleRotate(key.id)}
                      className="text-xs text-blue-600 hover:text-blue-800 px-2 py-1 border border-blue-200 rounded"
                    >
                      Rotate
                    </button>
                  )}
                  <button
                    onClick={() => handleRevoke(key.id)}
                    className="text-xs text-red-600 hover:text-red-800 px-2 py-1 border border-red-200 rounded"
                  >
                    Revoke
                  </button>
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
