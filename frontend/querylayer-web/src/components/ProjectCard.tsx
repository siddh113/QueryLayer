import Link from "next/link";
import type { Project } from "../types";

export default function ProjectCard({ project }: { project: Project }) {
  return (
    <Link
      href={`/projects/${project.id}`}
      className="block bg-white rounded-lg border border-gray-200 p-5 hover:border-blue-300 hover:shadow-sm transition-all"
    >
      <h3 className="font-semibold text-gray-900">{project.name}</h3>
      <p className="text-xs text-gray-400 mt-2">
        Created {new Date(project.createdAt).toLocaleDateString()}
      </p>
      <p className="text-xs font-mono text-gray-400 mt-1 truncate">{project.id}</p>
    </Link>
  );
}
