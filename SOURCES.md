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

These mostly document formats that predate January 21, 2026. They were used to study academic skill coverage, legacy interface conventions, explanations, rubrics, and vocabulary. The simulator does not label the retired task structure as current.

The original large reference files are not copied into the application bundle or this Git repository.

## Bundled practice content

Practice questions, short passages, listening scripts, sample responses, explanations, and vocabulary examples bundled in the app were written for this simulator. Every review item is labeled as an original practice item aligned to the ETS 2026 blueprint and not as an official ETS question.

The source app icon is user-provided artwork stored at `Design/AppIconSource.png`.
