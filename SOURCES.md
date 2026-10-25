# Sources and content provenance

## Current TOEFL iBT standard

The simulator's structure, timing, task formats, and scoring were checked again on October 25, 2026 against these ETS sources:

- [TOEFL iBT test content and structure](https://www.ets.org/toefl/test-takers/ibt/about/content.html)
- [TOEFL iBT 2026 Test Blueprint and Specifications](https://www.ets.org/content/dam/ets-india/pdfs/toefl/toefl-ibt-test-specifications-2026.pdf)
- [Understanding TOEFL scores](https://www.ets.org/toefl/test-takers/ibt/scores/understand-scores.html)
- [Reading section](https://www.ets.org/toefl/test-takers/ibt/about/content/reading.html)
- [Listening section](https://www.ets.org/toefl/test-takers/ibt/about/content/listening.html)
- [Writing section](https://www.ets.org/toefl/test-takers/ibt/about/content/writing.html)
- [Speaking section](https://www.ets.org/toefl/test-takers/ibt/about/content/speaking.html)
- [TOEFL iBT 2026 research program](https://www.ets.org/toefl/research/toefl-ibt-updates-2026.html)
- [Updated TOEFL iBT Test Overview](https://www.ets.org/content/dam/ets-org/pdfs/toefl/toefl-ibt-test-overview.pdf) (task formats and scoring rubrics)
- [TOEFL iBT Technical Manual, RR-25-12](https://rr.ets.org/index.php/etsrr/article/view/28/17) (scored items per module, scoring engines)
- [A banded score scale for the TOEFL iBT test (2026)](https://www.eu.ets.org/pdfs/toefl/score-scale-update-2026.pdf)
- [TOEFL iBT Teacher Resources Practice Tests 1 and 2](https://www.eu.ets.org/pdfs/toefl/toefl-ibt-teachers-resources-practice-test-1.pdf) (module layout and on-screen directions)

Key implementation facts:

- Test order: Reading, Listening, Writing, Speaking.
- Reading and Listening are two-stage adaptive; Writing and Speaking are linear.
- Base blueprint: up to 50 Reading, 47 Listening, 12 Writing, and 11 Speaking items.
- Writing contains 10 machine-scored Build a Sentence items and 2 constructed responses.
- Speaking contains 11 constructed responses; the Interview portion has four related questions.
- Scores use a 1–6 scale in half-band increments. The overall score is the four-section average rounded to the nearest half band.
- During the two-year transition through January 2028, official reports also display a comparable 0–120 overall score.

## Authorized local reference library

The project owner supplied the following authorized materials in the workspace `References` directory:

- *The Official Guide to the TOEFL iBT Test*
- *Official TOEFL iBT Tests, Volume 1*
- *Official TOEFL iBT Tests, Volume 2*
- *TOEFL iBT Premium with 8 Online Practice Tests + Online*
- *Princeton Review TOEFL iBT Prep with Audio-Listening Tracks*
- *Essential Words for the TOEFL Tests with Keys*
- *Essential Words for the TOEFL* (authorized edition)
- *TOEFL Idioms Quiz Book*
- *TOEFL Practice Test 2*
- *Collection of TOEFL Reading Comprehension*
- *托福中階500單字*
- *托福進階500單字*
- *高階500英文難字*
- *TOEFL 托福分類字彙*
- *Toefl 詞彙紅寶書*

These mostly document formats that predate January 21, 2026. They were used to study academic skill coverage, legacy interface conventions, explanations, rubrics, and vocabulary. The simulator does not label the retired task structure as current.

The original large reference files are not copied into the application bundle or this Git repository.

## Bundled vocabulary content

The normalized offline vocabulary resource contains 1,348 unique entries extracted from the three structured, user-authorized 500-word PDFs. After merging the original curated academic bank, the runtime library contains 1,375 deduplicated entries. Entries retain the applicable authorized-book source tags and include Taiwan Traditional Chinese meanings and complete English study examples.

Pronunciation and compact English lexical fields are supplemented from the existing offline GRE simulator lexicon. That lexicon includes [Open English WordNet 2025](https://en-word.net/) under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) and the [ECDICT](https://github.com/skywind3000/ECDICT) database under the MIT License. `Tools/import_authorized_vocabulary.py` documents and reproduces the normalization process when the unbundled source files are available.

## Bundled practice content

The runtime bank (`TOEFL2026Bank.json`) contains 3,936 original items organized into the task sets of the January 2026 format: 120 Complete the Words paragraphs (1,200 blanks, each annotated with its part of speech, Taiwan Traditional Chinese meaning in context, and the grammatical, contextual, and spelling clues that lead to the answer), 40 Read in Daily Life sets, 40 Read an Academic Passage sets, 598 Listen and Choose a Response items (each a distinct situation with its own wording, key, and trap distractors; no sentence templates), 120 conversations, 100 announcements, 120 academic talks, 400 Build a Sentence items (each with an English and a Taiwan Traditional Chinese grammar note, covering embedded questions, relative clauses, perfect tenses, passives, conditionals, modal perfects, gerunds and infinitives, causatives, comparatives, reported speech, and more), 100 email tasks (each with a model response), 100 academic discussions across 45 courses (each with a model response), 22 Listen and Repeat scenarios, and 26 interviews.

The items were written for this app to the ETS specifications above (text types and lengths, question counts per set, question types, speaker settings, and timing) and checked with `Tools/content2026` validators for length, set size, answer-key balance, and structure. The authorized reference library informed topic domains and skill coverage; no ETS or published test question is copied or relabeled. Every review item identifies itself as an original practice item, not an official ETS question. Live TOEFL questions are confidential, so the bank contains no recalled or leaked exam content; the Official Resources page links to ETS's own free practice material (TOEFL TestReady and the 2026-format Practice Test 1).

Recordings are synthesized at build time with the Apache-2.0 [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) model using North American and British English voices. ETS also uses Australian (and New Zealand) speakers; the bundled voice set does not include those accents.

The source app icon is user-provided artwork stored at `Design/AppIconSource.png`.
