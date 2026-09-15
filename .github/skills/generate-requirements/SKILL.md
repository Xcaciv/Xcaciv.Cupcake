---
name: generate-requirements
description: "Use when generating or updating a REQUIREMENTS.md file from project notes, briefs, interviews, or repository documentation. Produces implementation-neutral functional and non-functional requirements with testable acceptance criteria, and asks for source input when none is provided."
---

# Generate Requirements

Create or update the repository-root `REQUIREMENTS.md`. This skill defines **what** the system must do, not **how** it will be built.

## Input Gate

1. Look for user-provided project notes, a product brief, interview answers, an existing `REQUIREMENTS.md`, or clearly relevant repository documentation.
2. Treat the supplied material as the source of truth. Do not invent features, actors, data, integrations, constraints, limits, or acceptance behavior.
3. If no usable input is available, stop and ask the user for project notes. Request, at minimum:
   - project purpose
   - primary users or actors
   - core workflows
   - business objects or data entities
   - external integrations
   - authentication or roles
   - regulatory, privacy, or security constraints
4. If the input is incomplete, continue with the supported facts and record material gaps in `## Open Questions`. Do not fill gaps with common industry behavior.

## Source and Scope Rules

- Prefer explicit user-provided notes over repository inference.
- Repository documentation may clarify terminology or constraints, but it cannot authorize unsupported scope.
- Use `UNKNOWN` for a fact the sources do not provide.
- Use `TO BE DECIDED` only when the required behavior is clear but one value, rule, or limit remains unresolved.
- Mark any necessary inference as `ASSUMPTION`; do not present it as a requirement.
- Keep architecture, technology, framework, deployment, vendor, database, and visual design choices out of this document unless the source explicitly makes them a product constraint.
- Do not add a requirement merely because it is conventional, technically convenient, or needed by a possible implementation.

## Workflow

### 1. Extract the domain

Identify actors, goals, workflows, system responses, data affected, permissions, validations, failure cases, integrations, quality constraints, and unresolved decisions. Normalize synonyms without changing meaning.

### 2. Establish the requirement inputs

Fill each field exactly once under `## Required Requirement Inputs`. Use only source-supported facts and the markers defined above.

### 3. Write functional requirements

Group requirements by capability or workflow under `###` headings. Use stable IDs in this format:

`- **FR-1.1** The system MUST ...`

Each requirement MUST:

- describe one independently testable, externally observable behavior;
- identify the actor, action, system response, and affected data when known;
- use normative terms such as `MUST`, `MUST NOT`, `SHOULD`, or `MAY` precisely;
- include validation, authorization, success, and failure behavior when supported by the source; and
- trace directly to the supplied notes or repository documentation.

Do not include implementation details, API shapes, class names, database schemas, technology choices, or UI styling unless they are explicitly part of the required behavior.

### 4. Capture unresolved questions

Add only questions that materially affect scope, behavior, authorization, data handling, quality, or requirement testing and that the sources do not answer. Use stable IDs in this format:

`- **OQ-1** ...`

If a missing detail makes a proposed requirement untestable, move the issue here instead of writing a vague requirement. Keep a requirement with `TO BE DECIDED` only when its observable behavior is otherwise clear.

### 5. Validate the document

Before finishing, verify that:

- the file is named `REQUIREMENTS.md` at the repository root;
- the sections appear exactly in this order: `## Required Requirement Inputs`, `## Functional Requirements`, `## Open Questions`;
- every required input field appears exactly once;
- every functional requirement has a unique, stable ID and one observable behavior;
- no requirement exceeds the evidence in the source material;
- unresolved decisions are represented as open questions or clearly marked `TO BE DECIDED`;
- functional and non-functional requirements are both included when supported by the input;
- acceptance criteria are testable without requiring a particular implementation; and
- no external dependency, framework, vendor, or service has been introduced without explicit source support.

If the repository already contains `REQUIREMENTS.md`, update it carefully, preserve supported existing decisions, and remove only content contradicted by newer explicit input. Do not rewrite unrelated documentation.

## Required Output

`REQUIREMENTS.md` must contain exactly these sections in this order:

## Required Requirement Inputs

Each field appears exactly once and is filled only from the source material or clearly relevant repository documentation. Use `UNKNOWN` when not provided:

- Project purpose:
- Primary users / actors:
- Core workflows:
- Business objects / data entities:
- External integrations:
- Authentication / roles:
- Regulatory or privacy constraints:

## Functional Requirements

Concise, grouped requirements using stable IDs and normative language. Include non-functional requirements here under an appropriate capability heading only when the source supports them; keep them externally observable and testable.

## Open Questions

Only material unresolved questions, each with a stable `OQ-` ID.
