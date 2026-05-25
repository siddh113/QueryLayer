"use client";

import { useEffect, useState } from "react";
import { getLiveSchema } from "../services/api";
import type { LiveSchemaResponse, LiveTableInfo } from "../types";

interface DatabaseSchemaViewerProps {
  projectId: string;
}

const TYPE_COLORS: Record<string, string> = {
  uuid: "#a78bfa",
  varchar: "#60a5fa",
  "character varying": "#60a5fa",
  text: "#60a5fa",
  integer: "#34d399",
  int: "#34d399",
  int4: "#34d399",
  bigint: "#34d399",
  int8: "#34d399",
  boolean: "#f59e0b",
  bool: "#f59e0b",
  timestamptz: "#f472b6",
  "timestamp with time zone": "#f472b6",
  timestamp: "#f472b6",
  numeric: "#34d399",
  decimal: "#34d399",
  json: "#fb923c",
  jsonb: "#fb923c",
};

function typeColor(dataType: string): string {
  return TYPE_COLORS[dataType.toLowerCase()] ?? "#a1a1aa";
}

function TableCard({ table }: { table: LiveTableInfo }) {
  return (
    <div
      className="rounded-2xl glass-card p-5"
      style={{
        border: table.exists
          ? "1px solid rgba(255, 255, 255, 0.07)"
          : "1px solid rgba(245, 158, 11, 0.2)",
        background: table.exists
          ? undefined
          : "rgba(245, 158, 11, 0.03)",
      }}
    >
      {/* Table header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(99, 102, 241, 0.12)", border: "1px solid rgba(99, 102, 241, 0.2)" }}
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#818cf8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <ellipse cx="12" cy="5" rx="9" ry="3" />
              <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" />
              <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
            </svg>
          </div>
          <div>
            <div className="text-sm font-semibold" style={{ color: "#f0f0f5" }}>{table.name}</div>
            {table.entityName && (
              <div className="text-[11px]" style={{ color: "#52525b" }}>entity: {table.entityName}</div>
            )}
          </div>
        </div>
        {!table.exists ? (
          <span className="text-[11px] font-medium px-2.5 py-1 rounded-full"
            style={{ background: "rgba(245, 158, 11, 0.1)", color: "#fbbf24", border: "1px solid rgba(245, 158, 11, 0.2)" }}
          >
            not created yet
          </span>
        ) : (
          <span className="text-[11px] font-medium px-2.5 py-1 rounded-full"
            style={{ background: "rgba(34, 197, 94, 0.08)", color: "#4ade80", border: "1px solid rgba(34, 197, 94, 0.15)" }}
          >
            {table.columns.length} columns
          </span>
        )}
      </div>

      {/* Columns */}
      {table.exists && table.columns.length > 0 ? (
        <div className="rounded-xl overflow-hidden"
          style={{ border: "1px solid rgba(255, 255, 255, 0.05)" }}
        >
          <div className="grid grid-cols-3 px-4 py-2 text-[11px] font-medium uppercase tracking-wider"
            style={{ background: "rgba(255, 255, 255, 0.03)", color: "#52525b", borderBottom: "1px solid rgba(255, 255, 255, 0.05)" }}
          >
            <span>Column</span>
            <span>Type</span>
            <span>Nullable</span>
          </div>
          {table.columns.map((col, i) => (
            <div
              key={col.columnName}
              className="grid grid-cols-3 px-4 py-2.5 text-xs"
              style={{
                borderBottom: i < table.columns.length - 1 ? "1px solid rgba(255, 255, 255, 0.04)" : "none",
                background: i % 2 === 0 ? "transparent" : "rgba(255, 255, 255, 0.01)",
              }}
            >
              <span className="font-mono font-medium" style={{ color: "#e4e4f0" }}>{col.columnName}</span>
              <span className="font-mono text-[11px]" style={{ color: typeColor(col.dataType) }}>
                {col.dataType}{col.maxLength ? `(${col.maxLength})` : ""}
              </span>
              <span style={{ color: col.isNullable ? "#4ade80" : "#52525b" }}>
                {col.isNullable ? "yes" : "no"}
              </span>
            </div>
          ))}
        </div>
      ) : table.exists ? (
        <div className="text-xs text-center py-3" style={{ color: "#52525b" }}>No columns found</div>
      ) : (
        <div className="text-xs px-4 py-3 rounded-xl"
          style={{ background: "rgba(245, 158, 11, 0.05)", color: "#a16207" }}
        >
          Save your spec to create this table in the database.
        </div>
      )}
    </div>
  );
}

export default function DatabaseSchemaViewer({ projectId }: DatabaseSchemaViewerProps) {
  const [data, setData] = useState<LiveSchemaResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const result = await getLiveSchema(projectId);
      setData(result);
    } catch {
      setError("Failed to load schema. Make sure a spec is saved and the backend is reachable.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [projectId]);

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-[15px] font-semibold" style={{ color: "var(--text-primary)" }}>Live Database Schema</h2>
          <p className="text-xs mt-0.5" style={{ color: "var(--text-muted)" }}>
            Actual table structure from your database, queried live.
          </p>
        </div>
        <button
          onClick={load}
          disabled={loading}
          className="flex items-center gap-2 text-xs font-medium px-3.5 py-2 rounded-xl transition-all duration-200"
          style={{
            background: "rgba(255, 255, 255, 0.04)",
            border: "1px solid rgba(255, 255, 255, 0.08)",
            color: "#a1a1aa",
          }}
          onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.06)"; }}
          onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.04)"; }}
        >
          {loading ? (
            <span className="animate-spin w-3 h-3 rounded-full"
              style={{ border: "1.5px solid rgba(255,255,255,0.2)", borderTopColor: "white" }}
            />
          ) : (
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          )}
          Refresh
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="text-sm rounded-xl px-5 py-3.5"
          style={{ background: "rgba(239, 68, 68, 0.08)", border: "1px solid rgba(239, 68, 68, 0.15)", color: "#f87171" }}
        >
          {error}
        </div>
      )}

      {/* Loading */}
      {loading && !data && (
        <div className="flex items-center gap-3 py-8 justify-center text-sm" style={{ color: "var(--text-muted)" }}>
          <span className="animate-spin w-4 h-4 rounded-full"
            style={{ border: "2px solid var(--border)", borderTopColor: "var(--accent)" }}
          />
          Loading schema...
        </div>
      )}

      {/* Empty state */}
      {!loading && !error && data && data.tables.length === 0 && (
        <div className="rounded-2xl glass-card p-8 text-center">
          <div className="w-12 h-12 rounded-2xl flex items-center justify-center mx-auto mb-4"
            style={{ background: "rgba(99, 102, 241, 0.08)", border: "1px solid rgba(99, 102, 241, 0.15)" }}
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#818cf8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <ellipse cx="12" cy="5" rx="9" ry="3" />
              <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" />
              <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
            </svg>
          </div>
          <p className="text-sm font-medium mb-1" style={{ color: "var(--text-primary)" }}>No schema yet</p>
          <p className="text-xs" style={{ color: "var(--text-muted)" }}>
            Generate or save a spec first. Tables will appear here once created.
          </p>
        </div>
      )}

      {/* Tables grid */}
      {!loading && !error && data && data.tables.length > 0 && (
        <div className="grid gap-4" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(420px, 1fr))" }}>
          {data.tables.map((table) => (
            <TableCard key={table.name} table={table} />
          ))}
        </div>
      )}
    </div>
  );
}
