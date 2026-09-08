# PRD: Cupcake — Reimplementation Specification

> Reverse-engineered from `https://github.com/Xcaciv/Xcaciv.Cupcake` (analysed at the local
> read-only checkout `subject/Xcaciv.Cupcake`) @ commit
> `a05dc6f00d4510ac073f6dd02fbfba2ffb76a5b6`, branch `main`, on 2026-08-31.
>
> Prepared for reimplementation in a different language on a different platform; this document is
> implementation-agnostic. The source's own stack appears only as *Source used* annotations, inside
> External-technology sections, and in the traceability appendix.
>
> **Source licence:** BSD 3-Clause, © 2022 Alton Crossley (`LICENSE:1-4`). The licence permits
> reimplementation; attribution and the warranty disclaimer must accompany any redistribution of the
> original work or substantial portions of it. A clean-room reimplementation from this document does
> not redistribute the original.

---

## How to read this document

**Evidence notation.** Every requirement is traceable.

| Notation | Meaning |
|---|---|
| `path/to/file.ext:12-30` | Directly observed in the source repository at the pinned commit. Repo-relative. |
| `OUT-OF-REPO: path:12` | Observed in the **external command framework** the product depends on, read from its public repository at tag `v2.1.2` (commit `f34dedca8dc6d690290b2139acbd3e9b8264349c`). See the version caveat below. |
| `INFERRED` | Reasoned from evidence rather than directly observed. Load-bearing requirements marked this way should be re-verified before a clone commits to them. |
| `QUIRK` | Behaviour that looks like a defect. Documented **as observed**, never silently fixed and never silently blessed. Every quirk with an observable consequence has an acceptance criterion that pins it, and a keep-or-fix entry in §11. |

**Three caveats that colour the whole document.**

1. **Nothing was executed.** The product does not build at the pinned commit (§2.4, OQ-1, OQ-2).
   Every statement here is read from source. Where a dossier confirmed arithmetic or a platform
   primitive by running an equivalent construct in a scratch directory, that is stated at the point
   of claim.
2. **The framework reference is one patch release ahead.** The product pins framework versions
   2.1.1 / 2.1.0; the only obtainable source is tag `v2.1.2`, because the packages are absent from
   the public index and the repository's own feed configuration cannot reach them (§2.4). Every
   `OUT-OF-REPO:` claim therefore carries a small version risk. In-repo literals carry none. The
   repository's own hand-written test doubles already disagree with `v2.1.2` in several members
   (OQ-3), which is direct evidence that the pinned contract differed somewhat.
3. **Most user-visible behaviour comes from the framework, not from this repository.** The product
   is ~950 lines of its own code sitting on a command framework that supplies parsing, pipelines,
   help, argument binding and plugin loading. Per the commissioning decision, this PRD documents
   that framework as a **required capability** with its semantics specified (§7.1, §6), not as an
   opaque dependency — a clone must build or source an equivalent.
