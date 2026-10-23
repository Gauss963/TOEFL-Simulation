# TOEFL-Simulation

Native SwiftUI TOEFL iBT practice simulator for iPadOS and macOS, built around the test format in effect from January 21, 2026.

## Features

- Full-length 2026 sequence: Reading → Listening → Writing → Speaking (about 90 minutes of section time; allow approximately two hours overall with directions and administration)
- 1,968 unique practice questions, including a 1,800-item expanded offline bank
- Seeded, stratified form generation so full tests and section practices draw different items while preserving task quotas
- Official blueprint counts and base timing: 50 / 47 / 12 / 11 items and 30 / 29 / 23 / 8 minutes
- Two-stage adaptive Reading and Listening with router and lower/upper modules
- All current task families:
  - Reading: Complete the Words, Read in Daily Life, Read an Academic Passage
  - Listening: Listen and Choose a Response, Conversation, Announcement, Academic Talk
  - Writing: Build a Sentence, Write an Email, Academic Discussion
  - Speaking: Listen and Repeat, Take an Interview
- One-prompt audio delivery in full-test mode using native speech synthesis
- Local microphone recording for Speaking; recordings stay in the system temporary directory
- Timer, mark/review, section transitions, answer review, and on-device score history
- 1–6 half-band practice scores, CEFR display, and a comparable 0–120 overall estimate
- Searchable 1,375-entry vocabulary bank with manually reviewed Taiwan Traditional Chinese definitions (complete senses, ordered by how often each appears on the 2026 TOEFL iBT) and source filters
- Browse and Flash Cards modes with shuffled 20/50/100/all-card decks or an **Infinite loop** that keeps recycling missed cards until every card is mastered, persistent mastery progress, natural offline neural pronunciation for every headword, and a `V` play/stop shortcut
- Light and dark appearance, responsive split-view design, iPad landscape/portrait support

## Scoring boundary

Reading, Listening, and Build a Sentence are checked against answer keys. Email and Academic Discussion use a disclosed local heuristic based on completion, development, lexical variety, cohesion, and email conventions. Speaking combines recording completion with the learner's 1–5 rubric self-check.

ETS uses secure calibrated test forms and a combination of AI scoring and certified raters for constructed responses. Those models and item parameters are not public. Scores from this app are diagnostic practice estimates, not official TOEFL scores and not admissions predictions.

The overall 1–6 result averages available section bands and rounds to the nearest half band. The comparable 0–120 display uses the midpoint of the ETS transition ranges. A single-section practice report describes only that section.

## 2026 format basis

| Section | Task types | Items | Approx. base time |
| --- | --- | ---: | ---: |
| Reading | Complete the Words; Daily Life; Academic Passage | 50 | 30 min |
| Listening | Response; Conversation; Announcement; Academic Talk | 47 | 29 min |
| Writing | Build a Sentence; Email; Academic Discussion | 12 | 23 min |
| Speaking | Listen and Repeat; Interview | 11 | 8 min |

The official specification describes Reading and Listening as two-stage adaptive and Writing and Speaking as linear. Exact time and item totals may vary on a production form because Reading or Listening can contain unscored pretest items.

See [SOURCES.md](SOURCES.md) for the official standard and authorized reference inventory.

## Download and use

### macOS release

1. Open the [latest GitHub release](https://github.com/Gauss963/TOEFL-Simulation/releases/latest) and download `TOEFL-Simulation-1.1.0-macOS-Universal.app.zip`.
2. Double-click the ZIP file, then move `TOEFL Simulation.app` to the Applications folder.
3. The downloadable build uses an ad-hoc signature and is not Apple-notarized. The first time you launch it, Control-click the app in Finder and choose **Open**. If macOS still blocks it, open **System Settings → Privacy & Security** and choose **Open Anyway**.
4. Allow microphone access when prompted if you want to record Speaking responses. Recordings remain local and use the system temporary directory.

The Universal build supports both Apple Silicon and Intel Macs running macOS 15.7 or later. The release also includes a SHA-256 file so the downloaded ZIP can be verified.

### App workflow

- Choose **Full-length 2026 Practice Test** for the four-section simulation, or select a timed Reading, Listening, Writing, or Speaking practice set.
- Follow the section directions, answer or mark questions, and use Review where that task permits it. Reading and Listening select the second adaptive module after the router is submitted.
- Speaking prompts play once in full-test mode. Record a response and complete the disclosed self-rating used by the local practice estimate.
- After submission, open the score report to review section bands, adaptive routes, answers, explanations, and the clearly labeled comparable 0–120 estimate.
- Use **Vocabulary** to browse or search the study bank. Switch to **Flash Cards** to create shuffled decks or choose **Infinite loop** mode, where Know it masters a card and Study again brings it back a few cards later until the whole pool is mastered. Hear each word in a natural recorded voice (or press `V` to play/stop); mastery progress stays on the device.
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

## Question bank generation

`Tools/generate_expanded_question_bank.py` deterministically rebuilds and validates `ExpandedQuestionBank.json`. The generated resource adds 1,800 original practice items: 720 Reading, 600 Listening, 240 Writing, and 240 Speaking. Its topic taxonomy and skill coverage were informed by the owner-authorized reference library, while its form quotas, stimulus lengths, task names, and adaptive routing follow the current ETS 2026 specification.

The runtime combines those items with the 168 hand-authored core questions for a 1,968-item bank. Each new exam uses one stable seed throughout the session. Reading and Listening draw a router first, then select a non-overlapping lower or upper module after routing; Writing and Speaking draw linear forms. Debug validation samples 40 deterministic forms across both routes and checks counts, task distributions, answer keys, duplicate IDs, duplicate stimuli, sentence-token integrity, and route difficulty boundaries.

## Trademark

TOEFL® is a registered trademark of ETS. This independent practice simulator is not endorsed or approved by ETS.
