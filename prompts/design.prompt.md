Create `logo.svg`, `DESIGN.md`, and `style-guide.html` in the repository root. `DESIGN.md` owns the 
visual and frontend design language: brand and logo, color palette, typography, layout, component 
conventions, and accessibility targets. `style-guide.html` renders that language as working code.
Read `REQUIREMENTS.md` first and treat it as the sole source of truth for the product, its users, and 
its core workflows. Do not invent features, user types, or brand direction.
Marker convention: `UNKNOWN` for facts the available documents do not provide; `TO BE DECIDED` for 
decisions not yet made. Never invent values for either.
# DESIGN.md
Exactly these sections:
PASTE INTO CLAUDE CODE
# logo.svg
A simple, clean SVG logo: one primary mark, recognizable at small and large sizes, in a small 
deliberate palette, working on light backgrounds at minimum. Plain SVG with no external assets; prefer 
shapes and paths over text, and set any text in a common system font or convert it to paths. Nothing 
copyrighted, trademarked, or confusingly similar to an existing mark. If the product name is UNKNOWN, 
use an abstract or neutral mark and flag the naming gap in `DESIGN.md`.
## Required Design Inputs
Each field exactly once. Fill `Primary audience` from `REQUIREMENTS.md` (or `UNKNOWN` if absent). Fill 
the others only if it explicitly supports them; otherwise keep the default marker shown.- Brand personality: TO BE DECIDED- Primary audience:- Platform targets (web / mobile / both): TO BE DECIDED- Light / dark mode: TO BE DECIDED- Existing brand assets: UNKNOWN
## Brand and Logo
Brand direction (if known), the logo concept and what the mark represents, and usage guidance for 
`logo.svg`: clear space, minimum display size, brief incorrect-usage examples. Mark pending brand 
decisions `TO BE DECIDED`; don't manufacture a strong brand personality the requirements don't 
support.
## Color Palette
Named design tokens with hex values: `primary`, `secondary`, `background`, `surface`, `text`, `error`, 
`success`. Document the colors used in `logo.svg`. All stated text/background combinations MUST meet 
WCAG 2.2 AA contrast (4.5:1 body text, 3:1 large text). Keep the palette restrained.
## Typography
Primary and fallback font families, type scale, and supported weights. Prefer system or open-licensed 
fonts and briefly say why. If brand typography is UNKNOWN, pick a neutral readable default and mark 
the broader typography direction `TO BE DECIDED`.
## Layout and Spacing
Spacing scale, basic grid assumptions, and responsive breakpoints. Implementation-neutral.
## Components
Compact guidance for buttons, inputs, links, focus states, and form feedback/error presentation: 
expected behavior and visual intent only, no UI framework choice. Include only components broadly 
useful for the known product.
## Accessibility
Target conformance `WCAG 2.2 AA`: contrast expectations, visible focus, keyboard accessibility, and 
reduced-motion handling. Concise and behavior-oriented.
## Open Questions
Only unresolved questions that materially affect branding, UX direction, or implementation. Stable 
IDs: `DQ-1`, `DQ-2`, ... (distinct from the `OQ-*` questions in `REQUIREMENTS.md`).
3
STEP THREE
ARCHITECTURE.md
# style-guide.html
One self-contained HTML page — inline CSS, no external assets, no framework — that renders the design 
language for real: the logo, every color token as a labeled swatch with its hex value, the type scale, 
the spacing scale, and working examples of the components above with hover and visible focus states. 
Where `DESIGN.md` says `TO BE DECIDED`, render the provisional default and label it as provisional on 
the page. This page is the reference implementation of the tokens: when a decision later changes 
`DESIGN.md`, it changes this file in the same commit.
# Constraints
No UI framework choices and no speculative features. Keep unknowns visible as `UNKNOWN` and pending 
decisions as `TO BE DECIDED`. Keep all three files compact — frontend work loads them as context.