import SwiftUI
import Foundation
import Combine

struct TOEFLExamContainerView: View {
    @StateObject private var session: TOEFLExamSession
    @StateObject private var player = PromptSpeechPlayer()
    @StateObject private var recorder = SpeakingResponseRecorder()
    @State private var showReview = false
    @State private var confirmExit = false
    @State private var confirmFinish = false
    @State private var recordingError = false

    let onComplete: (TOEFLExamResult) -> Void
    let onExit: () -> Void

    init(mode: ExamMode, onComplete: @escaping (TOEFLExamResult) -> Void, onExit: @escaping () -> Void) {
        _session = StateObject(wrappedValue: TOEFLExamSession(mode: mode))
        self.onComplete = onComplete
        self.onExit = onExit
    }

    var body: some View {
        ZStack {
            Group {
                switch session.phase {
                case .directions: directionsView
                case .testing: testingView
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

            if showReview { reviewOverlay }
        }
        .confirmationDialog("Exit this practice session?", isPresented: $confirmExit, titleVisibility: .visible) {
            Button("Exit without saving", role: .destructive) { stopMedia(); onExit() }
        } message: {
            Text("Incomplete answers and recordings will be discarded.")
        }
        .confirmationDialog("Finish this section now?", isPresented: $confirmFinish, titleVisibility: .visible) {
            Button("Finish section") { finishCurrentSection() }
        } message: {
            Text("You answered \(session.answeredCount) of \(session.currentSection.questions.count) items.")
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
                    factCard("Items", "\(session.currentSection.questions.count)", "list.number")
                    factCard("Time", "\(session.currentSection.durationSeconds / 60) min", "timer")
                    factCard("Delivery", session.currentSection.kind.isAdaptive && session.mode == .fullLength ? "2-stage" : "Linear", "arrow.triangle.branch")
                }

                VStack(alignment: .leading, spacing: 13) {
                    Text("Task types").font(.title2.bold())
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

                if session.currentSection.kind.isAdaptive && session.mode == .fullLength {
                    Label("Your router performance selects a lower or upper second module. Once the second module begins, the router is locked.", systemImage: "arrow.triangle.branch")
                        .font(.subheadline).foregroundStyle(TOEFLTheme.navy).toeflCard()
                }
                if session.currentSection.kind == .speaking {
                    Label("Speaking uses your microphone. Recordings are temporary and are not uploaded by this app.", systemImage: "hand.raised.fill")
                        .font(.subheadline).foregroundStyle(TOEFLTheme.navy).toeflCard()
                }

                HStack {
                    Spacer()
                    Button("Begin \(session.currentSection.kind.title)") { session.beginSection() }
                        .buttonStyle(TOEFLPrimaryButtonStyle())
                }
            }
            .frame(maxWidth: 900, alignment: .leading)
            .padding(34)
        }
    }

    private var testingView: some View {
        VStack(spacing: 0) {
            examToolbar
            ProgressView(value: session.progress).tint(TOEFLTheme.aqua)
            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    Text(session.currentQuestion.taskType.directions)
                        .font(.subheadline).foregroundStyle(.secondary)
                    questionBody(session.currentQuestion)
                }
                .frame(maxWidth: 980, alignment: .leading)
                .padding(.horizontal, 34)
                .padding(.vertical, 30)
            }
            Divider()
            examFooter
        }
        .background(TOEFLTheme.canvas)
        .onReceive(Timer.publish(every: 0.5, on: .main, in: .common).autoconnect()) { _ in
            if recorder.isRecording && session.secondsRemaining <= 1 {
                session.updateRecording(duration: recorder.stop())
            }
            session.tick()
        }
    }

    private var examToolbar: some View {
        VStack(spacing: 0) {
            HStack(spacing: 14) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(session.currentSection.displayTitle).font(.headline)
                    HStack(spacing: 7) {
                        Text("Question \(session.currentQuestionIndex + 1) of \(session.currentSection.questions.count)")
                        if let route = session.routeLabel { Text("· \(route)") }
                    }
                    .font(.caption).opacity(0.85)
                }
                Spacer()
                if session.canReview {
                    Button { session.toggleMark() } label: {
                        Label(session.isCurrentMarked ? "Marked" : "Mark", systemImage: session.isCurrentMarked ? "bookmark.fill" : "bookmark")
                    }
                    Button { showReview = true } label: { Label("Review", systemImage: "square.grid.3x3") }
                }
                Button { session.isTimeVisible.toggle() } label: {
                    Label(session.isTimeVisible ? timeString(session.secondsRemaining) : "Show Time", systemImage: "timer")
                        .monospacedDigit()
                }
                Button { confirmExit = true } label: { Image(systemName: "xmark") }
            }
            .buttonStyle(.borderless)
            .foregroundStyle(.white)
            .padding(.horizontal, 24)
            .padding(.vertical, 13)
            .background(TOEFLTheme.header)
        }
    }

    @ViewBuilder
    private func questionBody(_ question: TOEFLQuestion) -> some View {
        if question.isAudioTask { audioPrompt(question) }

        if let passage = question.passage {
            Text(passage)
                .font(question.taskType == .completeWords ? .title3 : .body)
                .lineSpacing(6)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .toeflCard()
        }

        VStack(alignment: .leading, spacing: 18) {
            Text(question.prompt).font(.title2.bold()).foregroundStyle(.primary)

            if !question.choices.isEmpty { choiceList(question) }
            else if question.taskType == .completeWords { completeWordEditor }
            else if question.taskType == .buildSentence { sentenceBuilder(question) }
            else if question.taskType == .writeEmail || question.taskType == .academicDiscussion { writingEditor(question) }
            else if question.taskType == .listenRepeat || question.taskType == .interview { speakingRecorder(question) }

            if let guidance = question.responseGuidance {
                Label(guidance, systemImage: "lightbulb")
                    .font(.footnote).foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func audioPrompt(_ question: TOEFLQuestion) -> some View {
        let canPlay = session.canPlayPrompt(for: question)
        return HStack(spacing: 16) {
            ZStack {
                Circle().fill(TOEFLTheme.blue.opacity(0.13)).frame(width: 52, height: 52)
                Image(systemName: player.isSpeaking ? "waveform" : "headphones").font(.title2).foregroundStyle(TOEFLTheme.blue)
            }
            VStack(alignment: .leading, spacing: 4) {
                Text(player.isSpeaking ? "Playing prompt…" : (canPlay ? "Audio ready" : "Audio completed"))
                    .font(.headline)
                Text(session.mode == .fullLength ? "The prompt can be played once." : "Practice mode allows replay.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            Button(player.isSpeaking ? "Playing" : (canPlay ? "Play" : "Played")) {
                guard let script = question.audioScript else { return }
                session.markPromptPlayed()
                player.speak(script)
            }
            .buttonStyle(TOEFLPrimaryButtonStyle())
            .disabled(player.isSpeaking || !canPlay)
        }
        .toeflCard()
    }

    private func choiceList(_ question: TOEFLQuestion) -> some View {
        VStack(spacing: 11) {
            ForEach(question.choices) { choice in
                Button { session.selectChoice(choice.id) } label: {
                    HStack(alignment: .top, spacing: 13) {
                        ZStack {
                            Circle().stroke(session.currentAnswer.selectedChoiceID == choice.id ? TOEFLTheme.action : Color.secondary, lineWidth: 2).frame(width: 23, height: 23)
                            if session.currentAnswer.selectedChoiceID == choice.id { Circle().fill(TOEFLTheme.action).frame(width: 13, height: 13) }
                        }
                        Text(choice.text).foregroundStyle(.primary).frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .padding(14)
                    .background(session.currentAnswer.selectedChoiceID == choice.id ? TOEFLTheme.blue.opacity(0.09) : TOEFLTheme.surface)
                    .overlay(RoundedRectangle(cornerRadius: 9).stroke(session.currentAnswer.selectedChoiceID == choice.id ? TOEFLTheme.action : TOEFLTheme.border))
                    .clipShape(RoundedRectangle(cornerRadius: 9))
                }
                .buttonStyle(.plain)
            }
        }
    }

    private var completeWordEditor: some View {
        TextField("Type the complete word", text: Binding(
            get: { session.currentAnswer.textEntry },
            set: { session.updateTextEntry($0) }
        ))
        .textFieldStyle(.plain)
        .font(.title3)
        .padding(14)
        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
        .frame(maxWidth: 480)
        #if os(iOS)
        .textInputAutocapitalization(.never)
        .autocorrectionDisabled()
        #endif
    }

    private func sentenceBuilder(_ question: TOEFLQuestion) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("Your sentence").font(.headline)
                Spacer()
                Button("Clear") { session.clearSentence() }.disabled(session.currentAnswer.orderedTokens.isEmpty)
            }
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 120), spacing: 9)], spacing: 9) {
                ForEach(session.currentAnswer.orderedTokens, id: \.self) { token in
                    Button(token) { session.toggleSentenceToken(token) }
                        .buttonStyle(.borderedProminent).tint(TOEFLTheme.action)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 72, alignment: .topLeading)
            .padding(12)
            .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
            Divider()
            Text("Phrase bank").font(.headline)
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 120), spacing: 9)], spacing: 9) {
                ForEach(question.sentenceTokens.filter { !session.currentAnswer.orderedTokens.contains($0) }, id: \.self) { token in
                    Button(token) { session.toggleSentenceToken(token) }.buttonStyle(.bordered)
                }
            }
        }
        .toeflCard()
    }

    private func writingEditor(_ question: TOEFLQuestion) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("Response").font(.headline)
                Spacer()
                Text("\(wordCount(session.currentAnswer.writtenResponse)) words")
                    .font(.caption.monospacedDigit()).foregroundStyle(.secondary)
            }
            TextEditor(text: Binding(
                get: { session.currentAnswer.writtenResponse },
                set: { session.updateWrittenResponse($0) }
            ))
            .font(.body)
            .scrollContentBackground(.hidden)
            .padding(10)
            .frame(minHeight: 260)
            .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
            .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
        }
    }

    private func speakingRecorder(_ question: TOEFLQuestion) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 14) {
                Button {
                    if recorder.isRecording {
                        session.updateRecording(duration: recorder.stop())
                    } else {
                        player.stop()
                        Task {
                            let started = await recorder.start(questionID: question.id)
                            if !started { recordingError = true }
                        }
                    }
                } label: {
                    Label(recorder.isRecording ? "Stop recording" : "Start recording", systemImage: recorder.isRecording ? "stop.circle.fill" : "mic.circle.fill")
                }
                .buttonStyle(TOEFLPrimaryButtonStyle())

                if recorder.isRecording {
                    Label("Recording…", systemImage: "waveform").foregroundStyle(.red).font(.headline)
                } else if session.currentAnswer.recordingDuration > 0 {
                    Label(String(format: "Recorded %.1f sec", session.currentAnswer.recordingDuration), systemImage: "checkmark.circle.fill")
                        .foregroundStyle(TOEFLTheme.teal).font(.headline)
                }
            }

            if session.currentAnswer.recordingDuration > 0 {
                VStack(alignment: .leading, spacing: 8) {
                    Text(question.taskType == .listenRepeat ? "Self-check: accuracy and intelligibility" : "Self-check: relevance, fluency, and language use")
                        .font(.headline)
                    Picker("Rubric self-rating", selection: Binding(
                        get: { session.currentAnswer.speakingSelfRating },
                        set: { session.updateSpeakingSelfRating($0) }
                    )) {
                        Text("Not rated").tag(0)
                        ForEach(1...5, id: \.self) { Text("\($0) / 5").tag($0) }
                    }
                    .pickerStyle(.segmented)
                }
            }
        }
        .toeflCard()
    }

    private var examFooter: some View {
        HStack(spacing: 12) {
            Button("Finish section") { confirmFinish = true }
                .buttonStyle(.borderless).foregroundStyle(.secondary)
            Spacer()
            Button { goBack() } label: { Label("Back", systemImage: "chevron.left") }
                .buttonStyle(TOEFLSecondaryButtonStyle()).disabled(!session.canGoBack || recorder.isRecording)
            Button { advance() } label: {
                Label(session.isLastQuestion ? "End section" : "Next", systemImage: "chevron.right")
            }
            .buttonStyle(TOEFLPrimaryButtonStyle()).disabled(recorder.isRecording)
        }
        .padding(.horizontal, 24)
        .padding(.vertical, 14)
        .background(TOEFLTheme.surface)
    }

    private var reviewOverlay: some View {
        ZStack {
            Color.black.opacity(0.38).ignoresSafeArea().onTapGesture { showReview = false }
            VStack(spacing: 0) {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("Review \(session.currentSection.kind.title)").font(.title2.bold())
                        Text("\(session.answeredCount) answered · \(session.markedCount) marked").font(.caption).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button { showReview = false } label: { Image(systemName: "xmark.circle.fill").font(.title2) }.buttonStyle(.plain)
                }
                .padding(20)
                Divider()
                ScrollView {
                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 78), spacing: 10)], spacing: 10) {
                        ForEach(session.minimumReachableIndex..<session.currentSection.questions.count, id: \.self) { index in
                            let question = session.currentSection.questions[index]
                            Button {
                                session.goToQuestion(index)
                                showReview = false
                            } label: {
                                VStack(spacing: 6) {
                                    Text("\(index + 1)").font(.headline)
                                    Image(systemName: statusIcon(session.status(for: question))).font(.caption)
                                }
                                .frame(maxWidth: .infinity, minHeight: 62)
                            }
                            .buttonStyle(.bordered)
                            .tint(index == session.currentQuestionIndex ? TOEFLTheme.action : statusColor(session.status(for: question)))
                        }
                    }
                    .padding(20)
                }
            }
            .frame(maxWidth: 620, maxHeight: 520)
            .background(TOEFLTheme.surface, in: RoundedRectangle(cornerRadius: 16))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(TOEFLTheme.border))
            .shadow(radius: 28)
            .padding(30)
        }
        .zIndex(10)
    }

    private var taskTypesInCurrentSection: [TOEFLTaskType] {
        var seen: Set<TOEFLTaskType> = []
        return session.currentSection.questions.compactMap { seen.insert($0.taskType).inserted ? $0.taskType : nil }
    }

    private var sectionIntroduction: String {
        switch session.currentSection.kind {
        case .reading: "Read everyday and academic texts, use context, and restore partially missing words."
        case .listening: "Understand responses, conversations, announcements, and academic talks."
        case .writing: "Reconstruct sentences and write for practical and academic purposes."
        case .speaking: "Repeat spoken language accurately and respond naturally in a simulated interview."
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

    private func advance() { stopMedia(); session.goNext() }
    private func goBack() { stopMedia(); session.goBack() }
    private func finishCurrentSection() { stopMedia(); session.finishSection() }
    private func stopMedia() {
        player.stop()
        if recorder.isRecording { session.updateRecording(duration: recorder.stop()) }
    }
    private func timeString(_ seconds: Int) -> String { String(format: "%02d:%02d", seconds / 60, seconds % 60) }
    private func wordCount(_ text: String) -> Int { text.split { $0.isWhitespace || $0.isNewline }.count }
    private func statusIcon(_ status: QuestionProgressStatus) -> String {
        switch status { case .unanswered: "circle"; case .answered: "checkmark.circle.fill"; case .marked: "bookmark.fill" }
    }
    private func statusColor(_ status: QuestionProgressStatus) -> Color {
        switch status { case .unanswered: .secondary; case .answered: TOEFLTheme.teal; case .marked: TOEFLTheme.gold }
    }
}
