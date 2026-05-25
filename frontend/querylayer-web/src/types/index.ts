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

export interface LiveColumnInfo {
  columnName: string;
  dataType: string;
  isNullable: boolean;
  maxLength: number | null;
}

export interface LiveTableInfo {
  name: string;
  entityName: string | null;
  exists: boolean;
  columns: LiveColumnInfo[];
}

export interface LiveSchemaResponse {
  tables: LiveTableInfo[];
}

export interface ColumnDiff {
  table: string;
  column: string;
  detail: string;
}

export interface SchemaSyncResult {
  isInSync: boolean;
  missingTables: string[];
  newColumns: ColumnDiff[];
  extraColumns: ColumnDiff[];
  typeMismatches: ColumnDiff[];
}

export interface SpecPreviewResponse {
  entities: EntitySpec[];
  syncResult: SchemaSyncResult;
  migrationSql: string[];
  hasChanges: boolean;
}
