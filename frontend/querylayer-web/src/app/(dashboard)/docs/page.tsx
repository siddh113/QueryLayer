export default function DocsPage() {
  return (
    <div className="fade-in max-w-3xl space-y-6">
      <div className="mb-2">
        <h1 className="text-3xl font-bold tracking-tight gradient-text-hero mb-2">Documentation</h1>
        <p className="text-sm" style={{ color: '#71717a' }}>Everything you need to know about QueryLayer.</p>
      </div>

      <section className="rounded-2xl glass-card p-7 space-y-3">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>What is QueryLayer?</h2>
        </div>
        <p className="text-sm leading-relaxed" style={{ color: '#a1a1aa' }}>
          QueryLayer is an AI-native backend platform. You describe your data model in plain English,
          and the platform automatically generates your database schema, REST API endpoints, and
          interactive documentation -- with no code required.
        </p>
      </section>

      <section className="rounded-2xl glass-card p-7 space-y-4">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>How it works</h2>
        </div>
        <ol className="space-y-4 text-sm list-none leading-relaxed">
          {[
            { num: "1", title: "Create a project", desc: "Each project gets its own isolated API and database tables." },
            { num: "2", title: "Generate or write a spec", desc: "Use the AI Assistant to describe your backend, or write the JSON spec manually in the Backend Spec tab." },
            { num: "3", title: "Save the spec", desc: "QueryLayer immediately creates the database tables and activates your API endpoints." },
            { num: "4", title: "Use your API", desc: "Call your endpoints using the project ID as the API key, or test them directly in the API Explorer." },
          ].map(({ num, title, desc }) => (
            <li key={num} className="flex gap-4 items-start">
              <span className="flex-shrink-0 w-7 h-7 rounded-lg text-xs font-bold flex items-center justify-center"
                style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#818cf8', border: '1px solid rgba(99, 102, 241, 0.12)' }}
              >
                {num}
              </span>
              <div>
                <strong style={{ color: '#e4e4e7' }}>{title}</strong>
                <span style={{ color: '#71717a' }}> -- {desc}</span>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="rounded-2xl glass-card p-7 space-y-4">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>Making API requests</h2>
        </div>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          All runtime API requests are routed through:
        </p>
        <pre className="text-sm font-mono rounded-xl p-5 overflow-x-auto"
          style={{
            background: 'rgba(7, 7, 14, 0.6)',
            border: '1px solid rgba(255, 255, 255, 0.06)',
            color: '#4ade80',
          }}
        >
{`GET  /api/{projectId}/{entity}
POST /api/{projectId}/{entity}
PUT  /api/{projectId}/{entity}/{id}
DELETE /api/{projectId}/{entity}/{id}`}
        </pre>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          Replace <code className="px-2 py-0.5 rounded-md text-xs font-mono"
            style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >{"{projectId}"}</code> with your project&apos;s ID,
          found in the Overview tab.
        </p>
      </section>

      <section className="rounded-2xl glass-card p-7 space-y-4">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>Authentication</h2>
        </div>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          Endpoints marked as <code className="px-2 py-0.5 rounded-md text-xs font-mono"
            style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >authenticated</code> or{" "}
          <code className="px-2 py-0.5 rounded-md text-xs font-mono"
            style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >admin</code> require a Bearer token in the
          Authorization header:
        </p>
        <pre className="text-sm font-mono rounded-xl p-5"
          style={{
            background: 'rgba(7, 7, 14, 0.6)',
            border: '1px solid rgba(255, 255, 255, 0.06)',
            color: '#4ade80',
          }}
        >
{`Authorization: Bearer YOUR_JWT_TOKEN`}
        </pre>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          Obtain a token by calling <code className="px-2 py-0.5 rounded-md text-xs font-mono"
            style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >POST /auth/login</code>{" "}
          on your project&apos;s auth endpoint.
        </p>
      </section>

      <section className="rounded-2xl glass-card p-7 space-y-4">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>Rate limits</h2>
        </div>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          Runtime API endpoints are limited to <strong style={{ color: '#e4e4e7' }}>100 requests per minute</strong> per IP address.
          Exceeding this returns a <code className="px-2 py-0.5 rounded-md text-xs font-mono"
            style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
          >429 Too Many Requests</code> response.
        </p>
      </section>

      <section className="rounded-2xl glass-card p-7 space-y-4">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-1 h-5 rounded-full" style={{ background: 'linear-gradient(to bottom, #6366f1, #06b6d4)' }} />
          <h2 className="text-[15px] font-semibold" style={{ color: '#f0f0f5' }}>Spec format</h2>
        </div>
        <p className="text-sm" style={{ color: '#a1a1aa' }}>
          The backend spec is a JSON object describing your entities, endpoints, and permissions.
          Field types supported: {["string", "integer", "boolean", "uuid", "timestamp"].map((t, i) => (
            <span key={t}>
              {i > 0 && ", "}
              <code className="px-2 py-0.5 rounded-md text-xs font-mono"
                style={{ background: 'rgba(99, 102, 241, 0.08)', color: '#a5b4fc', border: '1px solid rgba(99, 102, 241, 0.1)' }}
              >{t}</code>
            </span>
          ))}.
        </p>
        <pre className="text-sm font-mono rounded-xl p-5 overflow-x-auto"
          style={{
            background: 'rgba(7, 7, 14, 0.6)',
            border: '1px solid rgba(255, 255, 255, 0.06)',
            color: '#4ade80',
          }}
        >
{`{
  "entities": [{
    "name": "Task",
    "table": "tasks",
    "fields": [
      { "name": "id", "type": "uuid", "primary": true },
      { "name": "title", "type": "string", "required": true },
      { "name": "done", "type": "boolean" }
    ]
  }],
  "endpoints": [
    { "method": "GET", "path": "/tasks", "operation": "list", "entity": "Task" },
    { "method": "POST", "path": "/tasks", "operation": "create", "entity": "Task" }
  ],
  "permissions": []
}`}
        </pre>
      </section>
    </div>
  );
}
