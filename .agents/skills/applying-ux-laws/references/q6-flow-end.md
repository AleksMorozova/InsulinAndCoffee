# Q6 How to carry and end a flow?

Rule IDs: GOAL, PEAK (Tier A); ZEIG (Tier B); frame laws Parkinson, Pareto in the last line.

### GOAL - Goal-Gradient   [evidence: M]
Claim: motivation to finish rises as the finish line gets closer, so a multi-step task should
  show where the person stands and how much is left, and that display has to be truthful.
Source: https://lawsofux.com/goal-gradient-effect; Hull's hypothesis (1932); field evidence in Kivetz,
  Urminsky & Zheng 2006 (J Marketing Research, loyalty-card purchases speed up near the reward).
Applies to: wizards, onboarding, multi-step checkout and forms, profile-completeness meters,
  loyalty and reward cards.
GOAL-1 (code): a flow of more than one step renders its position from real state (current index
  and total derived from the step list or route), not from hard-coded text or a fixed bar width.
GOAL-2 (rendered): at 375 and 1440 px the multi-step flow shows "step n of N" (or equivalent
  labelled bar) on every step, with n and N equal to the real step count; count the steps
  clicked through and compare with the label.
GOAL-3 (rendered): after a reload on step n the indicator still shows step n, not step 1;
  reload mid-flow and read it.
GOAL-4 (rendered): the remaining count or bar on step 1 matches the real remaining work
  (GUARD-1). A head start the user did not earn, shown next to an honest remaining count, is
  an open question (sources.md, GUARD-1 vs Kivetz 2006): note it, do not report it as a finding.
Does NOT apply / misreading: not a licence to inflate or fake progress (illusory progress did
  speed purchases in the study, but GUARD-1 requires the shown remaining work to be real); a single-step
  or instant action needs no indicator; the study is about rewards, so do not assume the same
  size of effect for plain forms (hence evidence M, not R).
Conflicts: vs GUARD-1 (progress shown must match real remaining work; GUARD-1 wins); vs
  DOHERTY in the collision table (CHUNK vs DOHERTY): splitting into steps adds round trips,
  split only where it helps.

### PEAK - Peak-End   [evidence: M]
Claim: people judge an experience mostly by its most intense moment and its last moment, so the
  moment of failure, relief or completion deserves more design care than the average screen.
Source: https://lawsofux.com/peak-end-rule; Kahneman, Fredrickson, Schreiber & Redelmeier 1993
  (Psychological Science, cold-water trials: a longer trial with a milder end was preferred).
Applies to: completion and confirmation screens, error and empty states, first-run moments,
  checkout success, account deletion or cancellation exits.
PEAK-1 (code): every flow's success path ends in a rendered completion state (a page or
  panel naming what happened and what comes next), not `alert()`, `confirm()`, a bare toast
  that disappears, or a redirect to an unrelated page.
PEAK-2 (rendered): trigger the worst realistic failure of the flow (server error, validation
  rejection, offline); the screen says what went wrong, keeps the entered data, and offers one
  next action. Yes/no on each of the three.
PEAK-3 (rendered): at 375 and 1440 px the completion state's next action is visible without
  scrolling past the confirmation text and is a real link or button (check with
  getBoundingClientRect against window.innerHeight).
PEAK-4 (rendered): the final step is not a dead end: after completion a user can leave to
  the product (a link or button exists) without using browser Back.
Does NOT apply / misreading: not a reason to ignore the middle of the flow or to add
  celebration for its own sake; it does not justify confetti on a routine save; one lab
  paradigm, so findings stay at minor unless another rule (blocker or major) also applies.
Conflicts: vs DOHERTY (a delighted ending does not excuse a slow save; feedback limits still
  apply); GUARD-4 (a cheerful ending must not shame a decline or cancellation).

ZEIG - Zeigarnik - show an unfinished task's state and offer a way back in: after leaving mid-flow and returning, a saved draft or "continue" entry exists (yes/no) - not a claim that users remember unfinished items better (a meta-analysis found no memory advantage, only a tendency to resume) - evidence X (cap at minor) - https://lawsofux.com/zeigarnik-effect; Ghibellini & Meier (Humanities and Social Sciences Communications; publication year unconfirmed in the fetched text).

Frame laws, never findings: Parkinson (work expands to the time given) and Pareto (a minority of causes drives most effects) help scope and prioritise a review; do not cite them as a rule.
