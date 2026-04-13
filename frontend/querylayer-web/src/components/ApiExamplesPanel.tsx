"use client";

import { useState } from "react";
import type { EndpointExample } from "../services/api";

interface ApiExamplesPanelProps {
  examples: EndpointExample[];
}

const METHOD_COLORS: Record<string, string> = {
  GET: "bg-green-100 text-green-700",
  POST: "bg-blue-100 text-blue-700",
  PUT: "bg-yellow-100 text-yellow-700",
  PATCH: "bg-orange-100 text-orange-700",
  DELETE: "bg-red-100 text-red-700",
};

type SnippetTab = "curl" | "fetch";

function ExampleCard({ ex }: { ex: EndpointExample }) {
  const [tab, setTab] = useState<SnippetTab>("curl");
  const [copied, setCopied] = useState(false);

  const code = tab === "curl" ? ex.curl : ex.fetch;

  const copy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4 space-y-3">
      <div className="flex items-center gap-2">
        <span className={`text-xs font-bold px-2 py-1 rounded ${METHOD_COLORS[ex.method] || "bg-gray-100 text-gray-700"}`}>
          {ex.method}
        </span>
        <span className="font-mono text-sm text-gray-700">{ex.path}</span>
        <span className="text-xs text-gray-400">({ex.entity})</span>
        {ex.auth && ex.auth !== "public" && (
          <span className="text-xs bg-gray-100 text-gray-500 px-2 py-0.5 rounded">{ex.auth}</span>
        )}
      </div>

      <div className="flex gap-1 border-b border-gray-100">
        {(["curl", "fetch"] as SnippetTab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-3 py-1 text-xs font-medium transition-colors ${
              tab === t
                ? "border-b-2 border-blue-600 text-blue-600"
                : "text-gray-500 hover:text-gray-700"
            }`}
          >
            {t === "curl" ? "cURL" : "JavaScript fetch"}
          </button>
        ))}
      </div>

      <div className="relative">
        <pre className="text-xs font-mono bg-gray-900 text-green-400 rounded p-3 overflow-x-auto whitespace-pre-wrap">
          {code}
        </pre>
        <button
          onClick={copy}
          className="absolute top-2 right-2 text-xs bg-gray-700 text-gray-200 hover:bg-gray-600 px-2 py-1 rounded transition-colors"
        >
          {copied ? "Copied!" : "Copy"}
        </button>
      </div>
    </div>
  );
}

export default function ApiExamplesPanel({ examples }: ApiExamplesPanelProps) {
  if (examples.length === 0) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 text-center text-gray-500 text-sm">
        No endpoints defined yet.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-500">
        Copy-ready code snippets for every endpoint in your project.
      </p>
      {examples.map((ex, i) => (
        <ExampleCard key={`${ex.method}-${ex.path}-${i}`} ex={ex} />
      ))}
    </div>
  );
}
