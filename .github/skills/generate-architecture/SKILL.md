---
name: generate-architecture
description: "Generate or refine an ARCHITECTURE.md file for a project based on input documents like REQUIREMENTS.md and DESIGN.md, or user-provided architecture notes. Use when asked to create, update, or specify system architecture, component boundaries, dependency rules, or generate ARCHITECTURE.md."
argument-hint: "[optional architecture notes or input specs]"
user-invocable: true
disable-model-invocation: false
---

# Generate Architecture (ARCHITECTURE.md)

This skill guides the creation or updating of a clean, spec-driven `ARCHITECTURE.md` in the repository root. `ARCHITECTURE.md` defines HOW the system is built: logical components, responsibilities, interfaces, data flow, trust boundaries, technology choices, rationale, and dependency rules, without introducing premature implementation or external vendor dependencies.

## When to Use

Use this skill when:
- Asked to create, generate, or update `ARCHITECTURE.md`.
- Defining system architecture, component boundaries, or dependency rules from requirements and design specifications.
- Transitioning from requirements (`REQUIREMENTS.md`) and design (`DESIGN.md`) to structural architecture.

## Pre-requisites & Input Checking

Before drafting `ARCHITECTURE.md`:

1. **Check for Input Documents**: Look for `REQUIREMENTS.md` and `DESIGN.md` in the workspace root or docs folder.
2. **Check for User Notes**: Check if the user provided specific architecture notes or context in the prompt/arguments.
3. **If No Inputs Exist**:
   - If neither `REQUIREMENTS.md`, `DESIGN.md`, nor any user architecture notes/context are available, **stop and ask the user** to provide the core system requirements, system purpose, or target context before proceeding.

## Marker Conventions

Strictly adhere to these markers throughout `ARCHITECTURE.md`:
- `UNKNOWN`: For facts or requirements that the input documents or notes do not provide.
- `TO BE DECIDED`: For architectural or tech decisions that have intentionally not been made yet.
- `ASSUMPTION`: Label every unsupported inference or provisional choice as an explicit assumption.
- Never present provisional choices as final.

## Step-by-Step Procedure

### 1. Gather Inputs
- Read `REQUIREMENTS.md` (source of truth for system behavior, users, workflows, data, integrations).
- Read `DESIGN.md` (source of truth for visual/interaction language, accessibility, and client capability expectations).
- Read any user-supplied architecture notes.

### 2. Identify Logical Components & Boundaries
- Identify candidate boundaries supported by requirements (e.g., Client/UI, Server/API, Identity/Session, Data Persistence, External Integrations, Background Workers).
- Include **only** boundaries justified by the functional requirements. Do not add component boundaries merely because they are common in general templates.
- Maintain technology neutrality: avoid choosing specific databases, cloud providers, hosting platforms, or third-party libraries unless explicitly mandated by the input documents.

### 3. Draft `ARCHITECTURE.md` Structure
Format the document with the following exact required sections:

#### Section 1: Required Architecture Inputs
Include key pointers and baseline fields:
- Requirements source: `REQUIREMENTS.md` (or specified path/notes)
- Design source: `DESIGN.md` (or specified path/notes)
- System purpose: See `REQUIREMENTS.md`
- Primary use cases: See `REQUIREMENTS.md`
- Target users / actors: See `REQUIREMENTS.md`
- Runtime environment: (Specify platform/environment based on input, or `TO BE DECIDED`)
- Server framework: `TO BE DECIDED` (or explicit choice if mandated)
- Client framework: `TO BE DECIDED` (or explicit choice if mandated)
- API style and integration model: `TO BE DECIDED`
- Authentication and session model: `TO BE DECIDED`
- Data model expectations: `TO BE DECIDED`
- Deployment model: `TO BE DECIDED`
- Scale expectations: `TO BE DECIDED`
- Security expectations: `TO BE DECIDED` — addressed in `SECURITY.md`

#### Section 2: Initial Architecture (Provisional)
Provide a concise first-pass logical architecture:
- Stay implementation-neutral where decisions are unresolved.
- Define major logical components, responsibilities, request/response and data flows, business rule enforcement points, object ownership, and external system boundaries.
- For each component, detail:
  - **Component name**
  - **Responsibility:** Concise description of core purpose
  - **Inputs:** Expected incoming data, signals, or calls
  - **Outputs:** Emitted data, responses, or side effects
  - **Data owned or accessed:** Core entities or stores managed
  - **Open decisions:** Unresolved architectural choices

#### Section 3: Requirement Traceability
Map components/boundaries to functional requirement groups from `REQUIREMENTS.md` (using exact requirement IDs where available).
- Status values: `SUPPORTED`, `PARTIALLY DEFINED`, `TO BE DECIDED`.
- Claim `SUPPORTED` only when a named component is explicitly responsible for that requirement.

#### Section 4: Dependency Rules
Define implementation-neutral rules governing dependencies between components (formatted as `- **DR-1** ...`, `- **DR-2** ...`):
- Ensure presentation concerns do not dictate business rules.
- Prevent external integrations from leaking vendor-specific types/behavior.
- Make data ownership and mutation responsibilities explicit.
- Require dependencies to cross documented interfaces with no circular dependencies.

### 4. Enforce Scope Boundaries
Keep `ARCHITECTURE.md` focused on architectural boundaries and principles:
- Detailed security controls belong in `SECURITY.md`.
- File inventories, project folder trees, deployment scripts, and specific library choices belong in implementation docs.

### 5. Write `ARCHITECTURE.md`
Save the generated content to `ARCHITECTURE.md` at the repository root.
