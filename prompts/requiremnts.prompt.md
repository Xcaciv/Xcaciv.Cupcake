
Create `REQUIREMENTS.md` in this repository. `REQUIREMENTS.md` owns WHAT the system must do: 
functional and non-functional requirements, each with testable acceptance criteria.
The project notes below are the sole source of truth: no speculative features, no behavior they don't 
support. The repository is empty, so there is no code to infer behavior from. Produce the best 
document the notes support and record unresolved decisions in `## Open Questions`.
# Project Notes
<<PROJECT_NOTES>>
# File contents
`REQUIREMENTS.md` contains exactly these sections in order:
## Required Requirement Inputs
Each field exactly once, filled only from the project notes or clearly relevant repository 
documentation. Use `UNKNOWN` when not provided; never invent details.- Project purpose:- Primary users / actors:- Core workflows:- Business objects / data entities:- External integrations:- Authentication / roles:- Regulatory or privacy constraints:
## Functional Requirements
Convert the notes into concise functional requirements:- Group under `###` headings by capability or workflow.- Use stable IDs and this format: `- **FR-1.1** The system MUST ...`- Use normative terms where appropriate: MUST, MUST NOT, SHOULD, MAY.- One independently testable, externally observable behavior per requirement. No implementation 
details.- Identify actor, action, system response, and data affected when known.- Include validation, authorization, and failure behavior when stated or clearly required by the 
workflow.- Every requirement must trace to the project notes.- If a missing detail makes a requirement untestable, move the issue to `## Open Questions`. Use `TO 
BE DECIDED` only when a requirement is otherwise clear but contains one unresolved value, rule, or 
limit.
## Open Questions
Only unresolved questions that materially affect system behavior, scope, or requirement testing, and 
that the notes do not already answer. Stable IDs: `OQ-1`, `OQ-2`, ...
# Scope boundary
Architecture, technology and framework choices, deployment, and the visual design language belong to 
later documents, not this one. Keep the document compact and concrete — every downstream step loads it 
as context.