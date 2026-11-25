# Q1 How many options? (HICK, CHOICE, CHUNK, CLOAD, TESLER)

No target counts anywhere in this file: a count is the outcome of grouping and prioritising, never a goal.
Collisions are resolved in the "When laws collide" table of SKILL.md.

### HICK - Hick's Law   [evidence: M]
Claim: time to pick grows with how many alternatives are offered at one moment, and the growth is
  logarithmic, so adding items costs a small constant step each time the set doubles. Structure and
  defaults buy more than shaving items.
Source: https://lawsofux.com/hicks-law/ ; Hick 1952 (QJEP, choice-reaction experiments), Hick and Hyman 1952 as named on the page.
Applies to: menus, navigation bars, action toolbars, plan or option pickers, command palettes, any set shown at once.
HICK-1 (code): a set of sibling actions or links that the user must scan together is split under
  headings or sub-menus, or the rarely used ones sit in an overflow control (yes/no: is there a
  grouping element, or only one flat list of siblings?).
HICK-2 (code): where one option is the usual choice it is pre-selected or marked "recommended"
  (checked / selected / aria-current attribute present, or a recommended badge in the markup).
  A paid option or add-on is never pre-selected (GUARD-3); mark it "recommended" only.
HICK-3 (rendered, 375 and 1440 px): at each width, the choices visible without interaction form
  labelled groups, not one undifferentiated run. Measure: count visible sibling controls per group
  container via querySelectorAll; a container with no heading, legend or label and with several
  unrelated kinds of action fails. Report the observation, never a numeric limit.
Does NOT apply / misreading: not a rule to delete options or "cut to N"; it is about alternatives
  displayed at once, so a long list that is searched or sorted for a known item is not a HICK
  problem; it does not say time doubles when options double.
Conflicts: vs JAKOB (group, do not remove items users expect); vs FITTS (frequent actions large and
  visible, the rest in overflow). See collision table.

### CHOICE - Choice Overload   [evidence: X]
Claim: a wide assortment can hurt a decision, but whether it does depends on conditions; the
  overall effect across studies is shaky, so treat it as a prompt to help comparison, not a defect.
Source: https://lawsofux.com/choice-overload/ (term credited to Toffler 1970, a book not a study);
  Scheibehenne et al. 2010 (mean effect virtually zero, large variance); Chernev et al. 2015 (effect appears through moderators).
Applies to: pricing tables, product or plan grids, template galleries, filter-less catalogues.
CHOICE-1 (code): a set of comparable items offers a filter, sort, or side-by-side compare control
  rather than only a flat list. [max severity minor: evidence X]
CHOICE-2 (rendered, 375 and 1440 px): when the visible alternatives differ along several
  attributes, the differing attributes line up in a shared row or column layout so they can be
  compared; verify by checking that matching attributes share an x or y position. [max severity minor: evidence X]
Does NOT apply / misreading: not proof that fewer is better; do not cite it to remove plans or
  products; never a blocker or major on its own.
Conflicts: vs JAKOB (expected catalogue conventions stay); vs HICK (grouping beats deletion).

### CHUNK - Chunking / Miller's Law / Working Memory   [evidence: R for limited capacity; "7+-2" outdated]
Claim: people hold only a few meaningful units at once (Cowan: about 4 chunks); grouping raw items
  into labelled units lets them handle more.
Source: https://lawsofux.com/chunking/ , https://lawsofux.com/millers-law/ , https://lawsofux.com/working-memory/ ; Miller 1956; Cowan 2001 (Europe PMC, PMID 11515286).
Applies to: long forms, codes users retype (IBAN, phone, OTP), settings, dense dashboards.
CHUNK-1 (code): strings users read or retype are displayed in groups of 3-4 characters; the stored
  value stays raw. [house heuristic -> max severity minor: chosen for readability of retyped codes, no source gives it]
CHUNK-2 (rendered, 375x667 reference viewport): a form whose fields container is taller than one
  viewport height is split into labelled sections (fieldset/legend or headings). Measure the
  fields container's getBoundingClientRect().height against window.innerHeight, not the whole
  document. [house heuristic -> max severity minor: reference viewport and split point are our choice, no source]
CHUNK-3 (rendered): no step makes the user recall information from an earlier screen; it is shown
  or carried over (NN/g H6, recognition rather than recall). Check: walk the flow and look for a
  step that needs a value last displayed on a previous step.
Does NOT apply / misreading: not a cap on nav items, plan count or menu length (the Miller page
  itself warns against that use); a list scanned for a known item (countries) needs search or sorting, not chunking.
Conflicts: vs HICK (group instead of removing options); vs DOHERTY (each extra step is another round trip).

### CLOAD - Cognitive Load   [evidence: R (unconfirmed)]
Claim: an interface uses mental effort; effort spent on the interface's own clutter (extraneous
  load) is taken from the task itself (intrinsic load), so remove what does not serve the task.
Source: https://lawsofux.com/cognitive-load/ ; Sweller 1988 (no abstract fetched, label carried over unconfirmed).
Applies to: dashboards, tables, dense screens, onboarding, forms mixing instruction and input.
CLOAD-1 (code): each screen has one primary action marked as such (a single primary button
  style per view); decorative elements carry no information role and are marked aria-hidden or are CSS-only.
CLOAD-2 (rendered, 375 and 1440 px): every piece of text or control on the screen maps to the
  current task or a labelled way out; list anything that does not (autoplay media, unrelated
  banners, duplicate controls) with its bounding box.
Does NOT apply / misreading: not "fewer elements always"; essential task content is intrinsic
  load and stays; it gives no numeric element budget. Unconfirmed evidence: report as minor unless a measured task failure exists.
Conflicts: vs TESLER (hide complexity behind defaults, do not delete needed controls).

### TESLER - Tesler's Law   [evidence: P]
Claim: a task has complexity that cannot be designed away; the open question is whether the
  system or the user carries it, and the system should take as much as it can.
Source: https://lawsofux.com/teslers-law/ ; Larry Tesler, mid-1980s, Xerox PARC (a design principle, no study).
Applies to: forms, settings, import and setup flows, checkout, anything asking users for derivable values.
TESLER-1 (code): a field whose value the system can derive or already holds (country from locale,
  city from postcode, name already in the account) is pre-filled or omitted; check the markup for autocomplete tokens and defaults.
TESLER-2 (code): a technical choice the user is asked to make has a default that fits the common case.
TESLER-3 (rendered, 375 and 1440 px): a returning user is not asked again for data the product
  already stored; walk the flow twice and compare the fields shown.
Does NOT apply / misreading: not licence to hide controls people need; complexity moved behind a
  default must stay reachable. Principle only, never evidence-backed major.
Conflicts: vs Occam (hide complexity behind defaults; do not delete needed controls). See collision table.

Frame law (never produces findings): Occam backs TESLER: prefer defaults over deleting needed controls.
