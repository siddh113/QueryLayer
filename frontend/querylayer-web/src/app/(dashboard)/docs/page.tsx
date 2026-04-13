export default function DocsPage() {
  return (
    <div className="max-w-3xl space-y-10">
      <div>
        <h1 className="text-2xl font-bold text-gray-900 mb-2">Documentation</h1>
        <p className="text-sm text-gray-500">Everything you need to know about QueryLayer.</p>
      </div>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-3">
        <h2 className="text-lg font-semibold text-gray-900">What is QueryLayer?</h2>
        <p className="text-sm text-gray-700">
          QueryLayer is an AI-native backend platform. You describe your data model in plain English,
          and the platform automatically generates your database schema, REST API endpoints, and
          interactive documentation — with no code required.
        </p>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">How it works</h2>
        <ol className="space-y-3 text-sm text-gray-700 list-decimal list-inside">
          <li>
            <strong>Create a project</strong> — Each project gets its own isolated API and database tables.
          </li>
          <li>
            <strong>Generate or write a spec</strong> — Use the AI Assistant to describe your backend,
            or write the JSON spec manually in the Backend Spec tab.
          </li>
          <li>
            <strong>Save the spec</strong> — QueryLayer immediately creates the database tables and
            activates your API endpoints.
          </li>
          <li>
            <strong>Use your API</strong> — Call your endpoints using the project ID as the API key,
            or test them directly in the API Explorer.
          </li>
        </ol>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">Making API requests</h2>
        <p className="text-sm text-gray-700">
          All runtime API requests are routed through:
        </p>
        <pre className="bg-gray-900 text-green-400 text-xs font-mono rounded p-3">
{`GET  /api/{projectId}/{entity}
POST /api/{projectId}/{entity}
PUT  /api/{projectId}/{entity}/{id}
DELETE /api/{projectId}/{entity}/{id}`}
        </pre>
        <p className="text-sm text-gray-700">
          Replace <code className="bg-gray-100 px-1 rounded">{"{projectId}"}</code> with your project&apos;s ID,
          found in the Overview tab.
        </p>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">Authentication</h2>
        <p className="text-sm text-gray-700">
          Endpoints marked as <code className="bg-gray-100 px-1 rounded">authenticated</code> or{" "}
          <code className="bg-gray-100 px-1 rounded">admin</code> require a Bearer token in the
          Authorization header:
        </p>
        <pre className="bg-gray-900 text-green-400 text-xs font-mono rounded p-3">
{`Authorization: Bearer YOUR_JWT_TOKEN`}
        </pre>
        <p className="text-sm text-gray-700">
          Obtain a token by calling <code className="bg-gray-100 px-1 rounded">POST /auth/login</code>{" "}
          on your project&apos;s auth endpoint.
        </p>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">Rate limits</h2>
        <p className="text-sm text-gray-700">
          Runtime API endpoints are limited to <strong>100 requests per minute</strong> per IP address.
          Exceeding this returns a <code className="bg-gray-100 px-1 rounded">429 Too Many Requests</code> response.
        </p>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 p-6 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">Spec format</h2>
        <p className="text-sm text-gray-700">
          The backend spec is a JSON object describing your entities, endpoints, and permissions.
          Field types supported: <code className="bg-gray-100 px-1 rounded">string</code>,{" "}
          <code className="bg-gray-100 px-1 rounded">integer</code>,{" "}
          <code className="bg-gray-100 px-1 rounded">boolean</code>,{" "}
          <code className="bg-gray-100 px-1 rounded">uuid</code>,{" "}
          <code className="bg-gray-100 px-1 rounded">timestamp</code>.
        </p>
        <pre className="bg-gray-900 text-green-400 text-xs font-mono rounded p-3 overflow-x-auto">
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
