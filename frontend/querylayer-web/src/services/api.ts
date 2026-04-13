import axios from "axios";
import type { Project, BackendSpec } from "../types";

export const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5000",
});

api.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401 && typeof window !== "undefined") {
      const path = window.location.pathname;
      if (path !== "/login" && path !== "/signup") {
        localStorage.removeItem("token");
        window.location.href = "/login";
      }
    }
    return Promise.reject(err);
  }
);

// Auth (platform-level, no projectId needed)
export async function login(email: string, password: string) {
  const res = await api.post("/platform/login", { email, password });
  return res.data as { token: string; userId: string; role: string };
}

export async function signup(email: string, password: string) {
  const res = await api.post("/platform/signup", { email, password });
  return res.data as { token: string; userId: string; role: string };
}

// Projects
export async function getProjects() {
  const res = await api.get("/platform/projects");
  return res.data as Project[];
}

export async function createProject(name: string) {
  const res = await api.post("/platform/projects", { name });
  return res.data as Project;
}

export async function getProject(id: string) {
  const res = await api.get(`/platform/projects/${id}`);
  return res.data as Project;
}

// Spec
export async function getSpec(projectId: string) {
  const res = await api.get(`/platform/projects/${projectId}/spec`);
  return res.data as { specJson: string; version: number };
}

export async function updateSpec(projectId: string, spec: BackendSpec) {
  const res = await api.put(`/projects/${projectId}/spec`, spec);
  return res.data as { message: string; version: number; projectId: string };
}

export async function validateSchema(projectId: string) {
  const res = await api.get(`/projects/${projectId}/schema/validate`);
  return res.data;
}

// AI Spec Generation
export async function generateSpec(projectId: string, prompt: string) {
  const res = await api.post(`/projects/${projectId}/generate-spec`, { prompt });
  return res.data as { spec: BackendSpec; specJson: string };
}

export async function editSpec(projectId: string, instruction: string) {
  const res = await api.post(`/projects/${projectId}/edit-spec`, { instruction });
  return res.data as { spec: BackendSpec; specJson: string };
}

// Key Management
export interface ApiKeyRecord {
  id: string;
  keyType: string;
  keyPrefix: string;
  name?: string;
  createdAt: string;
  lastUsedAt?: string;
  revokedAt?: string;
  isActive: boolean;
}

export interface GeneratedKey extends ApiKeyRecord {
  rawKey: string;
  warning?: string;
}

export async function getKeys(projectId: string) {
  const res = await api.get(`/projects/${projectId}/keys`);
  return res.data as ApiKeyRecord[];
}

export async function generateKey(projectId: string, keyType: "public" | "secret", name?: string) {
  const res = await api.post(`/projects/${projectId}/keys/generate`, { keyType, name });
  return res.data as GeneratedKey;
}

export async function revokeKey(projectId: string, keyId: string) {
  const res = await api.post(`/projects/${projectId}/keys/${keyId}/revoke`);
  return res.data as { message: string };
}

export async function rotateKey(projectId: string, keyId: string) {
  const res = await api.post(`/projects/${projectId}/keys/${keyId}/rotate`);
  return res.data as GeneratedKey;
}

// DX: OpenAPI + Examples
export async function getOpenApi(projectId: string) {
  const res = await api.get(`/projects/${projectId}/openapi`);
  return res.data;
}

export async function getExamples(projectId: string) {
  const res = await api.get(`/projects/${projectId}/examples`);
  return res.data as EndpointExample[];
}

export interface EndpointExample {
  method: string;
  path: string;
  entity: string;
  auth?: string;
  sampleBody?: string;
  curl: string;
  fetch: string;
  url: string;
}

// Runtime API calls
export async function callEndpoint(
  projectKey: string,
  method: string,
  path: string,
  body?: Record<string, unknown>
) {
  const url = `/api/${projectKey}${path}`;
  const config = { method: method.toLowerCase(), url, data: body };
  const res = await api.request(config);
  return res.data;
}
