# Q5 What will users expect? (JAKOB, POSTEL; one-liners MMODEL, ACTIVE)

Collisions: see "When laws collide" in SKILL.md. Sources: references/sources.md section 2.

### JAKOB - Jakob's Law   [evidence: P]
Claim: people carry habits from the other products they use, so a familiar pattern costs them less than a novel one.
Source: https://lawsofux.com/jakobs-law/ ; attributed to Jakob Nielsen, no study or year named on the page (principle, not an empirical finding).
Applies to: header and main navigation, login and signup, search, cart and account entry points, form controls, modals, redesigns of existing screens.
JAKOB-1 (code): the logo or brand element is an `<a>` to the home page; primary navigation is a `<nav>` of real links; controls use native elements (`button`, `input`, `select`) or carry the matching ARIA role, not a div with a click handler.
JAKOB-2 (code): where the brief names a reference product or pattern library, each control in the changed area exists in it (same name for the same action: "Sign in" vs "Log in" is one name, not two); a control with no counterpart is listed in the finding.
JAKOB-3 (rendered, 375 and 1440 px): logo, search, account and cart entry points sit in the same screen region as in the named reference product, and the mobile menu opens from the control the reference uses. Measure by screenshot against the reference; report the region (left, right, top, bottom) for each.
JAKOB-4 (rendered): a redesign of a screen existing users already know either keeps the old placement of its primary actions or ships a visible notice plus a way back to the old version. Yes/no: notice present, old version reachable.
Does NOT apply / misreading: not a ban on novelty; a new pattern is fine when it measurably beats the convention and ships with a label or hint (flag only an unexplained one, at minor). Not a reason to delete items users expect (that is the HICK vs JAKOB row). A one-off product with no peers has no convention to match: mark JAKOB-2 and JAKOB-3 "not applicable".
Conflicts: vs HICK (conventions win: group, do not delete expected items); see also the Modal and Navigation routes in SKILL.md.

### POSTEL - Postel's Law   [evidence: P]
Claim: be generous about the shape of what comes in and strict about what goes out; for a form that means accepting messy formatting and storing one clean form.
Source: https://lawsofux.com/postels-law/ ; Jon Postel's robustness principle for TCP, no year named on the page (an engineering principle, not an empirical finding).
Applies to: phone, email, address, postcode, date, card and IBAN fields, search boxes, import and paste, any text parsed on input.
POSTEL-1 (code): the validator for a phone, card, IBAN or postcode field strips or ignores spaces, dashes, brackets and a leading `+` before testing, instead of a regex that rejects them (an email or address field only trims surrounding whitespace and never drops characters that change its meaning); the stored value is one canonical form (phone: E.164, e.g. +35799123456).
POSTEL-2 (code): server-side validation of the same field exists and does not trust the client check; password, one-time code and other auth fields are NOT normalised or loosened (no trimming of a password, no case-folding of a code).
POSTEL-3 (rendered, 375 and 1440 px): enter "+357 99 123456", "99-123456" and "(99) 123456" in a phone field and submit (use the two national forms only when the product's configured default region is CY; otherwise use three international +357 variants); none shows an error, and the request body or the stored value is the same canonical string. Measure with DevTools network panel or a form-state dump.
POSTEL-4 (rendered): when input is rejected, the message names the expected format with one example, and the user's text is still in the field.
Does NOT apply / misreading: not lenience on security, rules or meaning: a mistyped digit count still fails, and server checks, auth, permissions and payment validation stay strict. Not "accept anything": ambiguous input (a date like 03/04) needs a visible interpretation or a format hint, not a silent guess.
Conflicts: vs security (POSTEL vs security row: lenient on input format only; validation and auth stay strict).

MMODEL - users arrive with their own picture of how a system works; match labels, structure and feedback to it - check: each label for a main action in the changed screen matches a term users or the brief use for it (yes/no per label; list mismatches) - does not apply: a wrong picture found in research is not copied, it is corrected through feedback - evidence P - source https://lawsofux.com/mental-model/ (Craik 1943, a theoretical book).
ACTIVE - people start using software at once and skip manuals, so help must appear where the task is - check: the first screen of a flow offers help in place (hint, tooltip, example) and the primary action works without opening a docs page (yes/no) - does not apply: a high-risk or regulated step may still require reading before acting; contextual help is added, the warning is not removed - evidence P - source https://lawsofux.com/paradox-of-the-active-user/ (Rosson and Carroll 1987, chapter abstract unreachable; citation confirmed by NN/g).
