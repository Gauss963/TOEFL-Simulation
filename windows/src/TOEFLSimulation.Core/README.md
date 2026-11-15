# TOEFLSimulation.Core — API guide for the Windows UI

Core is the platform-neutral port of the Mac app's logic (everything except drawing and platform services). It targets
`net10.0`, has no Windows APIs, and its behavior is checked against the Mac Swift code by the tests
(`windows/tests/TOEFLSimulation.Core.Tests`, including golden data recorded from the real Swift sources — see
`Golden/Harness/generate.sh`). Everything lives in namespace `TOEFLSimulation.Core` (plus `.Audio`, `.Storage`, `.Json`).

## Startup and shutdown

```csharp
var resources = Path.Combine(AppContext.BaseDirectory, "Resources");                       // copied Mac Resources folder
var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TOEFL Simulation");
using var core = TOEFLCore.Load(resources, data, transcriber: new WhisperTranscriber(), spellChecker: new WindowsSpellChecker());
```

* `TOEFLCore.Load` throws `InvalidDataException`/`IOException` if `TOEFL2026Bank.json` or `AuthorizedTOEFLVocabulary.json`
  is missing or does not decode (show an error and quit). Missing `.pack` files only make audio empty.
* All saved progress is one file, `<data>/settings.json` (`KeyValueStore`), written atomically on every change. Nothing
  needs to be saved on exit; just `Dispose()` the core (closes the audio pack files).
* `core.SelfTest()` is the Mac app's `-selfTest` (bank validation, vocabulary validation, Listening rotation, drill storage);
  an empty list means it passed. Not needed at runtime.

`TOEFLCore` exposes: `QuestionBank`, `StudyBank`, `ExamAudio`, `WordPronunciations`, `History`, `WordsDrill`,
`ReadingDrill`, `Store`, plus factories `CreateExamSession(mode)`, `CreateExamAudioPlayer(output)`,
`CreateWordPronunciationPlayer(output)`, `CreateFlashcardSession(player)`, `CreateWordsDrillSession()`,
`CreateReadingDrillSession()`, `CreateListenRepeatTrainer(player)`, `CreateInterviewTrainer(player)`.

## What the App must implement

| Interface | Purpose | Mac equivalent |
|---|---|---|
| `IAudioOutput` | Play clip bytes (`AudioClipFormat.M4a` = AAC .m4a from the packs, `Wav` = tones), speak fallback text with a US English voice (`rate` uses AVSpeech's scale: 0.5 = normal), `Stop()`, `SetVolume()`. One sound at a time; a new request or `Stop()` ends the previous one with `PlaybackEnd.Stopped`. Return `Failed` if the data cannot be decoded. | AVAudioPlayer / AVSpeechSynthesizer |
| `ISpeakingRecorder` | `StartAsync(questionID)` → record AAC mono to `SpeakingRecording.NewFilePath(tempDir, id)`, return false if the mic is unavailable; `Stop()` returns seconds; `LastRecordingPath`. | SpeakingResponseRecorder |
| `ISpeechTranscriber` | Whisper. Return `null` when recognition is unavailable, `SpeechTranscription.Empty` when no speech; segments = words with start/duration in seconds (used for pauses and rate). `contextualStrings` = target sentence for Listen and Repeat (use as an initial prompt). | SFSpeechRecognizer (on-device) |
| `ISpellChecker` | Count misspelled words, en-US (Windows Spell Checking API). | NSSpellChecker |

Microphone meter: `SpeakingRecording.LevelFromAveragePower(dB)`; mic-check labels/colors: `MicrophoneCheckLabel/Color(level)`.

## Screens → types

| Mac view (Swift) | Core types |
|---|---|
| `ContentView` sidebar | `TOEFLAppPage` (+ `.Title()`, `.Icon()` = SF Symbol name), `TOEFLAppPages.AllCases` |
| `DashboardView` (Practice Tests) | `DashboardContent` (texts, `PracticeModes`, `TaskSummary(kind)`, `BankSummary`), `ExamMode.Title()/Subtitle()`, `TOEFLSectionKind.OfficialItemCount()/OfficialMinutes()/Icon()`, `core.History.Latest` |
| `TOEFLExamContainerView` + `ExamView.swift` | `TOEFLExamSession`, `ExamText`, `CTestParagraph`, `BuildSentence`, `ListeningStimulus`, `SpeakingFlow`, `ExamAudioPlayer` |
| `TOEFLScoreReportView` | `TOEFLExamResult`, `ScoreReportText` |
| `HistoryView` | `TOEFLHistoryStore` (`Results`, `Add`, `Clear`), `ScoreReportText.SectionBandsLine/HistoryCefrLine` |
| `WordsDrillView`, `CheckedParagraphView`, `WordExplanationsView`, `WordsDrillReviewView` | `WordsDrillSession`, `WordsDrillStore`, `WordsDrillParagraph`, `WordExplanation`, `CTestParagraph` |
| `ReadingDrillView`, `ReadingSetView` | `ReadingDrillSession`, `ReadingDrillStore`, `ReadingDrillSet`, `ReadingQuestionType` (`Title()`, `Tip()`) |
| `VocabularyView` (Browse) | `TOEFLStudyBank` (`Words`, `SourceOptions`, `Filter(query, source)`), `VocabularyContent` |
| `TOEFLFlashcardStudyView` | `FlashcardSession`, `FlashcardProgressStore`, `WordPronunciationPlayer` |
| `WritingGuideView` | `WritingGuide` (samples, `LabeledEmailParts`, `ClassmateNames`, `HighlightedDiscussion` → colored spans, `WordCount`) |
| `SpeakingGuideView` + trainers | `SpeakingGuide` (`ModelAnswers`, `MeaningGroups`), `ListenRepeatTrainer`, `InterviewTrainer` |
| `ResourcesView` | `ResourcesContent` |
| `Theme.swift` | `TOEFLThemeColors` (palette values) |

## Practice test (`TOEFLExamSession`)

Create with `core.CreateExamSession(mode)` when the user presses Start. Show by `Phase`:

* **Directions** — `CurrentSection.Kind`, `ExamText.SectionIntroduction/SectionRules/StructureLabel`, `TaskTypesInCurrentSection`
  (with `TOEFLTaskType.Title()/Directions()`), button `ExamText.BeginButtonTitle(kind)` → `BeginSection()`. For Speaking also
  show the microphone check (disable Begin while it records).
* **Testing** — call **`Tick()` every 0.25 s** (DispatcherTimer). Toolbar: `CurrentSection.DisplayTitle`, `QuestionLabel`,
  `RouteLabel`, clock `TextUtil.ClockString(SecondsRemaining)` toggled by `IsTimeVisible` (no clock for Speaking), volume
  menu (`ExamAudioPlayer.Volume`: 0.5 / 0.85 / 1.0). Body by `CurrentQuestion.TaskType`:
  * Complete the Words: one screen per paragraph — `CurrentScreenQuestions`; layout `new CTestParagraph(first.Passage).Pieces`
    (`TextPiece` / `BlankPiece(Index, Prefix, Missing, Suffix)`); on input `UpdateTextEntry(ExamText.SanitizeBlankEntry(text, missing), questionID)`.
  * Reading: passage + `SelectChoice(id)`.
  * Listening: if `NeedsStimulus(q)` show the stimulus screen (`ExamText.StimulusSpeakers`, `StimulusStatus(player.IsPlaying)`)
    and run `await ListeningStimulus.PlayAsync(session, q, player)` once when it appears; then the question + choices.
  * Build a Sentence: `BuildSentence.TemplatePieces(q)` (slots), `SlotText`, `RemainingTokens(q, filled)`;
    actions `AddSentenceToken`, `RemoveSentenceToken(slot)`, `ClearSentence`.
  * Email / Discussion: `UpdateWrittenResponse(text)`, `ExamText.WordCount`, `ExamText.DiscussionPosts(q)`.
  * Speaking: create `new SpeakingFlow(session, q, player, recorder)` per item and `await RunAsync(token)`; bind `Stage`,
    `StatusTitle`, `Remaining`/`Total`, `RecordingFailed` (show "Microphone unavailable"). It advances the item itself.
* Footer (not for Speaking): Back only if `CurrentPart.AllowsBack` (enabled `CanGoBack`) → stop media, `GoBack()`; Next is
  enabled per `ExamText.IsNextEnabled(session, player.IsPlaying)`; if `IsLastInPart` confirm with
  `ExamText.PartEndTitle/PartEndConfirm/PartEndMessage` → stop media, `AdvancePart()`; otherwise stop media, `GoNext()`.
  "Stop media" = `ListeningStimulus.StopMedia(session, player, recorder, isRecording)`.
* Exit (any phase): confirm `ExamText.ExitTitle/ExitConfirm/ExitMessage`, stop media, cancel the Speaking flow, discard the session.
* **Scoring** — Speaking responses are still being transcribed: `ExamText.ScoringTitle`, `ScoringMessage(PendingScores)`.
* **Completed** — show `Result` (score report; heading `ScoreReportText.CompletedHeading`, button "Save & close").
  On close: `core.History.Add(result)` and navigate to Score History.

Binding: state changes raise `PropertyChanged` with an empty name (re-read everything); `Tick` raises only
`SecondsRemaining` unless the part ends; `AnswerChanged` gives the changed item ID. `RecordSpeakingAsync` continues on the
caller's synchronization context, so start flows from the UI thread.

Adaptive routing, module/task clocks (Reading 21 + 9 min; Listening 18 + 7/11 min; Writing 6/7/10 min; Speaking 8 min,
untimed on screen), forfeiting time when a part ends, exposure history, and scoring all happen inside the session.

## Score report and history

`TOEFLExamResult` (`OverallBand`, `Cefr`, `ComparableTotal120`, `Sections`, `Reviews`). Helpers in `ScoreReportText`:
`Band`, `CefrLine`, `RawLine(section)`, `Duration(seconds)`, `ReviewGroups(result)`, `ReviewTitle`, `StatusBadge(isCorrect)`,
`AnswerText`, `RubricLine`, `SampleLine`, `TranscriptLine`, `Methodology`, `Disclaimer`, `OverallExplanation`.
Section card: route title via `AdaptiveRoute.Title()`; tapping toggles `RubricDetail`. Dates: format `CompletedAt` locally.

## Study pages

* **Complete the Words** — `var s = core.CreateWordsDrillSession(); s.Appear();` Bind `Level`, `Subject` (`"all"`, `"weak"`,
  or a `WordsDrillField` raw value; setting either loads a new paragraph), `Paragraph`, `Entries`, `Results`. Actions:
  `SetEntry(i, text, missing)`, `Check()` (Ctrl+Enter), `Next()` (Skip / Next Paragraph), `TryAgain()`, `Load(paragraph)`
  (Practice Again). Header stats: `ParagraphsPracticedText`, `RecentAccuracyText`, `Store.History.Count`,
  `Store.WordsToReview`, `Store.AccuracyByField` (sort by `Accuracy`), `Store.WeakFields`, `WeakAreasText`. History list:
  `VisibleHistory` + `ShowsAllHistory`; word review button opens `Store.LatestMiss(word)`. Explanations: `WordExplanation.For`.
* **Reading Practice** — `core.CreateReadingDrillSession()`, `Appear()`; `TaskFilter`, `Level` (`"all"/"intermediate"/"advanced"`),
  `FocusWeak`; `Choose(i, choiceID)`, `Check()`, `Next()`, `TryAgain()`; `OptionState(...)` for option backgrounds after checking.
* **Vocabulary** — Browse with `core.StudyBank.Filter(query, source)`; Flash Cards with
  `core.CreateFlashcardSession(core.CreateWordPronunciationPlayer(output))`: `Appear()`/`Disappear()`, `Screen`, `CurrentCard`,
  `IsRevealed`, `Reveal()`, `Rate(true/false)`, `StartNewDeck()`, `ReviewMissedCards()`, `ResetProgress()` (after confirming),
  `HandleKey(FlashcardKey.Space/V/LeftArrow/RightArrow)`; settings `Mode` (persisted; changing it restarts the deck), and
  `SelectedSource`, `DeckSize`, `UnmasteredOnly` (as on the Mac, these take effect on the next "New shuffled deck").
* **Writing / Speaking guides** — prose is UI; samples and annotations come from `WritingGuide` / `SpeakingGuide`.
  `InterviewTrainer` needs `Tick()` about every 0.1 s while `Remaining` is not null (it plays the end tone at 0).
  The guides' "Practice the … section" buttons start `ExamMode.WritingPractice` / `SpeakingPractice`.

## Storage keys (same as the Mac UserDefaults keys)

`toefl.practice.results.v1`, `toefl.question-exposure.v1`, `toefl.words-drill.v1`, `toefl.reading-drill.v1`,
`toefl-simulation.flashcards.mastered.v1`, `toefl-simulation.flashcards.mode` (see `StorageKeys`). Values use the Mac's JSON
encoding (dates = seconds since 2001-01-01, UUIDs upper-case, enum raw values), so Mac data can be imported with
`Store.SetRawJson(key, json)`.

## Not ported (and why)

* **Platform services** — audio playback, TTS fallback voice, microphone recording and permission, speech recognition, and
  spell checking are interfaces the App implements (above). Without an `ISpellChecker`, Writing scores count no misspellings
  (scores can be higher than on the Mac for misspelled text); without an `ISpeechTranscriber`, every Speaking response scores
  0 ("Speech recognition was unavailable…"), exactly as on a Mac without Speech permission. Whisper's transcripts and word
  timings differ from Apple's, so Speaking scores for the same recording can differ slightly.
* **Drawing** — layouts, FlowLayout, colors (values are in `TOEFLThemeColors`), animations, SF Symbols (names are provided).
* **Guide prose** — the Writing/Speaking guide explanatory text and phrase tables are UI content in the Swift views.
* **Debug launch flags** — `-renderWordsDrill`, `-renderWritingGuide`, `-renderSpeakingGuide`, `-renderReadingDrill`,
  `-openStudy`, `-startReadingPractice`, `-completePractice`, … are Mac UI-debugging hooks; `-selfTest` is `core.SelfTest()` and
  the tests; `-completePractice` is `TOEFLExamSession.CompleteWithSampleAnswersForTesting()`.
* **User-facing platform wording** — some strings mention "System Settings" (Mac); adjust wording for Windows in the UI.
* **Date formatting** — the Mac uses locale formats (`.abbreviated`, `.shortened`); format `DateTimeOffset` values in the UI.
