import Link from "next/link";
import type { Project } from "../types";

export default function ProjectCard({ project }: { project: Project }) {
  return (
    <Link
      href={`/projects/${project.id}`}
      className="block rounded-lg p-4 transition-colors group"
      style={{ background: "#141414", border: "1px solid #262626" }}
      onMouseEnter={(e) => { e.currentTarget.style.borderColor = "#333333"; e.currentTarget.style.background = "#191919"; }}
      onMouseLeave={(e) => { e.currentTarget.style.borderColor = "#262626"; e.currentTarget.style.background = "#141414"; }}
    >
      <div className="flex items-start justify-between mb-2">
        <h3 className="text-sm font-medium" style={{ color: "#ededed" }}>{project.name}</h3>
        <span
          className="text-xs opacity-0 group-hover:opacity-100 transition-opacity"
          style={{ color: "#3ecf8e" }}
        >
          →
        </span>
      </div>
      <p
        className="text-xs truncate mb-2"
        style={{ color: "#3a3a3a", fontFamily: "var(--font-geist-mono), monospace" }}
      >
        {project.id}
      </p>
      <p className="text-xs" style={{ color: "#525252" }}>
        {new Date(project.createdAt).toLocaleDateString()}
      </p>
    </Link>
  );
}
