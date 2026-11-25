# applying-ux-laws

A Claude Code skill that checks a web UI against the 30 Laws of UX (Hick, Fitts, Miller, Jakob, Postel, Doherty, Peak-End and the rest) and turns each law into a checkable rule: code check or rendered check, with a stated "does not apply" case, so it avoids the usual misuse (for example "7 plus or minus 2 menu items").

Use it for navigation, forms and validation, signup and checkout flows, tap targets, slow or empty states, and "rate this UX" requests. It is not for colour palettes, theming or animation.

## Install

Copy this folder to `~/.claude/skills/applying-ux-laws`, or add it through a plugin marketplace that lists it.

## Evidence

Laws and numbers were read from the lawsofux.com pages, WCAG and web.dev; sources are in `references/sources.md` and unverified numbers are marked. Tested with headless Claude Code runs against fixture pages and reviewed by a second model.

Independent project, not affiliated with lawsofux.com. The rule text is written in its own words.

MIT licensed.
