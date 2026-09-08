#!/usr/bin/env bash
# Assemble the per-section drafts into the single deliverable PRD.
# Fails loudly if any expected section is missing — silent omission would read as "covered".
set -uo pipefail

WS="/mnt/g/reversing/reversing/workspace"
SRC="$WS/prd-sections"
OUT="$WS/PRD-Cupcake.md"

ORDER=(
  S00-header.md
  S01-executive-summary.md
  S02-goals.md
  S03-actors.md
  S04-glossary.md
  S05-system-overview.md
  S06-product-wide.md
  S07-intro.md
  7.01-F10-extensibility-contract.md
  7.02-F8-configuration.md
  7.03-F2-console-presentation.md
  7.04-F3-plugin-discovery.md
  7.05-F1-interactive-shell-session.md
  7.06-F9-error-handling.md
  7.07-F6-registry-client.md
  7.08-F4-package-search.md
  7.09-F5-package-install.md
  7.10-F11-input-validation-safety.md
  7.11-F7-distribution.md
  S08-data-model.md
  S09-nfr.md
  S10-phasing.md
  S11-open-questions.md
  S12-traceability.md
)

missing=0
for f in "${ORDER[@]}"; do
  if [ ! -s "$SRC/$f" ]; then
    echo "MISSING OR EMPTY: $f" >&2
    missing=1
  fi
done
if [ "$missing" -ne 0 ]; then
  echo "--- refusing to assemble an incomplete PRD ---" >&2
  exit 1
fi

: > "$OUT"
for f in "${ORDER[@]}"; do
  # strip a leading UTF-8 BOM and any trailing blank lines, then separate sections cleanly
  sed '1s/^\xEF\xBB\xBF//' "$SRC/$f" >> "$OUT"
  printf '\n\n' >> "$OUT"
done

# collapse 3+ blank lines to exactly one blank line
awk 'BEGIN{blank=0} /^[[:space:]]*$/{blank++; next} {if(blank>0) print ""; blank=0; print} END{}' "$OUT" > "$OUT.tmp" && mv "$OUT.tmp" "$OUT"

echo "Assembled: $OUT"
wc -l "$OUT"
echo "--- headings ---"
grep -nE '^#{1,3} ' "$OUT" | head -80
