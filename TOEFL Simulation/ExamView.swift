import SwiftUI
import Foundation
import Combine

struct TOEFLExamContainerView: View {
    @StateObject private var session: TOEFLExamSession
    @StateObject private var player = ExamAudioPlayer()
    @StateObject private var recorder = SpeakingResponseRecorder()
    @State private var confirmExit = false
    @State private var confirmPartEnd = false
    @State private var recordingError = false

    let onComplete: (TOEFLExamResult) -> Void
    let onExit: () -> Void

    init(mode: ExamMode, onComplete: @escaping (TOEFLExamResult) -> Void, onExit: @escaping () -> Void) {
        _session = StateObject(wrappedValue: TOEFLExamSession(mode: mode))
        self.onComplete = onComplete
        self.onExit = onExit
    }

    var body: some View {
        Group {
            switch session.phase {
            case .directions: directionsView
            case .testing: testingView
            case .scoring: scoringView
            case .completed:
                if let result = session.result {
                    TOEFLScoreReportView(result: result, heading: "Practice score report", doneTitle: "Save & close") {
                        onComplete(result)
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(TOEFLTheme.canvas.ignoresSafeArea())
        .confirmationDialog("Exit this test?", isPresented: $confirmExit, titleVisibility: .visible) {
            Button("Exit without saving", role: .destructive) { stopMedia(); onExit() }
        } message: {
            Text("Answers and recordings from this session will be discarded.")
        }
        .confirmationDialog(partEndTitle, isPresented: $confirmPartEnd, titleVisibility: .visible) {
            Button(session.isLastPart ? "End section" : "Continue") { stopMedia(); session.advancePart() }
            Button("Return", role: .cancel) {}
        } message: {
            Text("You will not be able to return to this part once you continue.")
        }
        .alert("Microphone unavailable", isPresented: $recordingError) {
            Button("OK", role: .cancel) {}
        } message: {
            Text("Allow microphone access in System Settings to record Speaking responses.")
        }
        .onAppear {
            #if DEBUG
            let arguments = ProcessInfo.processInfo.arguments
            if arguments.contains("-completePractice") {
                session.completeWithSampleAnswersForTesting()
            } else if arguments.contains("-autoBegin"), session.phase == .directions {
                session.beginSection()
            }
            #endif
        }
    }

    private var partEndTitle: String {
        session.isLastPart ? "End the \(session.currentSection.kind.title) section?" : "End \(session.currentPart.title)?"
    }

    // MARK: - Directions

    private var directionsView: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                HStack {
                    Button { confirmExit = true } label: { Label("Exit", systemImage: "xmark") }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                    Spacer()
                    Text(session.mode.title).font(.subheadline).foregroundStyle(.secondary)
                }

                VStack(alignment: .leading, spacing: 10) {
                    Label(session.currentSection.kind.title, systemImage: session.currentSection.kind.icon)
                        .font(.system(size: 38, weight: .bold, design: .rounded))
                        .foregroundStyle(TOEFLTheme.navy)
                    Text(sectionIntroduction).font(.title3).foregroundStyle(.secondary)
                }

                HStack(spacing: 14) {
                    factCard("Items", "\(session.currentSection.kind.officialItemCount)", "list.number")
                    factCard("Time", "\(session.currentSection.kind.officialMinutes) min", "timer")
                    factCard("Structure", structureLabel, "arrow.triangle.branch")
                }

                VStack(alignment: .leading, spacing: 13) {
                    Text("Tasks in this section").font(.title2.bold())
                    ForEach(taskTypesInCurrentSection, id: \.self) { task in
                        HStack(alignment: .top, spacing: 12) {
                            Image(systemName: "checkmark.circle.fill").foregroundStyle(TOEFLTheme.teal).padding(.top, 2)
                            VStack(alignment: .leading, spacing: 3) {
                                Text(task.title).font(.headline)
                                Text(task.directions).font(.subheadline).foregroundStyle(.secondary)
                            }
                        }
                    }
                }
                .toeflCard()

                Label(sectionRules, systemImage: "info.circle.fill")
                    .font(.subheadline).foregroundStyle(TOEFLTheme.navy).toeflCard()

                if session.currentSection.kind == .speaking {
                    MicrophoneCheckView(recorder: recorder)
                }

                HStack {
                    Spacer()
                    Button("Begin \(session.currentSection.kind.title)") { session.beginSection() }
                        .buttonStyle(TOEFLPrimaryButtonStyle())
                        .disabled(recorder.isRecording)
                }
            }
            .frame(maxWidth: 900, alignment: .leading)
            .padding(34)
        }
    }

    private var structureLabel: String {
        switch session.currentSection.kind {
        case .reading, .listening: "2 modules"
        case .writing: "3 timed tasks"
        case .speaking: "Auto-timed"
        }
    }

    private var sectionRules: String {
        switch session.currentSection.kind {
        case .reading:
            "Module 1 has 21 minutes and Module 2 has 9 minutes. Use Next and Back to move within a module. Your Module 1 performance determines Module 2, and you cannot return to Module 1. Some items are unscored."
        case .listening:
            "Module 1 has 18 minutes and Module 2 has 11 minutes; the clock pauses while recordings play. Each recording plays only once. You can take notes. You cannot return to previous questions. Some items are unscored."
        case .writing:
            "Build a Sentence has 6 minutes, the email 7 minutes, and the academic discussion 10 minutes. Each task ends when its clock runs out."
        case .speaking:
            "Each prompt plays once. Speak after the tone; recording starts and stops automatically (8–12 seconds for each repeated sentence, 45 seconds for each interview answer). There is no preparation time."
        }
    }

    // MARK: - Testing

    private var testingView: some View {
        VStack(spacing: 0) {
            examToolbar
            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    questionBody
                }
                .frame(maxWidth: 1080, alignment: .leading)
                .padding(.horizontal, 34)
                .padding(.vertical, 26)
            }
            if session.currentSection.kind != .speaking {
                Divider()
                examFooter
            }
        }
        .background(TOEFLTheme.canvas)
        .onReceive(Timer.publish(every: 0.25, on: .main, in: .common).autoconnect()) { _ in
            session.tick()
        }
    }

    private var examToolbar: some View {
        HStack(spacing: 16) {
            VStack(alignment: .leading, spacing: 2) {
                Text(session.currentSection.displayTitle).font(.headline)
                HStack(spacing: 7) {
                    Text(session.questionLabel)
                    if let route = session.routeLabel { Text("· \(route)") }
                }
                .font(.caption).opacity(0.85)
            }
            Spacer()
            Menu {
                Slider(value: $player.volume, in: 0...1) { Text("Volume") }
                Button("Volume 50%") { player.volume = 0.5 }
                Button("Volume 85%") { player.volume = 0.85 }
                Button("Volume 100%") { player.volume = 1 }
            } label: {
                Label("Volume", systemImage: "speaker.wave.2.fill")
            }
            .menuStyle(.borderlessButton)
            .fixedSize()
            if session.currentSection.kind != .speaking {
                Button { session.isTimeVisible.toggle() } label: {
                    Label(session.isTimeVisible ? timeString(session.secondsRemaining) : "Show Time", systemImage: "timer")
                        .monospacedDigit()
                }
            }
            Button { confirmExit = true } label: { Image(systemName: "xmark") }
                .accessibilityLabel("Exit test")
        }
        .buttonStyle(.borderless)
        .foregroundStyle(.white)
        .padding(.horizontal, 24)
        .padding(.vertical, 13)
        .background(TOEFLTheme.header)
    }

    @ViewBuilder
    private var questionBody: some View {
        let question = session.currentQuestion
        Text(question.taskType.title).font(.caption.bold()).foregroundStyle(TOEFLTheme.blue)
        switch question.taskType {
        case .completeWords:
            Text(question.taskType.directions).font(.headline)
            CompleteTheWordsView(questions: session.currentScreenQuestions, session: session)
        case .readDailyLife, .readAcademicPassage:
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .top, spacing: 22) {
                    readingText(question).frame(minWidth: 440, maxWidth: .infinity, alignment: .topLeading)
                    questionPanel(question).frame(width: 420, alignment: .topLeading)
                }
                VStack(alignment: .leading, spacing: 18) {
                    readingText(question)
                    questionPanel(question)
                }
            }
        case .listenChooseResponse, .listenConversation, .listenAnnouncement, .listenAcademicTalk:
            if session.needsStimulus(question) {
                ListeningStimulusView(question: question, player: player, session: session)
                    .id(question.audioKey)
            } else {
                listeningQuestion(question)
            }
        case .buildSentence:
            BuildSentenceView(question: question, session: session)
        case .writeEmail, .academicDiscussion:
            WritingTaskView(question: question, session: session)
        case .listenRepeat, .interview:
            SpeakingFlowView(question: question, session: session, player: player, recorder: recorder, recordingError: $recordingError)
                .id(question.id)
        }
    }

    private func readingText(_ question: TOEFLQuestion) -> some View {
        Text(question.passage ?? "")
            .font(.body)
            .lineSpacing(6)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
            .toeflCard()
    }

    private func questionPanel(_ question: TOEFLQuestion) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(question.prompt).font(.title3.bold())
            ChoiceListView(question: question, session: session)
        }
    }

    private func listeningQuestion(_ question: TOEFLQuestion) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            if let speakers = question.speakers, question.taskType != .listenChooseResponse {
                SpeakerStrip(speakers: speakers, isSpeaking: false).padding(.bottom, 4)
            }
            Text(question.prompt).font(.title3.bold())
            ChoiceListView(question: question, session: session)
        }
    }

    private var examFooter: some View {
        HStack(spacing: 12) {
            if session.currentSection.kind == .listening {
                Label("You cannot return to previous questions.", systemImage: "arrow.uturn.backward.circle")
                    .font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            if session.currentPart.allowsBack {
                Button { stopMedia(); session.goBack() } label: { Label("Back", systemImage: "chevron.left") }
                    .buttonStyle(TOEFLSecondaryButtonStyle()).disabled(!session.canGoBack)
            }
            Button {
                if session.isLastInPart { confirmPartEnd = true } else { stopMedia(); session.goNext() }
            } label: {
                Label("Next", systemImage: "chevron.right")
            }
            .buttonStyle(TOEFLPrimaryButtonStyle())
            .disabled(session.needsStimulus(session.currentQuestion) || player.isPlaying)
        }
        .padding(.horizontal, 24)
        .padding(.vertical, 14)
        .background(TOEFLTheme.surface)
    }

    private var scoringView: some View {
        VStack(spacing: 18) {
            ProgressView().controlSize(.large)
            Text("Scoring your Speaking responses…").font(.title2.bold())
            Text("Recordings are transcribed on this device and scored with the official 0–5 rubrics. \(session.pendingScores) remaining.")
                .foregroundStyle(.secondary).multilineTextAlignment(.center)
        }
        .padding(40)
        .frame(maxWidth: 560)
    }

    // MARK: - Helpers

    private var taskTypesInCurrentSection: [TOEFLTaskType] {
        var seen: Set<TOEFLTaskType> = []
        return session.currentSection.questions.compactMap { seen.insert($0.taskType).inserted ? $0.taskType : nil }
    }

    private var sectionIntroduction: String {
        switch session.currentSection.kind {
        case .reading: "Read everyday and academic texts, use context, and restore partially missing words."
        case .listening: "Listen to people speaking in campus and academic settings, then answer questions."
        case .writing: "Build sentences, write an email, and contribute to an academic discussion."
        case .speaking: "Repeat what you hear, then answer an interviewer's questions."
        }
    }

    private func factCard(_ title: String, _ value: String, _ icon: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Image(systemName: icon).foregroundStyle(TOEFLTheme.blue)
            Text(value).font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
            Text(title).font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .toeflCard()
    }

    private func stopMedia() {
        player.stop()
        if recorder.isRecording { recorder.stop() }
        session.isClockPaused = false
    }

    private func timeString(_ seconds: Int) -> String { String(format: "%02d:%02d", seconds / 60, seconds % 60) }
}

// MARK: - Multiple choice

private struct ChoiceListView: View {
    let question: TOEFLQuestion
    @ObservedObject var session: TOEFLExamSession

    var body: some View {
        VStack(spacing: 11) {
            ForEach(question.choices) { choice in
                let selected = session.answers[question.id]?.selectedChoiceID == choice.id
                Button { session.selectChoice(choice.id) } label: {
                    HStack(alignment: .top, spacing: 13) {
                        ZStack {
                            Circle().stroke(selected ? TOEFLTheme.action : Color.secondary, lineWidth: 2).frame(width: 23, height: 23)
                            if selected { Circle().fill(TOEFLTheme.action).frame(width: 13, height: 13) }
                        }
                        Text(choice.text).foregroundStyle(.primary).frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .padding(14)
                    .background(selected ? TOEFLTheme.blue.opacity(0.09) : TOEFLTheme.surface)
                    .overlay(RoundedRectangle(cornerRadius: 9).stroke(selected ? TOEFLTheme.action : TOEFLTheme.border))
                    .clipShape(RoundedRectangle(cornerRadius: 9))
                }
                .buttonStyle(.plain)
            }
        }
    }
}

// MARK: - Complete the Words

/// A paragraph whose blanks are written as {kept letters|missing letters}.
struct CTestParagraph {
    enum Piece: Hashable {
        case text(String)
        case blank(index: Int, prefix: String, missing: Int, suffix: String)
    }

    let pieces: [Piece]

    init(_ passage: String) {
        var pieces: [Piece] = []
        var index = 0
        let pattern = try! NSRegularExpression(pattern: "\\{([^|}]*)\\|([^}]*)\\}")
        let ns = passage as NSString
        var cursor = 0
        func appendText(_ text: String) {
            var words = text.split(separator: " ", omittingEmptySubsequences: false).map(String.init)
            // Punctuation written directly after a blank stays attached to it.
            if let first = words.first, !first.isEmpty, case .blank(let i, let p, let m, let s)? = pieces.last {
                pieces[pieces.count - 1] = .blank(index: i, prefix: p, missing: m, suffix: s + first)
                words.removeFirst()
            }
            pieces += words.filter { !$0.isEmpty }.map { .text($0) }
        }
        for match in pattern.matches(in: passage, range: NSRange(location: 0, length: ns.length)) {
            appendText(ns.substring(with: NSRange(location: cursor, length: match.range.location - cursor)))
            let prefix = ns.substring(with: match.range(at: 1))
            let missing = ns.substring(with: match.range(at: 2)).count
            pieces.append(.blank(index: index, prefix: prefix, missing: missing, suffix: ""))
            index += 1
            cursor = match.range.location + match.range.length
        }
        appendText(ns.substring(from: cursor))
        self.pieces = pieces
    }
}

private struct CompleteTheWordsView: View {
    let questions: [TOEFLQuestion]
    @ObservedObject var session: TOEFLExamSession
    @FocusState private var focused: Int?

    var body: some View {
        let paragraph = CTestParagraph(questions.first?.passage ?? "")
        FlowLayout(spacing: 6, lineSpacing: 12) {
            ForEach(Array(paragraph.pieces.enumerated()), id: \.offset) { _, piece in
                switch piece {
                case .text(let word):
                    Text(word).font(.title3)
                case .blank(let index, let prefix, let missing, let suffix):
                    if questions.indices.contains(index) {
                        HStack(spacing: 0) {
                            blank(questions[index], index: index, prefix: prefix, missing: missing)
                            if !suffix.isEmpty { Text(suffix).font(.title3) }
                        }
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .toeflCard()
        .onAppear { focused = 0 }
    }

    private func blank(_ question: TOEFLQuestion, index: Int, prefix: String, missing: Int) -> some View {
        HStack(spacing: 0) {
            Text(prefix).font(.title3)
            TextField(String(repeating: "_", count: missing), text: Binding(
                get: { session.answers[question.id]?.textEntry ?? "" },
                set: { session.updateTextEntry(String($0.filter { $0.isLetter || $0 == "'" }.prefix(missing + 2)), for: question.id) }
            ))
            .textFieldStyle(.plain)
            .font(.title3.monospaced())
            .frame(width: CGFloat(missing) * 15 + 12)
            .padding(.vertical, 2)
            .padding(.horizontal, 3)
            .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 4))
            .overlay(RoundedRectangle(cornerRadius: 4).stroke(TOEFLTheme.action.opacity(0.5)))
            .focused($focused, equals: index)
            .onSubmit { focused = index + 1 < questions.count ? index + 1 : nil }
            #if os(iOS)
            .textInputAutocapitalization(.never)
            .autocorrectionDisabled()
            #endif
        }
        .accessibilityLabel("Blank \(index + 1): \(prefix), \(missing) missing letters")
    }
}

/// Wraps children onto lines like text.
struct FlowLayout: Layout {
    var spacing: CGFloat = 6
    var lineSpacing: CGFloat = 10

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let width = proposal.width ?? 800
        var x: CGFloat = 0, y: CGFloat = 0, lineHeight: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x > 0, x + size.width > width { x = 0; y += lineHeight + lineSpacing; lineHeight = 0 }
            x += size.width + spacing
            lineHeight = max(lineHeight, size.height)
        }
        return CGSize(width: width, height: y + lineHeight)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var x = bounds.minX, y = bounds.minY, lineHeight: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x > bounds.minX, x + size.width > bounds.maxX { x = bounds.minX; y += lineHeight + lineSpacing; lineHeight = 0 }
            subview.place(at: CGPoint(x: x, y: y), proposal: ProposedViewSize(size))
            x += size.width + spacing
            lineHeight = max(lineHeight, size.height)
        }
    }
}

// MARK: - Listening

private struct SpeakerStrip: View {
    let speakers: [TOEFLSpeaker]
    let isSpeaking: Bool

    var body: some View {
        HStack(spacing: 22) {
            ForEach(Array(speakers.enumerated()), id: \.offset) { index, speaker in
                VStack(spacing: 6) {
                    ZStack {
                        Circle().fill(color(index).opacity(0.16)).frame(width: 74, height: 74)
                        Image(systemName: speaker.gender == "female" ? "person.crop.circle.fill" : "person.crop.circle")
                            .font(.system(size: 48))
                            .foregroundStyle(color(index))
                    }
                    Text(speaker.displayName).font(.caption.bold()).foregroundStyle(.secondary)
                }
            }
            if isSpeaking {
                Image(systemName: "waveform").font(.largeTitle).foregroundStyle(TOEFLTheme.action)
                    .symbolEffect(.variableColor.iterative, isActive: true)
            }
        }
    }

    private func color(_ index: Int) -> Color { [TOEFLTheme.blue, TOEFLTheme.teal, TOEFLTheme.gold][index % 3] }
}

private struct ListeningStimulusView: View {
    let question: TOEFLQuestion
    @ObservedObject var player: ExamAudioPlayer
    @ObservedObject var session: TOEFLExamSession

    var body: some View {
        VStack(spacing: 22) {
            if let intro = question.intro {
                Text(intro).font(.title3).foregroundStyle(.secondary).multilineTextAlignment(.center)
            }
            SpeakerStrip(speakers: question.speakers ?? [TOEFLSpeaker(role: "speaker", gender: "female")], isSpeaking: player.isPlaying)
            Text(player.isPlaying ? "Listening…" : "Preparing audio…").font(.headline).foregroundStyle(TOEFLTheme.navy)
            Text("The recording plays once. You may take notes.").font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, minHeight: 320)
        .toeflCard()
        .task {
            session.isClockPaused = true
            let fallback = [question.intro, question.audioScript].compactMap { $0 }.joined(separator: " ")
            await withCheckedContinuation { continuation in
                player.play(key: question.audioKey, fallbackText: fallback) { continuation.resume() }
            }
            session.isClockPaused = false
            session.markStimulusPlayed(question)
        }
    }
}

// MARK: - Build a Sentence

private struct BuildSentenceView: View {
    let question: TOEFLQuestion
    @ObservedObject var session: TOEFLExamSession

    var body: some View {
        let filled = session.answers[question.id]?.orderedTokens ?? []
        VStack(alignment: .leading, spacing: 22) {
            Text(question.taskType.directions).font(.headline)
            if let context = question.context {
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "person.crop.circle.fill").font(.largeTitle).foregroundStyle(TOEFLTheme.blue)
                    Text(context).font(.title3).padding(12)
                        .background(TOEFLTheme.blue.opacity(0.09), in: RoundedRectangle(cornerRadius: 12))
                }
            }
            HStack(alignment: .center, spacing: 12) {
                Image(systemName: "person.crop.circle").font(.largeTitle).foregroundStyle(TOEFLTheme.teal)
                FlowLayout(spacing: 7, lineSpacing: 10) {
                    ForEach(Array(templatePieces.enumerated()), id: \.offset) { _, piece in
                        if let slot = piece.slot {
                            let token = filled.indices.contains(slot) ? filled[slot] : nil
                            Button { if let token { session.toggleSentenceToken(token) } } label: {
                                Text(token ?? " ")
                                    .font(.title3)
                                    .frame(minWidth: 86, minHeight: 34)
                                    .padding(.horizontal, 8)
                                    .background(token == nil ? TOEFLTheme.input : TOEFLTheme.action.opacity(0.14), in: RoundedRectangle(cornerRadius: 6))
                                    .overlay(alignment: .bottom) { Rectangle().fill(TOEFLTheme.action).frame(height: 2) }
                            }
                            .buttonStyle(.plain)
                        } else {
                            Text(piece.text).font(.title3)
                        }
                    }
                }
            }
            .padding(14)
            .background(TOEFLTheme.surface, in: RoundedRectangle(cornerRadius: 12))
            HStack {
                Text("Words").font(.headline)
                Spacer()
                Button("Clear") { session.clearSentence() }.disabled(filled.isEmpty)
            }
            FlowLayout(spacing: 9, lineSpacing: 9) {
                ForEach(question.sentenceTokens.filter { !filled.contains($0) }, id: \.self) { token in
                    Button(token) { session.toggleSentenceToken(token) }
                        .buttonStyle(.bordered)
                        .controlSize(.large)
                }
            }
        }
    }

    private struct Piece { let text: String; let slot: Int? }

    private var templatePieces: [Piece] {
        let parts = (question.sentenceTemplate ?? "").components(separatedBy: "___")
        var pieces: [Piece] = []
        for (index, part) in parts.enumerated() {
            pieces += part.split(separator: " ").map { Piece(text: String($0), slot: nil) }
            if index < parts.count - 1 { pieces.append(Piece(text: "", slot: index)) }
        }
        return pieces
    }
}

// MARK: - Writing

private struct WritingTaskView: View {
    let question: TOEFLQuestion
    @ObservedObject var session: TOEFLExamSession

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(question.taskType.directions).font(.subheadline).foregroundStyle(.secondary)
            if question.taskType == .writeEmail {
                Text(question.prompt).font(.body).lineSpacing(4).toeflCard()
                if let header = question.passage {
                    Text(header).font(.callout.monospaced()).foregroundStyle(.secondary)
                }
            } else {
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "person.crop.circle.badge.checkmark").font(.largeTitle).foregroundStyle(TOEFLTheme.navy)
                    Text(question.prompt).font(.body).lineSpacing(4)
                }
                .toeflCard()
                ForEach(posts, id: \.self) { post in
                    HStack(alignment: .top, spacing: 12) {
                        Image(systemName: "person.crop.circle").font(.title).foregroundStyle(TOEFLTheme.teal)
                        Text(post).font(.callout).lineSpacing(3)
                    }
                    .padding(12)
                    .background(TOEFLTheme.surface, in: RoundedRectangle(cornerRadius: 10))
                }
            }
            HStack {
                Text("Your response").font(.headline)
                Spacer()
                Text("\(wordCount) words").font(.caption.monospacedDigit()).foregroundStyle(.secondary)
            }
            TextEditor(text: Binding(
                get: { session.answers[question.id]?.writtenResponse ?? "" },
                set: { session.updateWrittenResponse($0) }
            ))
            .font(.body)
            .scrollContentBackground(.hidden)
            .padding(10)
            .frame(minHeight: 280)
            .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
            .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
        }
    }

    private var posts: [String] {
        (question.passage ?? "").components(separatedBy: "\n\n").filter { !$0.isEmpty }
    }

    private var wordCount: Int {
        (session.answers[question.id]?.writtenResponse ?? "").split { $0.isWhitespace || $0.isNewline }.count
    }
}

// MARK: - Speaking

private struct SpeakingFlowView: View {
    enum Stage: Equatable { case intro, prompt, tone, recording, saving }

    let question: TOEFLQuestion
    @ObservedObject var session: TOEFLExamSession
    @ObservedObject var player: ExamAudioPlayer
    @ObservedObject var recorder: SpeakingResponseRecorder
    @Binding var recordingError: Bool

    @State private var stage: Stage = .prompt
    @State private var remaining: Double = 0

    private var isFirstInSet: Bool { (question.setIndex ?? 0) == 0 }
    private var total: Double { Double(question.responseSeconds ?? (question.taskType == .interview ? 45 : 10)) }

    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            Text(question.taskType.directions).font(.subheadline).foregroundStyle(.secondary)
            if question.taskType == .listenRepeat {
                repeatIllustration
            } else {
                interviewer
            }
            status
        }
        .task { await run() }
    }

    private var repeatIllustration: some View {
        VStack(alignment: .leading, spacing: 14) {
            if let setting = question.setting {
                Label(setting, systemImage: "map.fill").font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
            }
            if isFirstInSet, let intro = question.intro {
                Text(intro).foregroundStyle(.secondary)
            }
            let stops = question.stops ?? []
            let current = question.setIndex ?? 0
            FlowLayout(spacing: 8, lineSpacing: 8) {
                ForEach(Array(stops.enumerated()), id: \.offset) { index, stop in
                    Label(stop, systemImage: index == current ? "mappin.circle.fill" : (index < current ? "checkmark.circle" : "circle"))
                        .font(.callout.bold())
                        .padding(.horizontal, 12).padding(.vertical, 8)
                        .background(index == current ? TOEFLTheme.action.opacity(0.16) : TOEFLTheme.surface, in: Capsule())
                        .foregroundStyle(index == current ? TOEFLTheme.action : .secondary)
                }
            }
            if let speaker = question.speakers?.first {
                Label(speaker.displayName, systemImage: speaker.gender == "female" ? "person.crop.circle.fill" : "person.crop.circle")
                    .font(.headline).foregroundStyle(TOEFLTheme.teal)
            }
        }
        .toeflCard()
    }

    private var interviewer: some View {
        VStack(alignment: .leading, spacing: 14) {
            if isFirstInSet, let intro = question.intro {
                Text(intro).font(.body).foregroundStyle(.secondary)
            }
            HStack(spacing: 16) {
                ZStack {
                    RoundedRectangle(cornerRadius: 14).fill(TOEFLTheme.header).frame(width: 200, height: 140)
                    Image(systemName: question.speakers?.first?.gender == "female" ? "person.crop.circle.fill" : "person.crop.circle")
                        .font(.system(size: 70)).foregroundStyle(.white.opacity(0.9))
                }
                VStack(alignment: .leading, spacing: 6) {
                    Text(question.speakers?.first?.role.capitalized ?? "Interviewer").font(.title3.bold())
                    Text("Question \((question.setIndex ?? 0) + 1) of 4").foregroundStyle(.secondary)
                }
            }
        }
        .toeflCard()
    }

    private var status: some View {
        HStack(spacing: 18) {
            ZStack {
                Circle().stroke(TOEFLTheme.border, lineWidth: 8).frame(width: 84, height: 84)
                if stage == .recording {
                    Circle().trim(from: 0, to: remaining / max(1, total))
                        .stroke(Color.red, style: StrokeStyle(lineWidth: 8, lineCap: .round))
                        .rotationEffect(.degrees(-90)).frame(width: 84, height: 84)
                    Text("\(Int(ceil(remaining)))").font(.title.bold().monospacedDigit())
                } else {
                    Image(systemName: stage == .saving ? "checkmark" : "ear").font(.title).foregroundStyle(TOEFLTheme.blue)
                }
            }
            VStack(alignment: .leading, spacing: 4) {
                Text(statusTitle).font(.title2.bold()).foregroundStyle(stage == .recording ? .red : TOEFLTheme.navy)
                if stage == .recording {
                    ProgressView(value: Double(recorder.level)).frame(width: 220).tint(TOEFLTheme.teal)
                }
            }
        }
        .toeflCard()
    }

    private var statusTitle: String {
        switch stage {
        case .intro, .prompt: "Listen"
        case .tone: "Get ready to speak"
        case .recording: "Speak now"
        case .saving: "Response saved"
        }
    }

    private func play(_ key: String, _ fallback: String?) async {
        await withCheckedContinuation { continuation in
            player.play(key: key, fallbackText: fallback) { continuation.resume() }
        }
    }

    private func tone(_ data: Data) async {
        await withCheckedContinuation { continuation in
            player.playTone(data) { continuation.resume() }
        }
    }

    private func run() async {
        if isFirstInSet, let setID = question.setID, question.intro != nil {
            stage = .intro
            await play(TOEFLExamAudio.introKey(for: setID), question.intro)
            try? await Task.sleep(for: .milliseconds(600))
        }
        guard !Task.isCancelled else { return }
        stage = .prompt
        await play(question.audioKey, question.audioScript)
        guard !Task.isCancelled else { return }
        stage = .tone
        await tone(TOEFLExamAudio.beep)
        guard !Task.isCancelled else { return }
        let started = await recorder.start(questionID: question.id)
        if !started { recordingError = true }
        stage = .recording
        remaining = total
        let begin = Date()
        while remaining > 0 && !Task.isCancelled {
            try? await Task.sleep(for: .milliseconds(100))
            remaining = max(0, total - Date().timeIntervalSince(begin))
        }
        guard !Task.isCancelled else { return }
        let duration = started ? recorder.stop() : 0
        stage = .saving
        await tone(TOEFLExamAudio.endTone)
        session.recordSpeaking(question: question, duration: duration, url: started ? recorder.lastRecordingURL : nil)
        // Releasing the microphone can make the output device reconfigure (Bluetooth headsets switch
        // back from call mode), which takes about a second; wait so the next prompt is not cut.
        try? await Task.sleep(for: .milliseconds(1_200))
        guard !Task.isCancelled else { return }
        session.goNext()
    }
}

// MARK: - Microphone check

private struct MicrophoneCheckView: View {
    @ObservedObject var recorder: SpeakingResponseRecorder
    @State private var speechReady: Bool?

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Microphone check", systemImage: "mic.fill").font(.headline)
            Text("Press Start, then say: “I live in a city and I like to walk to work.” The bar should reach the green zone.")
                .font(.subheadline).foregroundStyle(.secondary)
            HStack(spacing: 14) {
                Button(recorder.isRecording ? "Stop" : "Start") {
                    if recorder.isRecording {
                        recorder.stop()
                    } else {
                        Task { _ = await recorder.start(questionID: "mic-check") }
                    }
                }
                .buttonStyle(TOEFLSecondaryButtonStyle())
                ProgressView(value: Double(recorder.level))
                    .tint(recorder.level > 0.85 ? .red : (recorder.level > 0.35 ? .green : .orange))
                    .frame(maxWidth: 280)
                Text(recorder.level > 0.85 ? "Too loud" : (recorder.level > 0.35 ? "Good" : "Too quiet"))
                    .font(.caption.bold()).foregroundStyle(.secondary)
            }
            if let speechReady {
                Label(
                    speechReady ? "On-device speech recognition is ready for scoring." : "Speech recognition is unavailable or not allowed, so responses cannot be scored automatically. Allow Speech Recognition in System Settings › Privacy & Security.",
                    systemImage: speechReady ? "checkmark.seal.fill" : "exclamationmark.triangle.fill"
                )
                .font(.caption).foregroundStyle(speechReady ? TOEFLTheme.teal : .orange)
            }
        }
        .toeflCard()
        .task {
            _ = await recorder.requestPermission()
            let authorized = await SpeechTranscriber.requestAuthorization()
            speechReady = authorized && SpeechTranscriber.isOnDeviceAvailable
        }
        .onDisappear { if recorder.isRecording { recorder.stop() } }
    }
}
