export interface User {
  userId: string;
  email: string;
  role: string;
  token: string;
}

export interface Project {
  id: string;
  name: string;
  ownerUserId?: string;
  createdAt: string;
}

export interface ProjectSpec {
  id: string;
  projectId: string;
  specJson: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface FieldSpec {
  name: string;
  type: string;
  primary?: boolean;
  required?: boolean;
  unique?: boolean;
  relation?: {
    table: string;
    column: string;
  };
}

export interface EntitySpec {
  name: string;
  table: string;
  fields: FieldSpec[];
}

export interface EndpointSpec {
  method: string;
  path: string;
  operation: string;
  entity: string;
  auth?: string;
}

export interface PermissionSpec {
  entity: string;
  operations: string[];
  filter?: string;
}

export interface BackendSpec {
  entities: EntitySpec[];
  endpoints: EndpointSpec[];
  permissions: PermissionSpec[];
}

export interface ApiError {
  error: string;
  details?: string;
}
