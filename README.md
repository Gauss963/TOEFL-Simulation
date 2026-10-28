# TOEFL-Simulation

Native SwiftUI TOEFL iBT practice simulator for iPadOS and macOS, built around the test format in effect from January 21, 2026.

## Features

- Full-length 2026 sequence: Reading → Listening → Writing → Speaking, with the official item counts on every path (50 / 47 / 12 / 11) and base timing (30 / 29 / 23 / 8 minutes)
- **Two-stage adaptive Reading and Listening** that follows the ETS technical manual: a router module and a lower or upper second module with 35 scored items per path, plus unscored pretest items (15 in Reading, 12 in Listening) placed inside the modules just like the real test
- **Real-test timing and navigation**: Reading Module 1 (21 min) and Module 2 (9 min) with Next/Back inside a module; Listening Module 1 (18 min) and Module 2 (11 min), Next only, each recording played once; Writing task clocks (Build a Sentence 6 min, Email 7 min, Academic Discussion 10 min); Speaking paced automatically with no preparation time
- Task formats matched to the official specifications:
  - **Complete the Words**: 70–100-word academic paragraphs with an intact first sentence and the second half of every second word removed (10 blanks), answered inline
- **Writing Guide**: a sidebar page for Write an Email, Academic Discussion, and Build a Sentence with the task format, a step-by-step structure, fill-in templates, time plans, greetings and sign-offs by relationship, opening lines by purpose, phrase banks (requests, reasons, alternatives, declining, apologizing, complaining, engaging classmates, adding a new reason), a politeness ladder, common mistakes with corrections, pre-submit checklists, and annotated model answers from the bank (each email paragraph labeled by its role and the required point it answers; classmate references and signal phrases highlighted in discussion responses); explanations in Traditional Chinese
- **Speaking Guide**: a sidebar page for Listen and Repeat, Take an Interview, and pronunciation and fluency, drawing on ETS's task descriptions and scoring criteria and on test-prep advice: format and scoring, a meaning-group strategy for repetition, the four-question interview progression, a 45-second answer structure, phrase banks, common mistakes, a personal story bank, pronunciation issues common for Taiwanese learners, and a four-week plan. Model answers for one full interview set are labeled by structure, and practice widgets play real prompts from the bank (sentences shown in meaning groups after you repeat them; interview questions followed by a 45-second countdown)
- **Complete the Words practice**: a separate page for drilling the task one paragraph at a time. Checking a paragraph marks each blank right or wrong, shows the correct letters, and explains every word: part of speech, its meaning in context in Traditional Chinese, and the grammar, context, and spelling clues that lead to the answer. It draws unpracticed paragraphs first (then the weakest), filters by level, tracks accuracy, keeps an answer history in which every attempt can be reopened with your typed answers and the explanations, and keeps a list of missed words (each opens the attempt where it was missed) until you get them right
  - **Read in Daily Life**: notices, menus, web pages, posts, schedules, emails, text chains, ads, news, forms, invoices, and receipts with 2- or 3-question sets
  - **Read an Academic Passage**: ~200-word passages with five questions (factual, vocabulary, inference, purpose/relationship)
  - **Listening**: Listen and Choose a Response, two-speaker conversations (2 questions), 45–85-word announcements (2 questions), and 170–230-word academic talks (4 questions), introduced by a narrator, with speaker visuals
  - **Build a Sentence**: a first speaker's line and a reply built from word chunks, with fixed words and one distractor
  - **Write an Email** (scenario + three tasks) and **Academic Discussion** (professor post + two student posts)
  - **Listen and Repeat**: seven progressively longer sentences in one illustrated scenario, 8–12-second automatic recording windows
  - **Take an Interview**: one interview scenario with four questions progressing from personal to broader issues, 45 seconds each
- **Natural multi-voice audio**: every Listening and Speaking recording is pre-rendered with distinct neural voices (North American and British English, female and male, plus a narrator), fully offline
- **Official-rubric scoring**: Speaking responses are transcribed on this device and scored 0–5 per item (raw 0–55); Email and Academic Discussion are scored 0–5 (Writing raw 0–20 with Build a Sentence); Reading and Listening count scored items only (raw 0–35) with path-aware band conversion
- Volume control, microphone check before Speaking, and per-item answer review with transcripts, rubric points, and unscored-item labels
- 1–6 half-band section scores, overall average rounded to the nearest half band, CEFR display, and the comparable 0–120 transition score
- Searchable 1,375-entry vocabulary bank with manually reviewed Taiwan Traditional Chinese definitions (complete senses, ordered by how often each appears on the 2026 TOEFL iBT) and source filters
- Browse and Flash Cards modes with shuffled 20/50/100/all-card decks or an **Infinite loop** that keeps recycling missed cards until every card is mastered, persistent mastery progress, natural offline neural pronunciation for every headword, and a `V` play/stop shortcut
- Light and dark appearance, responsive split-view design, iPad landscape/portrait support

## Scoring boundary

Reading, Listening, and Build a Sentence are key-scored. For Email and Academic Discussion, a local engine measures the features ETS lists for its writing models (task completion, elaboration, sentence variety, word choice, cohesion, politeness conventions, and spelling) and maps them to the official 0–5 rubric points. Each Speaking response is transcribed with Apple's on-device speech recognizer (audio never leaves the device): Listen and Repeat is scored by word-level accuracy against the sentence following the rubric (exact = 5; meaning kept with minor changes = 4; and so on), and interview answers by relevance, elaboration, speaking rate, pauses, vocabulary, and connectors.

ETS combines proprietary AI scoring engines with certified human raters and equates forms with IRT; those models are not public, so the simulator converts raw scores to bands with a disclosed approximation. Scores are practice estimates, not official TOEFL scores.

## 2026 format basis

| Section | Module / task timing | Items (scored) |
| --- | --- | ---: |
| Reading | Module 1: 21 min · Module 2: 9 min | 50 (35) |
| Listening | Module 1: 18 min · Module 2: 11 min | 47 (35) |
| Writing | Build a Sentence 6 min · Email 7 min · Discussion 10 min | 12 |
| Speaking | Listen and Repeat 7 × 8–12 s · Interview 4 × 45 s | 11 |

Scored counts per module follow the ETS technical manual (Reading router 20 + module 15; Listening router 20 + module 15). ETS does not publish where unscored items appear, so the simulator places them inside the task blocks so that every path matches the published totals for each task type.

See [SOURCES.md](SOURCES.md) for the official standard and authorized reference inventory.

## Download and use

### macOS release

1. Open the [latest GitHub release](https://github.com/Gauss963/TOEFL-Simulation/releases/latest) and download `TOEFL-Simulation-1.2.0-macOS-Universal.app.zip`.
2. Double-click the ZIP file, then move `TOEFL Simulation.app` to the Applications folder.
3. The downloadable build uses an ad-hoc signature and is not Apple-notarized. The first time you launch it, Control-click the app in Finder and choose **Open**. If macOS still blocks it, open **System Settings → Privacy & Security** and choose **Open Anyway**.
4. Allow microphone and speech-recognition access when the Speaking section asks for them. Recordings and transcription stay on the device; recordings use the system temporary directory.

The Universal build supports both Apple Silicon and Intel Macs running macOS 15.7 or later. The release also includes a SHA-256 file so the downloaded ZIP can be verified.

### App workflow

- Choose **Full-length 2026 Practice Test** for the four-section simulation, or run any single section exactly as it appears in the full test.
- Reading and Listening move from Module 1 to Module 2 automatically when you finish Module 1 or its time runs out; your Module 1 performance chooses the lower or upper module.
- Listening recordings play once; take notes while you listen. Speaking runs on its own: listen, wait for the tone, and speak until the timer ends.
- After submission, open the score report to review section bands, adaptive routes, answers, explanations, and the clearly labeled comparable 0–120 estimate.
- Use **Vocabulary** to browse or search the study bank. Switch to **Flash Cards** to create shuffled decks or choose **Infinite loop** mode, where Know it masters a card and Study again brings it back after about 15–40 other cards (about a quarter of the remaining queue, or at the end of a short queue) so recall is tested after a real delay, until the whole pool is mastered. Hear each word in a natural recorded voice (or press `V` to play/stop); mastery progress stays on the device.
- Open **Score History** to revisit saved practice reports and per-question review.

All scores are diagnostic practice estimates, not official TOEFL scores or admissions predictions.

## Build

Requirements:

- Xcode 26.3 or newer
- macOS 15.7+ or iPadOS 26.2+

Open `TOEFL Simulation.xcodeproj` in Xcode and select either **My Mac** or an **iPad Simulator**. Command-line checks used for this release:

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "TOEFL Simulation.xcodeproj" \
  -scheme "TOEFL Simulation" \
  -configuration Release \
  -destination "platform=macOS" \
  CODE_SIGNING_ALLOWED=NO build

DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "TOEFL Simulation.xcodeproj" \
  -scheme "TOEFL Simulation" \
  -configuration Release \
  -destination "generic/platform=iOS Simulator" \
  CODE_SIGNING_ALLOWED=NO build
```

Every push to `main` also runs [Build macOS App](.github/workflows/build-macos-app.yml). The workflow builds an ad-hoc-signed Universal macOS app and uploads the `.app.zip` plus its SHA-256 checksum as a workflow artifact retained for 14 days.

## App icon

`Design/AppIconSource.png` is the supplied artwork. iPadOS receives the square source and applies the system mask. `Tools/generate_mac_app_icon.swift` creates a separate transparent macOS icon with Big Sur-style padding and rounded corners without redrawing the wordmark.

## Vocabulary import

`Tools/import_authorized_vocabulary.py` rebuilds the normalized offline JSON from the three structured 500-word PDFs in the owner's authorized `References` directory. It removes duplicate headwords, repairs OCR whitespace, rejects incomplete cards, and can enrich pronunciation and compact English glosses from the same offline lexical sources used by the GRE simulator. It then applies `Tools/reviewed_vocabulary_glosses.json`, the manually reviewed part of speech, English definition, and Taiwan Chinese senses for every headword, so a re-import keeps those corrections. Chinese senses are separated by `；`, ordered from the most to the least common meaning in 2026 TOEFL iBT academic and campus contexts, and prefixed with `n.`, `v.`, or `adj.` when a word has several parts of speech. The source PDFs remain outside Git and the application bundle.

## Pronunciation generation

`Tools/generate_word_pronunciations.py` pre-renders every headword once with the Apache-2.0 [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) neural voice (`af_heart`), trims silence, encodes 32 kbps mono AAC with macOS `afconvert`, and packs the clips into `WordPronunciations.pack` (about 10 MB) with a byte-range index in `WordPronunciations.json`. Kokoro runs only at build time in a disposable virtual environment (`pip install kokoro soundfile`); the app has no third-party runtime dependency and plays the clips offline with AVFoundation. Words without a bundled clip fall back to the highest-quality installed system voice, and debug launches fail if any headword lacks a clip.

## Question bank and audio generation

`Tools/content2026/` holds the original items written to the ETS 2026 task specifications (academic passages, daily-life texts, talks, conversations, announcements, Build a Sentence items, email tasks, academic discussions, Listen and Repeat scenarios, and interview sets). `Tools/build_toefl_2026_bank.py` turns them into `TOEFL2026Bank.json` (3,936 items with set structure), generates the Complete the Words paragraphs with the official deletion rule (`Tools/ctest_rule.py`, which also checks that each paragraph's study notes match its blanks), and assigns each speaker a distinct voice. `Tools/generate_exam_audio.py` renders every Listening and Speaking recording with Kokoro-82M voices at build time (`--device mps` uses the Apple GPU) and packs them into `ExamAudio.pack` volumes of at most 45 MB.

Each new exam uses one seed throughout the session. Debug validation checks every item's answer key, recording, and Build a Sentence structure, then samples 30 forms on both adaptive paths to confirm 50/47/12/11 items, 35 scored items per adaptive path, and no repeated items.

Practice keeps a local history of the most recent 1,500 item sets you worked through and draws unseen sets first, so items return only after the pool for that task has rotated. Listening draws from 598 Listen and Choose a Response items, 120 conversations, 100 announcements, and 120 academic talks (60 science and engineering and 60 humanities and social science topics among the new talks), so about 20 Listening practices pass before any conversation, announcement, or talk returns.

## Trademark

TOEFL® is a registered trademark of ETS. This independent practice simulator is not endorsed or approved by ETS.
