"use client";

import { useEffect, useState } from "react";
import { previewSpec } from "../services/api";
import type { BackendSpec, SpecPreviewResponse, EntitySpec } from "../types";

interface SchemaChangesModalProps {
  projectId: string;
  specJson: string;
  onApply: (editedSpec: BackendSpec) => Promise<void>;
  onBack: () => void;
  isApplying: boolean;
}

const FIELD_TYPES = ["string", "integer", "boolean", "uuid", "timestamp", "text", "decimal"];

export default function SchemaChangesModal({
  projectId,
  specJson,
  onApply,
  onBack,
  isApplying,
}: SchemaChangesModalProps) {
  const [preview, setPreview] = useState<SpecPreviewResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [editableEntities, setEditableEntities] = useState<EntitySpec[]>([]);
  const [showSql, setShowSql] = useState(false);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setLoadError("");
      try {
        const parsed: BackendSpec = JSON.parse(specJson);
        const result = await previewSpec(projectId, parsed);
        setPreview(result);
        setEditableEntities(JSON.parse(JSON.stringify(result.entities)));
      } catch (err: unknown) {
        const msg =
          err && typeof err === "object" && "response" in err
            ? (err as { response?: { data?: { error?: string; details?: string } } }).response?.data
            : undefined;
        setLoadError(msg?.details || msg?.error || "Failed to load preview.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [projectId, specJson]);

  const updateField = (entityIdx: number, fieldIdx: number, key: "name" | "type" | "required", value: string | boolean) => {
    setEditableEntities((prev) => {
      const next = JSON.parse(JSON.stringify(prev)) as EntitySpec[];
      if (key === "required") {
        next[entityIdx].fields[fieldIdx].required = value as boolean;
      } else if (key === "name") {
        next[entityIdx].fields[fieldIdx].name = value as string;
      } else if (key === "type") {
        next[entityIdx].fields[fieldIdx].type = value as string;
      }
      return next;
    });
  };

  const handleApply = async () => {
    if (!preview) return;
    const originalParsed: BackendSpec = JSON.parse(specJson);
    const edited: BackendSpec = {
      ...originalParsed,
      entities: editableEntities,
    };
    await onApply(edited);
  };

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 50,
        background: "rgba(0, 0, 0, 0.75)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "24px",
      }}
    >
      <div
        style={{
          background: "var(--bg-raised, #0f0f1a)",
          border: "1px solid var(--border, rgba(255,255,255,0.09))",
          borderRadius: "20px",
          width: "100%",
          maxWidth: "780px",
          maxHeight: "85vh",
          overflow: "hidden",
          display: "flex",
          flexDirection: "column",
        }}
      >
        {/* Modal header */}
        <div
          style={{
            padding: "20px 24px",
            borderBottom: "1px solid rgba(255,255,255,0.07)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexShrink: 0,
          }}
        >
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-xl flex items-center justify-center"
              style={{ background: "rgba(99, 102, 241, 0.12)", border: "1px solid rgba(99, 102, 241, 0.2)" }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#818cf8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </div>
            <div>
              <h2 className="text-sm font-semibold" style={{ color: "#f0f0f5" }}>Review Schema Changes</h2>
              <p className="text-[11px]" style={{ color: "#52525b" }}>Review and edit fields before applying to the database</p>
            </div>
          </div>
          <button
            onClick={onBack}
            className="text-xs px-3 py-1.5 rounded-lg transition-all duration-200"
            style={{ color: "#52525b", background: "rgba(255,255,255,0.03)" }}
            onMouseEnter={(e) => { e.currentTarget.style.color = "#a1a1aa"; }}
            onMouseLeave={(e) => { e.currentTarget.style.color = "#52525b"; }}
          >
            ✕
          </button>
        </div>

        {/* Modal body */}
        <div style={{ overflowY: "auto", padding: "20px 24px", flex: 1 }}>
          {loading && (
            <div className="flex items-center gap-3 py-8 justify-center text-sm" style={{ color: "#52525b" }}>
              <span className="animate-spin w-4 h-4 rounded-full"
                style={{ border: "2px solid rgba(255,255,255,0.08)", borderTopColor: "#818cf8" }}
              />
              Analyzing schema changes...
            </div>
          )}

          {loadError && (
            <div className="text-sm rounded-xl px-5 py-3.5"
              style={{ background: "rgba(239, 68, 68, 0.08)", border: "1px solid rgba(239, 68, 68, 0.15)", color: "#f87171" }}
            >
              {loadError}
            </div>
          )}

          {!loading && !loadError && preview && (
            <div className="space-y-5">
              {/* Changes summary */}
              {!preview.hasChanges ? (
                <div className="rounded-xl px-5 py-3.5 flex items-center gap-3"
                  style={{ background: "rgba(34, 197, 94, 0.06)", border: "1px solid rgba(34, 197, 94, 0.12)" }}
                >
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#4ade80" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  <span className="text-sm" style={{ color: "#4ade80" }}>
                    Schema already in sync — no migrations needed. Saving will update the spec version.
                  </span>
                </div>
              ) : (
                <div className="space-y-3">
                  {preview.syncResult.missingTables.length > 0 && (
                    <ChangesSection
                      title="New Tables"
                      color="#818cf8"
                      bgColor="rgba(99, 102, 241, 0.06)"
                      borderColor="rgba(99, 102, 241, 0.15)"
                      items={preview.syncResult.missingTables.map((t) => ({
                        primary: t,
                        secondary: "will be created",
                      }))}
                    />
                  )}
                  {preview.syncResult.newColumns.length > 0 && (
                    <ChangesSection
                      title="New Columns"
                      color="#60a5fa"
                      bgColor="rgba(96, 165, 250, 0.06)"
                      borderColor="rgba(96, 165, 250, 0.15)"
                      items={preview.syncResult.newColumns.map((c) => ({
                        primary: `${c.table}.${c.column}`,
                        secondary: c.detail,
                      }))}
                    />
                  )}
                  {preview.syncResult.typeMismatches.length > 0 && (
                    <ChangesSection
                      title="Type Changes"
                      color="#f59e0b"
                      bgColor="rgba(245, 158, 11, 0.06)"
                      borderColor="rgba(245, 158, 11, 0.15)"
                      items={preview.syncResult.typeMismatches.map((c) => ({
                        primary: `${c.table}.${c.column}`,
                        secondary: c.detail,
                      }))}
                    />
                  )}
                  {preview.syncResult.extraColumns.length > 0 && (
                    <ChangesSection
                      title="Extra DB Columns (not in spec)"
                      color="#52525b"
                      bgColor="rgba(255, 255, 255, 0.02)"
                      borderColor="rgba(255, 255, 255, 0.06)"
                      items={preview.syncResult.extraColumns.map((c) => ({
                        primary: `${c.table}.${c.column}`,
                        secondary: "exists in DB, not in spec — will be left as-is",
                      }))}
                    />
                  )}
                </div>
              )}

              {/* Editable field table */}
              {editableEntities.length > 0 && (
                <div>
                  <div className="flex items-center gap-2 mb-3">
                    <div className="w-1 h-4 rounded-full" style={{ background: "linear-gradient(to bottom, #6366f1, #06b6d4)" }} />
                    <h3 className="text-[13px] font-semibold" style={{ color: "#f0f0f5" }}>Edit Fields</h3>
                    <span className="text-[11px]" style={{ color: "#52525b" }}>Changes apply before saving to DB</span>
                  </div>
                  <div className="space-y-3">
                    {editableEntities.map((entity, ei) => (
                      <div key={ei} className="rounded-xl overflow-hidden"
                        style={{ border: "1px solid rgba(255,255,255,0.07)" }}
                      >
                        <div className="px-4 py-2.5 text-[12px] font-semibold"
                          style={{ background: "rgba(255,255,255,0.03)", color: "#a5b4fc", borderBottom: "1px solid rgba(255,255,255,0.05)" }}
                        >
                          {entity.name} <span style={{ color: "#3f3f5a", fontWeight: 400 }}>({entity.table})</span>
                        </div>
                        <div className="grid px-4 py-2 text-[11px] font-medium uppercase tracking-wider"
                          style={{ gridTemplateColumns: "2fr 1.5fr 80px", color: "#3f3f5a", borderBottom: "1px solid rgba(255,255,255,0.04)" }}
                        >
                          <span>Field Name</span><span>Type</span><span>Required</span>
                        </div>
                        {entity.fields.map((field, fi) => (
                          <div key={fi} className="grid px-4 py-2 items-center gap-2"
                            style={{
                              gridTemplateColumns: "2fr 1.5fr 80px",
                              borderBottom: fi < entity.fields.length - 1 ? "1px solid rgba(255,255,255,0.03)" : "none",
                            }}
                          >
                            <input
                              type="text"
                              value={field.name}
                              onChange={(e) => updateField(ei, fi, "name", e.target.value)}
                              disabled={field.primary}
                              className="text-xs font-mono rounded-lg px-2.5 py-1.5 focus:outline-none"
                              style={{
                                background: field.primary ? "transparent" : "rgba(255,255,255,0.04)",
                                border: field.primary ? "none" : "1px solid rgba(255,255,255,0.08)",
                                color: field.primary ? "#52525b" : "#e4e4f0",
                                cursor: field.primary ? "default" : "text",
                              }}
                            />
                            <select
                              value={field.type}
                              onChange={(e) => updateField(ei, fi, "type", e.target.value)}
                              disabled={field.primary}
                              className="text-xs rounded-lg px-2 py-1.5 focus:outline-none"
                              style={{
                                background: field.primary ? "transparent" : "rgba(255,255,255,0.04)",
                                border: field.primary ? "none" : "1px solid rgba(255,255,255,0.08)",
                                color: field.primary ? "#52525b" : "#a5b4fc",
                                cursor: field.primary ? "default" : "pointer",
                              }}
                            >
                              {FIELD_TYPES.map((t) => (
                                <option key={t} value={t} style={{ background: "#0f0f1a" }}>{t}</option>
                              ))}
                            </select>
                            <div className="flex justify-start pl-2">
                              <input
                                type="checkbox"
                                checked={!!field.required}
                                onChange={(e) => updateField(ei, fi, "required", e.target.checked)}
                                disabled={field.primary}
                                style={{ accentColor: "#818cf8", cursor: field.primary ? "default" : "pointer" }}
                              />
                            </div>
                          </div>
                        ))}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* SQL preview */}
              {preview.migrationSql.length > 0 && (
                <div>
                  <button
                    onClick={() => setShowSql(!showSql)}
                    className="flex items-center gap-2 text-xs font-medium transition-colors duration-200"
                    style={{ color: showSql ? "#a5b4fc" : "#52525b" }}
                    onMouseEnter={(e) => { e.currentTarget.style.color = "#a5b4fc"; }}
                    onMouseLeave={(e) => { if (!showSql) e.currentTarget.style.color = "#52525b"; }}
                  >
                    <svg
                      width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                      style={{ transform: showSql ? "rotate(90deg)" : "rotate(0deg)", transition: "transform 0.2s" }}
                    >
                      <polyline points="9 18 15 12 9 6" />
                    </svg>
                    {showSql ? "Hide" : "Show"} migration SQL ({preview.migrationSql.length} statement{preview.migrationSql.length !== 1 ? "s" : ""})
                  </button>
                  {showSql && (
                    <pre
                      className="mt-2 text-[11px] font-mono rounded-xl p-4 overflow-auto"
                      style={{
                        maxHeight: "200px",
                        background: "rgba(7, 7, 14, 0.8)",
                        border: "1px solid rgba(255,255,255,0.06)",
                        color: "#4ade80",
                        lineHeight: "1.6",
                      }}
                    >
                      {preview.migrationSql.join("\n\n")}
                    </pre>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Modal footer */}
        <div
          style={{
            padding: "16px 24px",
            borderTop: "1px solid rgba(255,255,255,0.07)",
            display: "flex",
            justifyContent: "flex-end",
            gap: "10px",
            flexShrink: 0,
          }}
        >
          <button
            onClick={onBack}
            disabled={isApplying}
            className="text-sm px-5 py-2.5 rounded-xl font-medium transition-all duration-200"
            style={{
              background: "rgba(255, 255, 255, 0.04)",
              border: "1px solid rgba(255, 255, 255, 0.08)",
              color: "#a1a1aa",
            }}
            onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.06)"; }}
            onMouseLeave={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.04)"; }}
          >
            ← Edit Spec
          </button>
          <button
            onClick={handleApply}
            disabled={isApplying || loading || !!loadError}
            className="text-sm px-5 py-2.5 rounded-xl font-semibold btn-gradient disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <span className="flex items-center gap-2">
              {isApplying ? (
                <>
                  <span className="animate-spin w-4 h-4 rounded-full"
                    style={{ border: "2px solid rgba(255,255,255,0.3)", borderTopColor: "white" }}
                  />
                  Applying...
                </>
              ) : (
                <>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  Apply Changes
                </>
              )}
            </span>
          </button>
        </div>
      </div>
    </div>
  );
}

function ChangesSection({
  title,
  color,
  bgColor,
  borderColor,
  items,
}: {
  title: string;
  color: string;
  bgColor: string;
  borderColor: string;
  items: { primary: string; secondary: string }[];
}) {
  return (
    <div className="rounded-xl px-4 py-3.5" style={{ background: bgColor, border: `1px solid ${borderColor}` }}>
      <div className="text-[11px] font-semibold uppercase tracking-wider mb-2" style={{ color }}>
        {title} ({items.length})
      </div>
      <div className="space-y-1.5">
        {items.map((item, i) => (
          <div key={i} className="flex items-center justify-between text-xs">
            <span className="font-mono" style={{ color: "#e4e4f0" }}>{item.primary}</span>
            <span style={{ color: "#52525b" }}>{item.secondary}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
