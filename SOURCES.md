# Sources and content provenance

## Current TOEFL iBT standard

The simulator's structure and score display were checked on July 20, 2026 against these ETS sources:

- [TOEFL iBT test content and structure](https://www.ets.org/toefl/test-takers/ibt/about/content.html)
- [TOEFL iBT 2026 Test Blueprint and Specifications](https://www.ets.org/content/dam/ets-india/pdfs/toefl/toefl-ibt-test-specifications-2026.pdf)
- [Understanding TOEFL scores](https://www.ets.org/toefl/test-takers/ibt/scores/understand-scores.html)
- [Reading section](https://www.ets.org/toefl/test-takers/ibt/about/content/reading.html)
- [Listening section](https://www.ets.org/toefl/test-takers/ibt/about/content/listening.html)
- [Writing section](https://www.ets.org/toefl/test-takers/ibt/about/content/writing.html)
- [Speaking section](https://www.ets.org/toefl/test-takers/ibt/about/content/speaking.html)
- [TOEFL iBT 2026 research program](https://www.ets.org/toefl/research/toefl-ibt-updates-2026.html)

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

Practice questions, short passages, listening scripts, sample responses, and explanations bundled in the exam simulator were written for this app. Vocabulary cards are either original curated entries or compact normalized records from the user-authorized vocabulary sources listed above. Every exam review item is labeled as an original practice item aligned to the ETS 2026 blueprint and not as an official ETS question.

The source app icon is user-provided artwork stored at `Design/AppIconSource.png`.
