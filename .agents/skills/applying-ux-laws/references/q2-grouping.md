# Q2 How to group? (PROX, SIM, REGION, CONNECT, PRAGN)

Evidence label for every law here is R (unconfirmed): sources.md records no abstract for any of them, and the lawsofux pages name no study. Findings that rest on these laws alone stay at the severity the rules in SKILL.md allow; none of the checks below carries a measured threshold. Layout, grid and alignment advice belongs to layout skills: emit a finding here only with its rule ID.

### PROX - Proximity   [evidence: R (unconfirmed)]
Claim: things placed near each other are read as one group, so the gap between related items must be visibly smaller than the gap between unrelated ones.
Source: https://lawsofux.com/law-of-proximity/ ; origin on the page: Gestalt psychologists, no year, no named study.
Applies to: form label/field/hint/error stacks, field groups, button rows, card contents, table and list rows, dashboard tiles, footer link columns.
PROX-1 (code): label, hint and error text of a field share one wrapper (a field container or fieldset) with the input, and the spacing between that wrapper and the next one is set by a different value than the spacing inside it; no single flat margin is applied to every child.
PROX-2 (rendered, 375 and 1440 px): for each label, measure the vertical gap (getBoundingClientRect, label bottom to input top) and the gap from the input bottom to the next label top. Pass only if the first gap is smaller than the second. Repeat at both widths: a stacked layout at 375 px often loses the difference.
PROX-3 (rendered, 375 and 1440 px): a heading or group title is closer to the content it introduces than to the content above it (compare the two gaps).
PROX-4 (rendered): a destructive or cancel action does not sit flush against the primary action unless the two are meant as one pair; if they are one pair, the gap to unrelated controls is larger than the gap inside the pair.
Does NOT apply / misreading: not a rule about absolute pixel gaps or "more whitespace"; only the relation between inner and outer gaps matters. Equal gaps are fine when the items really are one flat list. Page-grid and alignment questions go to a layout skill.
Conflicts: none in the collision table. If a gap fix fights a CHUNK-2 split into sections, the section break is the larger gap and the field rhythm inside it stays smaller.

### SIM - Similarity   [evidence: R (unconfirmed)]
Claim: elements that look alike (colour, shape, size, orientation, motion) are read as related and as having the same function, so equal function gets equal styling and different function gets different styling.
Source: https://lawsofux.com/law-of-similarity/ ; origin on the page: Gestalt psychologists, no year, no named study.
Applies to: buttons and links, nav items, table and list rows, status badges, icon sets, form controls, cards in a grid.
SIM-1 (code): controls with one function share one component or class (all primary actions use the same variant); no one-off inline styles that restyle a single instance of a shared pattern.
SIM-2 (rendered, 375 and 1440 px): list the computed background, colour, border and font of every primary button on the page. Pass only if they match; a different style for the same function is a finding.
SIM-3 (rendered): inline links in body text are distinguishable from surrounding text by something other than colour alone (underline, weight) in computed styles; and non-links do not carry link styling.
SIM-4 (rendered): the same status uses the same colour and label everywhere it appears (for example every "error" badge), and two different statuses are not styled alike.
Does NOT apply / misreading: it does not ask for everything to look the same. A deliberate single emphasised element is allowed (see VONR). Similarity of looks must not suggest a function that is absent, such as a heading styled like a button.
Conflicts: -> VONR vs SIM: one emphasised element per view, everything else consistent.

## Tier B one-liners
REGION - elements inside one border or background panel read as a group - check (code/rendered): each group that uses a container as its grouping method sits inside one container with its own border or background (a group separated clearly by a gap alone passes, see PROX), and no unrelated item sits inside that container - does not apply: do not box every item; nested boxes inside boxes add noise, and a gap alone (PROX) often suffices - evidence R (unconfirmed) - https://lawsofux.com/law-of-common-region/
CONNECT - items joined by a line, frame or shared colour read as related - check (rendered): a step indicator, timeline or linked-items display has a visible joining element between its own items and none to unrelated ones - does not apply: do not add connectors where proximity or a region already groups the items; a line must never join things that are not related - evidence R (unconfirmed) - https://lawsofux.com/law-of-uniform-connectedness/
PRAGN - the eye settles on the simplest reading of a figure - check (rendered, 375 and 1440 px): each icon, logo or chart reads as one simple shape at its displayed size, with no decorative detail that hides the main form - does not apply: it is no licence to strip needed information; simplify decoration, not content - evidence R (unconfirmed) - https://lawsofux.com/law-of-pr%C3%A4gnanz/ (origin on the page: Max Wertheimer, 1910)
