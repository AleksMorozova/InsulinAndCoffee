# sources.md - applying-ux-laws (step 1 evidence labelling)

Input: facts/all.json and facts/*.md (fetch session 2026-11-23). Nothing here comes from memory.
Status rule: "verified" only if the fetched snippet itself shows the value. If the fetcher's claim or
notes state a value the snippet does not show, the status is "unverified (claim only)".
Retrieval date: the w3c and laws2 fetchers recorded 2026-11-23. The other fetchers did not record a
date, so those rows say "not recorded".

## 1. Numbers used by the plan

| Item | Value | Source URL | Status | Retrieval note |
|---|---|---|---|---|
| NN/g instant limit | 0.1 s | https://www.nngroup.com/articles/response-times-3-important-limits/ | unverified (claim only) | The snippet gives the feedback advice but not the number 0.1. The fetcher's claim says 0.1 s. Date not recorded. |
| NN/g flow limit | 1.0 s | same | verified | The snippet shows "1.0 second". Date not recorded. |
| NN/g attention limit | 10 s | same | verified | The snippet shows "10 seconds". Date not recorded. |
| NN/g progress indicator | over about 1.0 s | https://www.nngroup.com/articles/progress-indicators/ | verified | The snippet shows 1.0 second. Date not recorded. |
| NN/g percent-done indicator | 10 s or more | same | verified | The snippet shows 10 seconds or more. The fetcher's note says looped animation is for 2-10 s; the snippet does not show that. |
| INP good | <=200 ms (poor >500 ms) | https://web.dev/articles/inp | verified | The snippet shows both values. Measured at the 75th percentile (fetcher note). The first fetch disconnected and the retry worked. |
| CLS good | <=0.1 (poor >0.25) | https://web.dev/articles/cls | verified | The snippet shows both values. 75th percentile of page loads (fetcher note). |
| Contrast, normal text (SC 1.4.3) | 4.5:1 | https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html | number verified; level AA unverified (claim only) | The snippet shows 4.5:1 but not the level. Fetched 2026-11-23. |
| Contrast, large text (SC 1.4.3) | 3:1; large = 18 pt or 14 pt bold | same | verified | The snippets show 3:1 and the 18 pt / 14 pt bold definition. |
| Non-text contrast (SC 1.4.11) | 3:1 against adjacent colours | https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html | number verified; level AA unverified (claim only) | The snippet shows 3:1 but not the level. Fetched 2026-11-23. |
| Target size minimum (SC 2.5.8) | 24x24 CSS px | https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html | number verified; level AA unverified (claim only) | The snippet shows 24 by 24 CSS pixels but not the level. Fetched 2026-11-23. |
| 2.5.8 exceptions | 5: Spacing, Equivalent, Inline, User Agent Control, Essential | same | partial | The snippet shows only "Spacing". The full list of five comes from the fetcher's claim. |
| Target size enhanced (SC 2.5.5) | 44x44 CSS px | https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html | number verified; level AAA unverified (claim only) | The snippet shows 44 by 44 CSS pixels but not the level. |
| Apple control size (iOS/iPadOS) | 44x44 pt DEFAULT; 28x28 pt MINIMUM | https://developer.apple.com/design/human-interface-guidelines/accessibility | unverified (claim only) | The snippet only says to strive for the recommended minimum size; neither 44 nor 28 appears in it. The fetcher read the page's Markdown table ("Default control size" and "Minimum control size" columns) and reports 44 as the default and 28 as the minimum. |
| Android/Material target | 48x48 dp | https://support.google.com/accessibility/android/answer/7101858 | verified (Google Android Accessibility Help, NOT a Material page) | The snippet shows at least 48dp. The m3.material.io structure page had no 48dp, and the m3 accessibility-basics page returned 404. No Material page is verified. |
| Doherty threshold | 400 ms (replaced 2,000 ms); 1982 | https://lawsofux.com/doherty-threshold/ | verified (secondary only) | The snippet shows 400 ms. The primary source (Doherty & Thadani 1982, vm.ibm.com) was unreachable because of an SSL failure. Fetched 2026-11-23. |
| Cowan capacity | about 4 chunks | Europe PMC record of PMID 11515286 (Cowan 2001, Behav Brain Sci 24(1)) | verified | The abstract snippet shows a central limit averaging about four chunks. Date not recorded. |
| Miller span (as stated by lawsofux) | 7 +/- 2 | https://lawsofux.com/millers-law/ | verified (secondary) | The page also advises against using 7 to justify limits. The Miller 1956 abstract was unreachable. |
| Working Memory (lawsofux) | 4-7 chunks; fades in 20-30 s | https://lawsofux.com/working-memory/ | unverified (claim only) | The snippet does not show the numbers. It took 3 attempts to fetch. |

House-heuristic numbers (no URL, provenance note instead):
- CHUNK-1, groups of 3-4. House heuristic chosen for readability of retyped codes; no fetched source gives this number. Capped at minor. It is not derived from Cowan's ~4.
- CHUNK-2, a 375x667 reference viewport and a fields container taller than one viewport height. House heuristic for when to split a form; no fetched source. Capped at minor.

## 2. Laws: URL, originating study named on the page, evidence label

The "Origin on page" column is what lawsofux.com names. The "Label basis" column is the abstract that
supports the label. These can differ. A label marked "unconfirmed" carries the plan's label over
because no abstract was fetched; it was not re-graded.

| ID | lawsofux URL | Origin on page | Plan Ev | Proposed | Label basis (one line) |
|---|---|---|---|---|---|
| HICK | https://lawsofux.com/hicks-law/ | Hick and Hyman, 1952 | M | M | Hick 1952 (Crossref abstract): the rate of gain of information is roughly constant, about 5 bits/s, which supports the logarithmic reading. This is a single study, and no replication was fetched. Hyman was not fetched. |
| CHOICE | https://lawsofux.com/choice-overload/ | Alvin Toffler, 1970, Future Shock (a book, not a study) | X | X | Scheibehenne et al. 2010 (OpenAlex abstract): mean effect virtually zero with large variance. Chernev et al. 2015 (Crossref abstract): the effect appears through 4 moderators. |
| CHUNK | https://lawsofux.com/chunking/, /millers-law/, /working-memory/ | Miller 1956; Miller, Galanter & Pribram (1960s); Atkinson & Shiffrin 1968 | R (capacity); "7+-2" outdated | R (capacity) | Cowan 2001 abstract: a real central limit of about 4 chunks; it calls Miller's 7 a rough estimate. Miller's own abstract was unreachable. |
| CLOAD | https://lawsofux.com/cognitive-load/ | John Sweller, 1988 | R | R (unconfirmed) | No abstract was fetched. |
| TESLER | https://lawsofux.com/teslers-law/ | Larry Tesler, mid-1980s, Xerox PARC (no study) | P | P | A design principle with no empirical study named. |
| PROX | https://lawsofux.com/law-of-proximity/ | Gestalt psychologists (no name, no year) | R | R (unconfirmed) | No study was named and no abstract was fetched. |
| SIM | https://lawsofux.com/law-of-similarity/ | Gestalt psychologists (no name, no year) | R | R (unconfirmed) | No study was named and no abstract was fetched. |
| REGION | https://lawsofux.com/law-of-common-region/ | none named | R | R (unconfirmed) | No study was named and no abstract was fetched. |
| CONNECT | https://lawsofux.com/law-of-uniform-connectedness/ | none named | R | R (unconfirmed) | No study was named and no abstract was fetched. |
| PRAGN | https://lawsofux.com/law-of-pr%C3%A4gnanz/ | Max Wertheimer, 1910 | R | R (unconfirmed) | No abstract was fetched. |
| VONR | https://lawsofux.com/von-restorff-effect/ | Hedwig von Restorff, 1933 study | R | R (unconfirmed) | No abstract was fetched. |
| SELATT | https://lawsofux.com/selective-attention/ | Broadbent 1958 (also Cherry 1953, Treisman 1960, Deutsch & Deutsch 1963) | R | R (unconfirmed) | No abstract was fetched. |
| SERIAL | https://lawsofux.com/serial-position-effect/ | Herman Ebbinghaus (no year) | R/M | R/M (unconfirmed) | No abstract was fetched. |
| AESTH | https://lawsofux.com/aesthetic-usability-effect/ | Kurosu & Kashimura, 1995 | M | M | Kurosu & Kashimura 1995 (CHI'95 page): a correlational analysis found that aesthetics drive APPARENT usability more than inherent usability does. It is one study, and it measured perception, not performance. |
| FITTS | https://lawsofux.com/fittss-law/ | Paul Fitts, 1954 | R | R (unconfirmed) | The Fitts 1954 abstract was unreachable; PubMed and Crossref have no abstract field. |
| DOHERTY | https://lawsofux.com/doherty-threshold/ | Doherty & Thadani, IBM Systems Journal, 1982 | M | M (unconfirmed) | The primary IBM page was unreachable (SSL failure), so the label rests only on the secondary page. |
| JAKOB | https://lawsofux.com/jakobs-law/ | Jakob Nielsen (no year, no study) | P | P | A principle with no study named. |
| POSTEL | https://lawsofux.com/postels-law/ | Jon Postel, TCP robustness principle (no year) | P | P | An engineering principle, not an empirical finding. |
| MMODEL | https://lawsofux.com/mental-model/ | Kenneth Craik, 1943, The Nature of Explanation | P | P | A theoretical book, not an experiment. |
| ACTIVE | https://lawsofux.com/paradox-of-the-active-user/ | Rosson & Carroll, 1987 | P | P | The chapter's abstract was unreachable; NN/g confirms the citation only. Kept as a principle. |
| GOAL | https://lawsofux.com/goal-gradient-effect/ | Clark Hull, 1932 (rat experiment 1934) | M | M | Kivetz, Urminsky & Zheng 2006 (Crossref abstract): field evidence that effort accelerates near a reward and that illusory progress speeds purchases. Hull was not fetched. |
| PEAK | https://lawsofux.com/peak-end-rule/ | Kahneman, Fredrickson, Schreiber & Redelmeier, 1993 | M | M | Kahneman et al. 1993 (Crossref abstract): a majority chose to repeat the longer cold-water trial that had a less painful end. It is one lab paradigm. |
| ZEIG | https://lawsofux.com/zeigarnik-effect/ | Bluma Zeigarnik, 1920s (no exact year) | X | X (memory claim not supported) | Ghibellini & Meier, a meta-analysis in Humanities and Social Sciences Communications (nature.com abstract), found no memory advantage for unfinished tasks but a general tendency to resume them. ZEIG checks should rest on resumption, not on memory. The year 2025 is inferred from the URL and not shown on the page. |

Tier C (Occam, Flow, Parkinson, Pareto, Cognitive Bias): not fetched, no label needed.

## 3. Contradictions with the plan (verified sources only)

C1. Zeigarnik, plan section 8.
Quote: "Its finding (memory advantage hard to reproduce, resumption tendency held up)"
Source: https://www.nature.com/articles/s41599-025-05000-w. The abstract reports NO memory advantage for unfinished tasks, which is stronger than "hard to reproduce". The resumption tendency matches the plan.

C2. Scenario 5, plan section 6.
Quote: "3 s blocking spinner on save -> pass: NN/g limits + DOHERTY, determinate or optimistic UI"
Source: https://www.nngroup.com/articles/progress-indicators/. The verified snippet reserves percent-done indicators for actions of 10 s or more. A determinate indicator for a 3 s save goes against that. The fetcher's note puts 2-10 s under looped animation, but the snippet does not show that part.

C3. Measurement method, plan section 5.
Quote: "INP/CLS from Performance panel"
Source: https://web.dev/articles/inp and https://web.dev/articles/cls. Per the fetcher notes, both thresholds are defined at the 75th percentile of field page loads. The numbers agree with the plan; the method differs. One lab Performance-panel trace is not the metric the thresholds are defined on.

C4 (note only). WCAG exceptions, plan section 5.
Quote: "e.g. 2.5.8 spacing / equivalent / inline / essential"
Source: target-size-minimum page (fetcher claim; the snippet shows only Spacing). There are five exceptions, and the plan's list leaves out User Agent Control. "e.g." makes the list non-exhaustive, but the severity rule says to check the listed exceptions.

Superseded by this step, not contradictions: section 8 "whose numbers were NOT read yet" (most are now read); EUR-Lex "NOT verified" (UCPD and DSA text now read); the Zeigarnik note "PROVISIONAL" (the abstract has now been read); "the Material URL tested is a generic structure page" (48dp is now found, but on Google Android Accessibility Help).

## 4. Unresolved

- Apple 44pt (largest impact). The fetcher's table read gives 44x44 pt as the DEFAULT and 28x28 pt as the MINIMUM. The snippet does not show either number. If this is confirmed, the plan's use of 44pt as Apple's bar (the scenario 2 pass criterion and the minor-severity rule) needs a rewrite. Re-fetch the table and quote it.
- WCAG conformance levels (AA for 1.4.3, 1.4.11 and 2.5.8; AAA for 2.5.5) appear in fetcher claims only, not in the snippets.
- NN/g 0.1 s: the claim states it, but the snippet does not show the number.
- Material 48dp: no Material page verified. The only source is Google Android Accessibility Help.
- Doherty 400 ms: secondary source only. The primary IBM 1982 source was unreachable.
- Abstracts unreachable: Miller 1956, Fitts 1954, Doherty & Thadani 1982, Carroll & Rosson 1987.
- Never fetched: McShane & Bockenholt 2018 (cited in plan section 8), Hyman 1953, Hull 1932/1934, Sweller 1988, von Restorff 1933, Broadbent 1958, Wertheimer 1910, Ebbinghaus.
- Labels carried over unconfirmed: CLOAD, PROX, SIM, REGION, CONNECT, PRAGN, VONR, SELATT, SERIAL, FITTS, DOHERTY.
- Zeigarnik paper year (2025) and online date (2025-07-01) are not shown in the fetched page text.
- lawsofux's Working Memory page says 4-7 chunks while Cowan's abstract says about 4. The plan follows Cowan, and the snippet does not show the 4-7 figure.
- GUARD-1 vs Kivetz 2006: illusory progress (bonus stamps) works. Decide whether a pre-filled but honest remaining count is allowed under GUARD-1.
- EU anchors. UCPD blacklist point 7 (a false claim of a very limited time) is verified, but the Annex I heading is not, and restarting timers are not named. Pre-selected paid add-ons are covered by the CRD 2011/83 text before Art. 23, not by the UCPD. That text is only partial, because the Art. 22 heading was not captured, so GUARD-3's "UCPD note" anchor is unconfirmed. DSA Art. 25(1) and recital 67 are verified. The micro/small-enterprise exemption is in a recital, and its article number is unconfirmed (partial).

## Post-labeller verification (read directly from the pages, 2026-11-23)
- WCAG levels confirmed in the page titles: SC 1.4.3 Level AA; SC 1.4.11 Level AA; SC 2.5.8 Level AA; SC 2.5.5 Level AAA.
- NN/g response-times page: "0.1 second is about the limit for having the user feel that the system is reacting instantaneously" (0.1 s confirmed).
- Apple HIG accessibility page: the static HTML contains no target-size numbers (the page is script-rendered). The 44 pt default / 28 pt minimum read by a fetcher is NOT confirmed. Treat "Apple 44pt" as unverified platform guidance.
