"use client";

import { useState } from "react";

interface Props {
  projectId: string;
  baseUrl?: string;
}

type Tab = "frontend" | "server" | "login";

function CodeBlock({ code }: { code: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <div className="relative">
      <pre className="text-xs font-mono bg-gray-900 text-green-400 rounded p-3 overflow-x-auto whitespace-pre-wrap">
        {code}
      </pre>
      <button
        onClick={() => {
          navigator.clipboard.writeText(code);
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        }}
        className="absolute top-2 right-2 text-xs bg-gray-700 text-gray-200 hover:bg-gray-600 px-2 py-1 rounded"
      >
        {copied ? "Copied!" : "Copy"}
      </button>
    </div>
  );
}

export default function FrontendIntegrationGuide({ projectId, baseUrl }: Props) {
  const [tab, setTab] = useState<Tab>("frontend");
  const apiBase = baseUrl ? `${baseUrl}/api/${projectId}` : `http://localhost:5000/api/${projectId}`;
  const authBase = baseUrl ? `${baseUrl}/auth` : `http://localhost:5000/auth`;

  const tabs: { key: Tab; label: string }[] = [
    { key: "frontend", label: "Frontend (JWT)" },
    { key: "login", label: "User Login" },
    { key: "server", label: "Server (API Key)" },
  ];

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 space-y-5">
      <div>
        <h3 className="font-semibold text-gray-900 mb-1">Integration Guide</h3>
        <p className="text-xs text-gray-500">
          How to connect your application to this project&apos;s API.
        </p>
      </div>

      {/* Base URL */}
      <div className="space-y-1">
        <p className="text-xs font-medium text-gray-700">Base API URL</p>
        <div className="flex items-center gap-2">
          <code className="flex-1 font-mono text-xs bg-gray-100 border border-gray-200 rounded px-3 py-1.5 text-gray-800">
            {apiBase}
          </code>
          <button
            onClick={() => navigator.clipboard.writeText(apiBase)}
            className="text-xs text-blue-600 hover:text-blue-800"
          >
            Copy
          </button>
        </div>
      </div>

      {/* Auth type tabs */}
      <div>
        <div className="flex gap-1 border-b border-gray-200 mb-4">
          {tabs.map((t) => (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`px-3 py-2 text-xs font-medium transition-colors ${
                tab === t.key
                  ? "border-b-2 border-blue-600 text-blue-600"
                  : "text-gray-500 hover:text-gray-700"
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>

        {tab === "frontend" && (
          <div className="space-y-3">
            <div className="bg-blue-50 border border-blue-200 rounded p-3 text-xs text-blue-800 space-y-1">
              <p className="font-semibold">Use JWT tokens for browser / mobile apps.</p>
              <p>After login, store the token and send it with every request. Never use secret API keys in frontend code.</p>
            </div>
            <CodeBlock code={`// After login, store the token
const token = localStorage.getItem("token");

// Make authenticated requests
const res = await fetch("${apiBase}/your-endpoint", {
  method: "GET",
  headers: {
    "Authorization": \`Bearer \${token}\`
  }
});

const data = await res.json();`} />
          </div>
        )}

        {tab === "login" && (
          <div className="space-y-3">
            <div className="bg-green-50 border border-green-200 rounded p-3 text-xs text-green-800">
              <p className="font-semibold">Sign up or log in users to receive a JWT token.</p>
            </div>
            <p className="text-xs font-medium text-gray-600">Sign up:</p>
            <CodeBlock code={`const res = await fetch("${authBase}/signup", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "user@example.com",
    password: "password123"
  })
});

const { token } = await res.json();
localStorage.setItem("token", token);`} />
            <p className="text-xs font-medium text-gray-600">Log in:</p>
            <CodeBlock code={`const res = await fetch("${authBase}/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "user@example.com",
    password: "password123"
  })
});

const { token } = await res.json();
localStorage.setItem("token", token);`} />
          </div>
        )}

        {tab === "server" && (
          <div className="space-y-3">
            <div className="bg-orange-50 border border-orange-200 rounded p-3 text-xs text-orange-800 space-y-1">
              <p className="font-semibold">Use secret API keys for server-to-server or backend calls only.</p>
              <p className="text-red-600 font-medium">Never expose secret keys in browser or mobile code.</p>
            </div>
            <p className="text-xs text-gray-600">
              Generate a secret key in the <strong>Keys</strong> tab, then use it in the <code className="bg-gray-100 px-1 rounded">x-api-key</code> header:
            </p>
            <CodeBlock code={`// Node.js / server-side only
const res = await fetch("${apiBase}/your-endpoint", {
  method: "GET",
  headers: {
    "x-api-key": "ql_sec_YOUR_SECRET_KEY"
  }
});

const data = await res.json();`} />
            <CodeBlock code={`# cURL example
curl "${apiBase}/your-endpoint" \\
  -H "x-api-key: ql_sec_YOUR_SECRET_KEY"`} />
          </div>
        )}
      </div>
    </div>
  );
}
