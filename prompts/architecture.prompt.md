Create `ARCHITECTURE.md` in the repository root. `ARCHITECTURE.md` owns HOW the system is built: 
components, interfaces, data flow, trust boundaries, technology choices and rationale.
Read `REQUIREMENTS.md` and `DESIGN.md` first. `REQUIREMENTS.md` is the source of truth for system 
behavior, users, workflows, data, and integrations. `DESIGN.md` is the source of truth for visual 
language, interaction conventions, accessibility expectations, and the frontend capabilities the 
client must support. No implementation exists yet — architect from these documents, not from imagined 
code or dependencies.
Marker convention: `UNKNOWN` for facts the input documents do not provide; `TO BE DECIDED` for 
decisions not yet made; label every unsupported inference `ASSUMPTION`. Never present provisional 
choices as final.
Treat the notes below as explicit human-provided input:
## Architecture Notes
<<ARCHITECTURE_NOTES_OR_NONE>>
## File contents
`ARCHITECTURE.md` contains exactly these sections:
### Required Architecture Inputs
Each field exactly once, starting from the values shown. `System purpose`, `Primary use cases`, and 
`Target users / actors` stay as pointers — `REQUIREMENTS.md` owns that content. Change a `TO BE 
DECIDED` field only when an explicit decision appears in `REQUIREMENTS.md`, `DESIGN.md`, or the 
architecture notes; use `UNKNOWN` for a fact no input source provides.- Requirements source: REQUIREMENTS.md- Design source: DESIGN.md- System purpose: See REQUIREMENTS.md- Primary use cases: See REQUIREMENTS.md- Target users / actors: See REQUIREMENTS.md- Runtime environment: web application- Server framework: TO BE DECIDED- Client framework: TO BE DECIDED- API style and integration model: TO BE DECIDED- Authentication and session model: TO BE DECIDED- Data model expectations: TO BE DECIDED- Deployment model: TO BE DECIDED- Scale expectations: TO BE DECIDED- Security expectations: TO BE DECIDED — addressed in a later security specification, SECURITY.md
### Initial Architecture (Provisional)
A concise first-pass logical architecture supporting the functional requirements:- Stay implementation-neutral wherever technology decisions are unresolved; choose no framework, 
database, cloud provider, hosting platform, messaging system, or vendor unless an input source 
explicitly requires it.- Define major logical components or boundaries and their responsibilities, the primary 
request/response and data flows, where business rules are enforced, which component owns each major 
business object, and external system boundaries where integrations exist.- Account for the frontend behavior and accessibility expectations in `DESIGN.md`.- Include only boundaries the requirements support (candidates: browser client, server-side 
application/API, identity and session handling, data persistence, external integrations, background 
processing). Don't add a boundary merely because it is common in web applications.
For each component:- **Component name**- **Responsibility:** ...- **Inputs:** ...- **Outputs:** ...- **Data owned or accessed:** ...
SPEC-DRIVEN BOOTSTRAP / CLAUDE CODE · CLAUDE 5 EDITION- **Open decisions:** ...
### Requirement Traceability
Map each component or boundary to the functional requirement groups it supports, using exact IDs from 
`REQUIREMENTS.md`. Status values: `SUPPORTED`, `PARTIALLY DEFINED`, `TO BE DECIDED`. Claim `SUPPORTED` 
only when a named component or boundary is responsible for the requirement; list any requirement 
without an assigned responsibility as `TO BE DECIDED`.
### Dependency Rules
Implementation-neutral rules governing dependencies between boundaries, format `- **DR-1** ...`, each 
independently reviewable. The rules MUST:- Keep presentation concerns from becoming the source of business rules.- Keep external integrations from leaking vendor-specific behavior through the system.- Make data ownership and mutation responsibility explicit.- Require dependencies to cross documented interfaces, with no circular dependencies.- Remain compatible with unresolved framework and deployment decisions.
Include only rules justified by the provisional architecture.
## Scope boundary
Detailed security controls belong to `SECURITY.md`, created next. File inventories, directory 
structures, deployment topology, and vendor comparisons belong to implementation. Keep the document 
compact, concrete, and internally consistent — every implementation issue will trace back to it.
