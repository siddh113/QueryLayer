"use client";

import EndpointTester from "./EndpointTester";
import type { BackendSpec } from "../types";

interface ApiExplorerProps {
  projectId: string;
  spec: BackendSpec | null;
}

export default function ApiExplorer({ projectId, spec }: ApiExplorerProps) {
  if (!spec || spec.endpoints.length === 0) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 text-center text-gray-500 text-sm">
        No endpoints defined. Add endpoints in the Backend Spec tab first.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="text-sm text-gray-500">
        {spec.endpoints.length} endpoint{spec.endpoints.length !== 1 ? "s" : ""} defined
      </div>
      {spec.endpoints.map((ep, i) => {
        const entity = spec.entities.find(
          (e) => e.name.toLowerCase() === ep.entity.toLowerCase()
        );
        return (
          <EndpointTester
            key={`${ep.method}-${ep.path}-${i}`}
            endpoint={ep}
            entity={entity}
            projectId={projectId}
          />
        );
      })}
    </div>
  );
}
