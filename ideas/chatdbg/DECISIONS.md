# ChatDbg Rebuild — Product Decision Log

> Owner decisions that resolve questions the reverse-engineering process deliberately left open.
> Each entry records the decision verbatim in substance, the questions it closes, the exact behavioural
> consequences written into the specifications, and every artifact edited so the change is auditable.
> Specifications state the *decided* behaviour; this file records *that it was decided, by whom, and when*.

---

## D-001 — No fabricated telemetry, ever; auto-disable with guidance; UI gating where the platform allows it

| | |
|---|---|
| **Date** | 2026-08-29 |
| **Decided by** | Product owner (user), in review of the delivered PRD and specifications |
| **Status** | **Accepted — definitive** ("Here is a definitive decision") |
| **Supersedes** | The open recommendation in PRD §11.3; ratifies and extends TECH-STACK-TARGET ADR-6 |

### The decision, as given

1. **Nothing should be faked.** No path in the product may invent probability data and present it as measured. This applies to all three fabricators found in the source: the hosted-cloud generator (fixed `ln(0.9)` + three synthetic alternatives), the local engine's temperature step-function, and the token-inspection index-derived formulas.
2. **When log probabilities are enabled and the active provider does not support them, the product must disable the log-probabilities setting** and **instruct the user that they need to use a different LLM service provider** to obtain the capability.
3. **Better still, where the platform allows it** (the full-screen shell, and any other surface with stateful controls), the product should **disable the UI control for turning log probabilities on** while an unsupporting provider is active, and **display a non-blocking error when the user tries to view them**.

### Interpretation applied in the specifications

- "Disable" means: the persisted `enableLogProbabilities` setting is flipped **off** and persisted, with a visible one-line notice naming the provider, stating that it does not support token log probabilities, and naming at least one configured-or-available provider that does (or stating that none currently does). The notice is informational, not an error — the chat turn itself proceeds normally without probability capture.
- The auto-disable fires at the **earliest capability-known moment**: on backend switch (`MODEL USE` / provider setting change) when capture is already on, and on session start when the loaded settings combine capture-on with an unsupporting provider. It never fires mid-turn on a *transient* failure — a provider that *supports* the capability but omitted it on one response produces the honest "none were returned" notice and leaves the setting alone (a transient absence is not a capability absence).
- "Non-blocking error" means: in the full-screen shell, attempting to open the probability panel or toggle the view for a message that has no probability data (because the provider could not supply it) shows a transient status-bar message — never a modal — stating the data is unavailable and why. In the line-oriented shell the equivalent is a single stderr-style advisory line.
- UI gating: the full-screen Settings dialog's log-probabilities controls are rendered disabled (greyed, focusable-with-explanation) while the active provider lacks the capability; the explanation names the provider and points at `MODEL CAPS`. The line-oriented `/set enableLogProbabilities true` and `/logprobs enable` commands **refuse** with the same instruction rather than silently succeeding.
- Capability is determined by declaration (the backend's declared capability record, per GR-18 / `MODEL CAPS`), never by attempting a call and interpreting its failure.
- The **explicitly labelled demonstration mode** (`TOKEN DEMO` / `/demologprobs`) remains the *only* permitted producer of synthetic data, and every record it emits is stamped `synthetic: true`. This is unchanged from the prior specification and is compatible with the decision: demo data is labelled, never presented as measurement.

### Questions this closes

| Item | Where | Disposition |
|---|---|---|
| OQ-A1 (fabricate at all? mark as invented?) | PRD §11.2 Theme A | **Resolved: never fabricate.** Marking is moot on live paths; demo mode stays labelled. |
| QD-1 (fabricated confidence presented as measured) | PRD §11.3 Tier 1 | **Decided: fix** — remove all three fabricators. |
| §11.3 lead decision (keep / label / remove) | PRD §11.3 | **Remove from live paths; keep labelled demo** — now with the auto-disable + instruct + UI-gating behaviour added. |
| GR-19 routing ("must not be taken silently in either direction") | PRD §6 | Decision now taken and recorded here; GR-19 restated as the decided requirement. |
| ADR-6 ("never fake a tier") | TECH-STACK-TARGET §6 | **Ratified** by the owner and extended with the concrete auto-disable and UI-gating behaviour. |

### Requirements consequences (what changed in each artifact)

| Artifact | Change |
|---|---|
| `PRD-ChatDbg.md` §6 **GR-19** | Retitled and restated as the decided requirement (**no fabrication; auto-disable + instruct; UI gating; non-blocking view error**). The observed source behaviour is retained beneath it as the quirk record it always was, for traceability and for acceptance tests written against the source. |
| `PRD-ChatDbg.md` §6 **GR-18** | Gains the log-probabilities-specific degradation rule: capability-known-in-advance, auto-disable semantics, transient-absence exemption. |
| `PRD-ChatDbg.md` §7.6 **FR-6.47–FR-6.52** | Marked **superseded by D-001** (observed record retained); replacement requirement FR-6.52a added: on a capability-absent provider the adapter returns the reply with `logProbabilities` absent and a capability notice; the shell then applies GR-19's auto-disable. |
| `PRD-ChatDbg.md` §7.8 **FR-8.38** | Marked **superseded by D-001**; replacement: per-token probability comes from the measured distribution or is absent — never estimated from temperature. |
| `PRD-ChatDbg.md` §7.9 **FR-9.72** | Marked **superseded by D-001**; replacement FR-9.72a: no substitution; honest notice; auto-disable per GR-19. Demo-mode FRs (FR-9.55, FR-9.60 and neighbours) unchanged — the demo is labelled synthetic. |
| `PRD-ChatDbg.md` §11 | OQ-A1, QD-1 and the §11.3 lead marked resolved with pointers here. |
| `ULTIMATE-TOOL-REFERENCE.md` §0.6 | Row 2 updated from "recommendation" to "owner-ratified decision D-001" with the concrete behaviour. |
| `ULTIMATE-TOOL-REFERENCE.md` §6.3.1 `TOKEN CAPTURE` | `on` refuses against a capability-absent backend with the switch-provider instruction; a backend switch with capture on auto-disables and notifies; refusal text specified. |
| `ULTIMATE-TOOL-REFERENCE.md` §6.3.9 `TOKEN SHOW` | Viewing a message without probability data produces the non-blocking unavailable notice, never a modal, never an invented rendering. |
| `ULTIMATE-TOOL-REFERENCE.md` §5 `MODEL USE` / `MODEL CAPS` | `MODEL USE` performs the auto-disable when switching to a capability-absent backend while capture is on, and says so in its output; `MODEL CAPS` is the authority the instruction points the user at. |
| `ULTIMATE-TOOL-REFERENCE.md` §B/§D (host) | Full-screen shell: log-probabilities controls disabled-with-explanation while the active provider lacks the capability; probability-view attempts produce a transient status-bar notice (non-blocking). Line-oriented shell: refusal + advisory line. |
| `TECH-STACK-TARGET.md` **ADR-6** | Status annotated *ratified by owner 2026-08-29 (D-001)*; consequence extended with auto-disable + UI gating. |

### Standard notice texts introduced (normative)

- **Auto-disable notice** (both shells, on switch or load):
  `Token log probabilities disabled: provider '{provider}' does not support them. Use a provider that does (see MODEL CAPS){, e.g. '{example}'| — none of the configured providers currently do}.`
- **Enable refusal** (line-oriented `/set` and `/logprobs enable`; same text as the full-screen control's explanation):
  `Cannot enable token log probabilities: provider '{provider}' does not support them. Switch providers first (see MODEL CAPS).`
- **Non-blocking view notice** (full-screen status bar / line-oriented advisory):
  `No token probability data for this message: provider '{provider}' does not supply it.`

### What this decision does *not* change

- The demonstration mode and its `synthetic: true` stamping (already specified).
- The honest "none were returned" notice for **transient** absence on a *supporting* provider (GR-18); only *capability* absence triggers auto-disable.
- The Bedrock finding (structurally incapable of logprobs — verified by disassembly): unchanged as fact; this decision defines what the product *does about it*.
- Source-behaviour documentation: observed fabrication remains recorded in §7 and the dossiers as what the source *does*, because the PRD's traceability contract requires it. It is now marked superseded rather than open.

---

*Next decision: D-002 (unallocated).*
