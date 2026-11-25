# Q3 What stands out? (VONR, SELATT; one-liners SERIAL, AESTH)

Emphasis and attention. Severity and evidence rules: SKILL.md. Numbers: only those in sources.md.

### VONR - Von Restorff effect [evidence: R (unconfirmed)]
Claim: when several items look alike, the one that looks different is the one people notice and
  recall; so a different look must be spent on the thing that matters, and on little else.
Source: https://lawsofux.com/von-restorff-effect/ ; primary study named there: Hedwig von
  Restorff, 1933 (the study itself was not fetched; no abstract read).
Applies to: primary call to action, recommended pricing plan, error or status messages, selected
  item in a list, modal footers, dashboards with one key figure.
VONR-1 (code): per view or modal there is exactly one element styled as the primary action
  (one primary-button class, or one filled button among outlined/text ones); zero or two or more
  is a finding. Count the primary-styled buttons in the DOM of each route state.
VONR-2 (rendered 375 and 1440): the emphasised element differs from its neighbours by more than
  hue alone. Check yes/no: in a grayscale or forced-colors render it is still the odd one out
  (size, weight, fill, position or an added mark). Its fill or border against the adjacent
  colour reaches 3:1, computed from getComputedStyle colours, light and dark. This is a WCAG
  SC 1.4.11 failure only when that fill or border is the sole cue that identifies the control
  or its state; a control with visible text is identified by its text, so for it the 3:1 is an
  emphasis heuristic [house heuristic -> max severity minor].
VONR-3 (rendered): the emphasis is not repeated. Count elements in the viewport carrying the
  primary style, a badge, a highlight band or a looping animation; the thing that matters is the
  only one in each group of look-alike siblings (the "recommended" plan, not three of four).
  Looping animation: more than one running in view is a finding. [house heuristic -> max
  severity minor; one moving thing at a time keeps motion from cancelling the contrast]
Does NOT apply / misreading: not "make everything pop"; not a licence for decoration; the
  effect needs a field of similar items to stand out against, so a lone element or a page where
  everything is already different has nothing to apply it to. Distinct is not the same as
  loud: do not use it to hide the quiet, valid alternative (a cancel or back control stays
  findable). Motion and flashing: check the user's reduced-motion preference (outside this skill:
  see the motion skill).
Conflicts: vs SIM -> collision table row "VONR vs SIM": one emphasised element per view,
  everything else consistent.

### SELATT - Selective attention [evidence: R (unconfirmed)]
Claim: people filter the screen down to what serves the current task and drop the rest; content
  that resembles an advert or sits where adverts usually sit gets skipped, and a change that
  happens outside their focus can go unseen.
Source: https://lawsofux.com/selective-attention/ ; primary study named there: Broadbent, 1958
  (the page also names Cherry 1953, Treisman 1960, Deutsch and Deutsch 1963); none of these
  abstracts was fetched.
Applies to: key messages and prices on landing and pricing pages, dashboards, form errors, toasts
  and banners, live-updating lists, carousels.
SELATT-1 (code): content the user must read (price, required step, validation error, consent
  text) is not inside an element with ad-like markup: a class or id with a whole token (split on
  space, hyphen or underscore) equal to banner, ad, ads, advert, promo or sponsor (so "ad-slot"
  matches, while "header", "badge" and "address" do not); a
  third-party ad or promo iframe (payment, sign-in and captcha frames are excluded); or an
  auto-advancing carousel slide. Grep for these on the critical block.
SELATT-2 (code): a change that happens away from the user's focus (inline error, saved
  confirmation, new list rows) is exposed to assistive tech and tied to the place being looked
  at: role="alert" or aria-live on the region, and the message is rendered next to the field or
  control that caused it, not only at the top of the page.
SELATT-3 (rendered 375 and 1440): the one thing the task needs sits in the main content flow,
  not in a full-width strip, sticky bar or side column styled like a banner. Check yes/no with
  elementFromPoint at the task's key spot and the computed position of the container; and check
  that at 375 px a sticky or fixed bar does not cover the field or the error being used.
Does NOT apply / misreading: not a rule to remove banners or promos; it asks that required
  content not borrow their look and place. It does not say attention is a fixed budget you can
  count in items; no number of elements is implied. It does not replace the emphasis rule: use
  VONR to choose what stands out.
Conflicts: vs VONR/SIM -> collision table row "VONR vs SIM" (emphasis is single and the rest
  consistent, so the key content is not drowned by competing highlights).

## Tier B one-liners
SERIAL - people recall the first and last items of a series best, so put the key actions at the
  ends and the least important in the middle - check: in a nav bar or action list whose order is
  free, the most important action is first or last and the least important is neither (yes/no
  per list) - does not apply where order carries meaning (steps, dates, alphabetical, rank) or
  where search is the main way in - evidence R/M (unconfirmed) - https://lawsofux.com/serial-position-effect/ (Ebbinghaus named, no year)
AESTH - a pleasing design is judged more usable and forgiven more, which can hide real problems
  in testing - check: the measured checks in this skill (target size, contrast, feedback) are run
  on the polished page too, and no finding is closed with "it looks fine" without a measurement
  (yes/no per finding) - does not apply as a reason to add decoration: one study measured
  perceived usability, not task performance, and says nothing for or against a plain design -
  evidence M - https://lawsofux.com/aesthetic-usability-effect/ (Kurosu and Kashimura 1995; abstract: https://chi1995.chistatic.hosting.acm.org/proceedings/shortppr/mk_bdy.htm)
