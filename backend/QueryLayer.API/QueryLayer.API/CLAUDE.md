# Claude Coding Instructions

## Project

This is the QueryLayer platform.

Completed:

Sprint 1: Platform Foundations
Sprint 2: Runtime API Engine
Sprint 3: Schema Generation
Sprint 4: Authentication + RBAC
Sprint 5: Frontend Dashboard

The system now:

* Lets users create projects
* Define backend specs manually
* Automatically generates APIs and database schema
* Provides a UI dashboard

---

## Goal

Implement Sprint 6: AI Spec Generation.

Users should be able to:

* Describe their backend in natural language
* Automatically generate a valid backend spec
* Modify existing specs using AI

---

## Core Concept

AI does NOT generate code.

AI generates and edits the **backend spec JSON**.

The runtime + schema engine execute that spec.

---

## Sprint 6 Features

### AI Service Integration

Create a service to interact with OpenAI API.

Use:

OPENAI_API_KEY from environment

Model:

gpt-4.1-mini (default)

---

### Spec Generation Endpoint

Create endpoint:

POST /projects/{id}/generate-spec

Request:

{
"prompt": "I want a task management app where users can create tasks..."
}

---

### Spec Editing Endpoint

Create endpoint:

POST /projects/{id}/edit-spec

Request:

{
"instruction": "Add due_date to Task and allow filtering by status"
}

---

### AI Prompt Design

System prompt:

"You are an expert backend architect.
Generate a valid backend specification JSON following this schema."

Include:

* Entities
* Fields
* Endpoints
* Auth
* Roles

---

### Provide Schema Format to AI

Send:

* Example valid spec
* Rules for fields and relationships
* Required JSON structure

---

### JSON Validation

After AI response:

* Parse JSON
* Validate against schema
* Reject invalid output

---

### Spec Repair System

If JSON invalid:

* Retry generation
* Or fix using second AI call

---

### Escalation Strategy

If generation fails:

1. Retry with same model
2. Retry with stronger model:

gpt-4.1

---

### Integration with Existing System

After valid spec:

1. Show preview in UI
2. User confirms
3. Save spec
4. Trigger schema generation (Sprint 3)
5. Runtime updates (Sprint 2)

---

### Frontend AI Interface

Add UI inside project page.

---

### Generate Spec UI

Component:

AI Spec Generator

Features:

* Text input for description
* Generate button
* Loading indicator

---

### Edit Spec UI

Component:

AI Spec Editor

Features:

* Input for modification instructions
* Apply changes button

---

### Preview Mode (Important)

Before saving:

* Show generated spec
* Allow manual edits
* Confirm button

---

### Diff View (Optional but Strong)

Show:

Old spec vs New spec

---

### Error Handling

Handle:

* Invalid JSON
* API failures
* Timeout

Display clear UI errors.

---

## Required Services

Create:

Services/AI/AIService
Services/AI/PromptBuilder
Services/AI/SpecValidator
Services/AI/SpecRepairService

---

## Backend Logic Flow

Generate Spec:

1. Receive prompt
2. Build AI request
3. Call OpenAI
4. Validate JSON
5. Return spec

Edit Spec:

1. Fetch current spec
2. Send spec + instruction to AI
3. Generate updated spec
4. Validate
5. Return

---

## Security

* Do NOT execute arbitrary AI output
* Only accept valid spec structure
* Validate all fields

---

## Logging

Log:

* AI prompts (sanitized)
* Responses (trimmed if large)
* Validation failures

---

## Testing Requirements

Test:

1. Generate spec from prompt
2. Validate spec correctness
3. Save spec
4. Confirm schema created
5. Test runtime APIs

Test editing:

* Modify spec
* Add fields
* Add endpoints

---

## Program.cs Updates

Ensure:

* AI services are registered
* HttpClient configured
* Environment variables loaded

---

## Important Constraints

* Do NOT bypass spec validation
* Do NOT allow malformed schema
* Keep AI output deterministic where possible

---

## Expected Outcome

The system should:

* Convert natural language → backend spec
* Allow iterative editing via AI
* Fully integrate with runtime + schema

This is your core product advantage.

---

## After Implementation

1. Test with multiple prompts
2. Fix edge cases
3. Improve prompts for accuracy
4. Deploy backend and frontend

---

## Result

You now have:

* AI-native backend platform
* Fully automated backend generation
* End-to-end product ready for demos

This completes Sprint 6.


## After Implementation

1. Run dotnet build
2. Fix compile errors
3. Show summary of created files
4. Generate SQL for metadata tables
5. Generate SQL for sample tasks table
