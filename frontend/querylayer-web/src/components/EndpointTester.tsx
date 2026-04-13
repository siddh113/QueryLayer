"use client";

import { useState } from "react";
import { callEndpoint } from "../services/api";
import type { EndpointSpec, EntitySpec } from "../types";

interface EndpointTesterProps {
  endpoint: EndpointSpec;
  entity: EntitySpec | undefined;
  projectId: string;
}

export default function EndpointTester({ endpoint, entity, projectId }: EndpointTesterProps) {
  const [body, setBody] = useState(() => {
    if (!entity || endpoint.method === "GET" || endpoint.method === "DELETE") return "";
    const obj: Record<string, string> = {};
    for (const f of entity.fields) {
      if (f.primary) continue;
      obj[f.name] = "";
    }
    return JSON.stringify(obj, null, 2);
  });
  const [pathParams, setPathParams] = useState<Record<string, string>>({});
  const [response, setResponse] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  // Extract path params like {id}
  const paramNames = (endpoint.path.match(/\{(\w+)\}/g) || []).map((p) => p.slice(1, -1));

  const handleSend = async () => {
    setError("");
    setResponse(null);
    setLoading(true);

    let resolvedPath = endpoint.path;
    for (const [key, val] of Object.entries(pathParams)) {
      resolvedPath = resolvedPath.replace(`{${key}}`, encodeURIComponent(val));
    }

    let parsedBody: Record<string, unknown> | undefined;
    if (body && endpoint.method !== "GET" && endpoint.method !== "DELETE") {
      try {
        parsedBody = JSON.parse(body);
      } catch {
        setError("Invalid JSON body");
        setLoading(false);
        return;
      }
    }

    try {
      const data = await callEndpoint(projectId, endpoint.method, resolvedPath, parsedBody);
      setResponse(JSON.stringify(data, null, 2));
    } catch (err: unknown) {
      const errData =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { status?: number; data?: unknown } }).response
          : undefined;
      if (errData) {
        setError(`${errData.status}: ${JSON.stringify(errData.data)}`);
      } else {
        setError("Request failed");
      }
    } finally {
      setLoading(false);
    }
  };

  const methodColors: Record<string, string> = {
    GET: "bg-green-100 text-green-700",
    POST: "bg-blue-100 text-blue-700",
    PUT: "bg-yellow-100 text-yellow-700",
    PATCH: "bg-orange-100 text-orange-700",
    DELETE: "bg-red-100 text-red-700",
  };

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4 space-y-3">
      <div className="flex items-center gap-2">
        <span className={`text-xs font-bold px-2 py-1 rounded ${methodColors[endpoint.method] || "bg-gray-100 text-gray-700"}`}>
          {endpoint.method}
        </span>
        <span className="font-mono text-sm text-gray-700">{endpoint.path}</span>
        <span className="text-xs text-gray-400">({endpoint.entity})</span>
        {endpoint.auth && (
          <span className="text-xs bg-gray-100 text-gray-500 px-2 py-0.5 rounded">{endpoint.auth}</span>
        )}
      </div>

      {paramNames.length > 0 && (
        <div className="space-y-2">
          <div className="text-xs font-medium text-gray-500">Path Parameters</div>
          {paramNames.map((name) => (
            <div key={name} className="flex items-center gap-2">
              <label className="text-xs text-gray-600 w-20">{name}:</label>
              <input
                type="text"
                value={pathParams[name] || ""}
                onChange={(e) => setPathParams({ ...pathParams, [name]: e.target.value })}
                placeholder={name}
                className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
          ))}
        </div>
      )}

      {body !== "" && (
        <div>
          <div className="text-xs font-medium text-gray-500 mb-1">Request Body</div>
          <textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            spellCheck={false}
            className="w-full h-32 font-mono text-xs bg-gray-50 border border-gray-300 rounded p-2 focus:outline-none focus:ring-1 focus:ring-blue-500 resize-y"
          />
        </div>
      )}

      <button
        onClick={handleSend}
        disabled={loading}
        className="bg-blue-600 text-white text-sm px-4 py-1.5 rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
      >
        {loading ? "Sending..." : "Send Request"}
      </button>

      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2 font-mono">
          {error}
        </div>
      )}

      {response && (
        <div>
          <div className="text-xs font-medium text-gray-500 mb-1">Response</div>
          <pre className="text-xs font-mono bg-gray-900 text-green-400 rounded p-3 overflow-auto max-h-60">
            {response}
          </pre>
        </div>
      )}
    </div>
  );
}
