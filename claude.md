# Claude Coding Instructions

## Project

This is the QueryLayer platform.

Completed:

* Sprint 1: Platform Foundations
* Sprint 2: Runtime API Engine
* Sprint 3: Schema Generation
* Sprint 4: Authentication + RBAC
* Sprint 5: Frontend Dashboard
* Sprint 6: AI Spec Generation
* Sprint 7: Developer Experience

The platform already supports:

* Dynamic backend generation
* Runtime APIs
* Schema creation
* JWT authentication
* RBAC
* Dashboard UI
* API documentation and testing tools

---

## Goal

Implement secure frontend connectivity and key management.

The system must clearly separate:

1. JWT tokens for frontend user authentication
2. API keys for secure server-to-server or internal access

Also add support for:

* public client key
* secret server key

The dashboard must clearly explain how users should connect their frontend safely.

---

## Security Design

### Authentication Types

#### 1. JWT Token

Use JWT for frontend applications.

Frontend flow:

* user signs up or logs in
* backend returns JWT
* frontend stores token
* frontend sends:

Authorization: Bearer <token>

JWT is used for:

* browser frontend
* mobile apps
* authenticated user requests
* RBAC / user-level access control

---

#### 2. Secret API Key

Use secret API keys only for:

* backend servers
* internal services
* trusted server-to-server communication
* admin automation
* testing tools like Postman if explicitly allowed

Secret API keys must never be shown in public frontend code.

Header:

x-api-key: <secret_key>

---

#### 3. Public Client Key

Add a public client key for limited frontend-safe identification if needed.

This key does NOT grant privileged access.
This key is optional in MVP, but should be supported in architecture.

Use cases:

* identifying project in public SDK
* low-risk client initialization
* analytics / onboarding
* public read-only scenarios if enabled later

Header or config:
x-public-key: <public_key>

This key must have very limited permissions and must not bypass JWT auth.

---

## Required Changes

### 1. Project Key Model

Do NOT use raw Project ID as the security mechanism.

Create proper key entities.

Support:

* public_key
* secret_key

Store keys in database.

Fields:

* id
* project_id
* key_type
* key_hash
* key_prefix
* created_at
* last_used_at
* revoked_at

Key types:

* public
* secret

Never store raw secret keys in plain text after creation.
Store hashed values for lookup/validation.

---

### 2. Key Generation Service

Create:

KeyManagementService

Responsibilities:

* generate public key
* generate secret key
* hash and store keys
* validate incoming keys
* revoke keys
* rotate keys

Format examples:

* ql_pub_xxxxxxxxx
* ql_sec_xxxxxxxxx

Use cryptographically secure random generation.

---

### 3. Runtime Authentication Flow

Update runtime request pipeline.

Order of evaluation:

1. Resolve project from route
2. Check endpoint auth mode
3. If endpoint requires user auth:

   * validate JWT
4. If endpoint allows service access:

   * validate secret API key
5. If public key present:

   * treat only as project/client identifier
   * do not grant elevated permissions
6. Apply RBAC and endpoint rules
7. Execute request

---

### 4. Endpoint Auth Modes

Support these auth modes cleanly:

* public
* authenticated
* admin
* service

Behavior:

public

* no JWT required
* optional public key allowed

authenticated

* valid JWT required

admin

* valid JWT required
* role must be admin

service

* valid secret API key required

Do NOT allow public key to satisfy authenticated or admin endpoints.

---

### 5. Frontend Guidance in Dashboard

Update project dashboard UI.

Current UI shows only:

* project info
* project id / api key

This is not sufficient and can mislead users into exposing secret keys.

Replace with a clearer integration section.

Add section:

## Frontend Integration

Show:

### Base API URL

Example:
https://your-domain.com/api/{projectKey}

### For Frontend Apps

Use JWT authentication.

Steps:

1. Sign up or log in user
2. Receive JWT token
3. Send:
   Authorization: Bearer <token>

### For Server-to-Server Usage

Use secret API key.

Header:
x-api-key: <secret_key>

### Public Key

If present, show as:
Client Key
Explain that it is not a secret and does not replace JWT auth.

Add copy buttons for:

* base URL
* public key
* secret key
* sample frontend code

Important:

* secret key should be hidden by default
* user must click reveal
* add warning text:
  "Do not expose secret keys in frontend code"

---

### 6. Auth Documentation UI

Add a documentation panel on project page:

Sections:

* How frontend authentication works
* How backend authentication works
* Example React integration
* Example server integration

---

### 7. Example Code Generation

Generate examples in dashboard.

#### React / Frontend Example

Show example using JWT:

const token = localStorage.getItem("token");

fetch("https://your-domain.com/api/{projectKey}/tasks", {
method: "GET",
headers: {
"Authorization": `Bearer ${token}`
}
});

#### Login Example

fetch("https://your-domain.com/auth/login", {
method: "POST",
headers: {
"Content-Type": "application/json"
},
body: JSON.stringify({
email: "[user@example.com](mailto:user@example.com)",
password: "password123"
})
});

#### Server Example

fetch("https://your-domain.com/api/{projectKey}/tasks", {
method: "GET",
headers: {
"x-api-key": "ql_sec_xxxxx"
}
});

---

### 8. Backend Endpoints for Key Management

Create endpoints:

GET /projects/{id}/keys
POST /projects/{id}/keys/generate
POST /projects/{id}/keys/rotate
POST /projects/{id}/keys/revoke

Response should never return hashed keys.
Return raw secret key only once at creation time.

---

### 9. UI for Key Management

Add project settings or keys page.

Features:

* list keys
* show key type
* created date
* last used date
* status
* generate new key
* revoke key
* rotate secret key

Secret keys:

* shown once on creation
* hidden afterwards
* copy on creation
* warning message displayed

---

### 10. Validation Rules

Implement strict rules:

* frontend requests must use JWT for user endpoints
* public key cannot access protected resources
* secret key cannot be exposed in browser examples
* admin endpoints require admin JWT
* service endpoints require secret key

---

## Required Services

Create:

Services/Auth/KeyManagementService
Services/Auth/ApiKeyValidator
Services/Auth/PublicKeyValidator
Services/Auth/IntegrationGuideService

Create or update:

Middleware/AuthMiddleware
Services/Auth/JwtService
Services/Auth/PermissionService

---

## Frontend Components

Create or update:

ProjectKeysPanel
FrontendIntegrationGuide
SecretKeyWarning
KeyGenerationModal
KeyList
CodeExamplePanel

---

## Database Changes

Create keys table, for example:

project_api_keys

* id
* project_id
* key_type
* key_hash
* key_prefix
* created_at
* last_used_at
* revoked_at

Optional:

* name
* environment
* created_by

---

## Logging

Log:

* key generation
* key revocation
* failed key validation
* JWT auth failures
* attempts to use wrong auth type

Do not log raw secrets.

Use Serilog.

---

## Error Handling

Return clear structured errors.

Examples:

{
"error": "JWT token required for this endpoint"
}

{
"error": "Secret API key required for service endpoint"
}

{
"error": "Public key cannot be used for authenticated endpoint"
}

{
"error": "Invalid or revoked API key"
}

---

## Testing Requirements

Test all of the following:

### JWT Frontend Flow

1. sign up user
2. log in
3. receive JWT
4. call authenticated endpoint with JWT
5. verify success

### Secret Key Flow

1. generate secret key
2. call service endpoint with x-api-key
3. verify success

### Public Key Flow

1. generate public key
2. call public endpoint
3. verify limited access only

### Security Checks

1. try using secret key in frontend example path and ensure docs warn against it
2. try using public key on authenticated endpoint and verify failure
3. try using no JWT on authenticated endpoint and verify 401
4. try using non-admin JWT on admin endpoint and verify 403
5. revoke key and verify access fails

---

## Program.cs Updates

Ensure:

* key services are registered
* auth middleware is updated
* JWT and key validation coexist safely
* security policies are explicit

---

## Important Constraints

* Do NOT use raw project id as the real security credential
* Do NOT expose secret keys by default in UI
* Do NOT let public key bypass JWT auth
* Do NOT break existing runtime API flow
* Keep backward compatibility where possible, but migrate toward proper key model

---

## Expected Outcome

After implementation:

* frontend apps authenticate using JWT
* backend/services authenticate using secret API keys
* optional public key can identify client safely with limited access
* dashboard clearly explains how to connect frontend securely
* users are no longer confused about which key to use

This feature makes the platform safe for real-world use.

---

## After Implementation

1. run backend
2. run frontend
3. create a project
4. generate keys
5. test JWT login flow from frontend
6. test service access with secret key
7. verify dashboard instructions are clear
8. fix all compile/runtime errors
9. summarize all created files and updated flows
