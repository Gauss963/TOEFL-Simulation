# TOEFL-Simulation

Native SwiftUI TOEFL iBT practice simulator for iPadOS and macOS, built around the test format in effect from January 21, 2026.

## First-version features

- Full-length 2026 sequence: Reading → Listening → Writing → Speaking (about 90 minutes of section time; allow approximately two hours overall with directions and administration)
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
- Searchable starter academic vocabulary bank with Taiwan Traditional Chinese definitions
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

## App icon

`Design/AppIconSource.png` is the supplied artwork. iPadOS receives the square source and applies the system mask. `Tools/generate_mac_app_icon.swift` creates a separate transparent macOS icon with Big Sur-style padding and rounded corners without redrawing the wordmark.

## Trademark

TOEFL® is a registered trademark of ETS. This independent practice simulator is not endorsed or approved by ETS.
