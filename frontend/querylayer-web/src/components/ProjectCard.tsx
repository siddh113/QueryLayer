"use client";

import { useState } from "react";
import Link from "next/link";
import { deleteProject } from "../services/api";
import type { Project } from "../types";

interface ProjectCardProps {
  project: Project;
  onDeleted?: (id: string) => void;
}

export default function ProjectCard({ project, onDeleted }: ProjectCardProps) {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");

  const handleDelete = async () => {
    setDeleting(true);
    setError("");
    try {
      await deleteProject(project.id);
      onDeleted?.(project.id);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { error?: string } } }).response?.data?.error
          : undefined;
      setError(msg || "Failed to delete project");
      setConfirming(false);
    } finally {
      setDeleting(false);
    }
  };

  if (confirming) {
    return (
      <div
        className="block rounded-2xl px-5 py-5 transition-all duration-200 glass-card-hover relative overflow-hidden"
      >
        <div className="relative">
          <p className="text-sm font-medium mb-3" style={{ color: 'var(--text-primary)' }}>
            Delete <strong>{project.name}</strong>?
          </p>
          {error && (
            <p className="text-xs mb-2" style={{ color: 'var(--error-text)' }}>{error}</p>
          )}
          <div className="flex items-center gap-2">
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="text-xs font-medium px-3 py-1.5 rounded-lg transition-colors duration-200 disabled:opacity-50"
              style={{
                background: 'var(--error-bg)',
                border: '1px solid var(--error-border)',
                color: 'var(--error-text)',
              }}
            >
              {deleting ? "Deleting..." : "Yes, delete"}
            </button>
            <button
              onClick={() => { setConfirming(false); setError(""); }}
              className="text-xs font-medium px-3 py-1.5 rounded-lg transition-colors duration-200"
              style={{
                background: 'var(--hover-nav-bg)',
                border: '1px solid var(--border)',
                color: 'var(--text-secondary)',
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative group">
      <Link
        href={`/projects/${project.id}`}
        className="block rounded-2xl px-5 py-5 transition-all duration-200 glass-card-hover relative overflow-hidden"
      >
        {/* Hover gradient glow */}
        <div
          className="absolute -top-8 -right-8 w-24 h-24 rounded-full opacity-0 group-hover:opacity-30 transition-opacity duration-300"
          style={{ background: 'radial-gradient(circle, rgba(99, 102, 241, 0.4), transparent)', filter: 'blur(16px)' }}
        />

        <div className="relative">
          <div className="flex items-start justify-between mb-3">
            <div className="flex items-center gap-3">
              <div
                className="w-9 h-9 rounded-lg flex items-center justify-center text-xs font-bold"
                style={{
                  background: 'var(--project-initial-bg)',
                  color: 'var(--project-initial-text)',
                  border: '1px solid var(--project-initial-border)',
                }}
              >
                {project.name.charAt(0).toUpperCase()}
              </div>
              <h3 className="text-sm font-semibold" style={{ color: 'var(--section-title-color)' }}>{project.name}</h3>
            </div>
            <svg
              width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
              className="opacity-0 group-hover:opacity-100 transition-all duration-200 group-hover:translate-x-0.5 mt-0.5"
              style={{ color: 'var(--chevron-color)' }}
            >
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </div>
          <p className="text-[11px] truncate mb-2 font-mono" style={{ color: 'var(--project-meta-color)' }}>
            {project.id}
          </p>
          <div className="flex items-center gap-2">
            <div className="w-1.5 h-1.5 rounded-full" style={{ background: 'var(--dot-success)' }} />
            <p className="text-xs" style={{ color: 'var(--project-date-color)' }}>
              {new Date(project.createdAt).toLocaleDateString()}
            </p>
          </div>
        </div>
      </Link>

      {/* Delete button - appears on hover */}
      <button
        onClick={(e) => { e.preventDefault(); e.stopPropagation(); setConfirming(true); }}
        className="absolute top-3 right-3 w-7 h-7 rounded-lg flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all duration-200 z-10"
        style={{
          background: 'var(--error-bg)',
          border: '1px solid var(--error-border)',
          color: 'var(--error-text)',
        }}
        title="Delete project"
        aria-label="Delete project"
      >
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="3 6 5 6 21 6" />
          <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
          <line x1="10" y1="11" x2="10" y2="17" />
          <line x1="14" y1="11" x2="14" y2="17" />
        </svg>
      </button>
    </div>
  );
}
