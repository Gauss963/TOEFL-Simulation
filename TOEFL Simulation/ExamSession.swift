import Foundation
import Combine

enum TOEFLExamPhase: Equatable {
    case directions
    case testing
    case scoring
    case completed
}

@MainActor
final class TOEFLExamSession: ObservableObject {
    let mode: ExamMode

    @Published private(set) var sections: [TOEFLExamSection]
    @Published private(set) var currentSectionIndex = 0
    @Published private(set) var currentQuestionIndex = 0
    @Published var answers: [String: TOEFLAnswer] = [:]
    @Published private(set) var playedStimuli: Set<String> = []
    @Published private(set) var phase: TOEFLExamPhase = .directions
    @Published private(set) var secondsRemaining = 0
    @Published private(set) var pendingScores = 0
    @Published var isTimeVisible = true
    @Published private(set) var result: TOEFLExamResult?

    /// Set by the view while a recording plays, so listening time is not spent on audio.
    var isClockPaused = false

    private let startedAt = Date()
    private let formSeed: UInt64
    private var partRemaining: Double = 0
    private var lastTick: Date?
    private var summaries: [TOEFLSectionSummary] = []
    private var awaitingSectionScores = false

    init(mode: ExamMode) {
        let formSeed = UInt64.random(in: UInt64.min...UInt64.max)
        self.mode = mode
        self.formSeed = formSeed
        if mode == .fullLength {
            self.sections = TOEFLQuestionBank.fullSections(seed: formSeed)
        } else if let kind = mode.sectionKinds.first {
            self.sections = [TOEFLQuestionBank.practiceSection(for: kind, seed: formSeed)]
        } else {
            self.sections = []
        }
    }

    // MARK: - Position

    var currentSection: TOEFLExamSection { sections[currentSectionIndex] }
    var currentQuestion: TOEFLQuestion { currentSection.questions[currentQuestionIndex] }
    var currentAnswer: TOEFLAnswer { answers[currentQuestion.id] ?? TOEFLAnswer() }

    var currentPart: TOEFLTimedPart {
        currentSection.timedParts.first { $0.range.contains(currentQuestionIndex) } ?? currentSection.timedParts[0]
    }

    /// Complete the Words paragraphs are answered on one screen; every other item has its own screen.
    func screenRange(containing index: Int) -> Range<Int> {
        let questions = currentSection.questions
        let question = questions[index]
        guard question.taskType == .completeWords, let setID = question.setID else { return index..<(index + 1) }
        var lower = index, upper = index + 1
        while lower > 0, questions[lower - 1].setID == setID { lower -= 1 }
        while upper < questions.count, questions[upper].setID == setID { upper += 1 }
        return lower..<upper
    }

    var currentScreen: Range<Int> { screenRange(containing: currentQuestionIndex) }
    var currentScreenQuestions: [TOEFLQuestion] { Array(currentSection.questions[currentScreen]) }

    var questionLabel: String {
        let screen = currentScreen
        let total = currentSection.questions.count
        return screen.count > 1
            ? "Questions \(screen.lowerBound + 1)–\(screen.upperBound) of \(total)"
            : "Question \(screen.lowerBound + 1) of \(total)"
    }

    var canGoBack: Bool {
        let part = currentPart
        return part.allowsBack && currentScreen.lowerBound > part.range.lowerBound
    }

    var isLastInPart: Bool { currentScreen.upperBound >= currentPart.range.upperBound }

    var isLastPart: Bool { currentSection.timedParts.last == currentPart }

    var routeLabel: String? {
        guard currentSection.kind.isAdaptive else { return nil }
        return currentPart.title
    }

    var answeredCount: Int { currentSection.questions.filter { !(answers[$0.id] ?? TOEFLAnswer()).isEmpty }.count }

    // MARK: - Flow

    func beginSection() {
        currentQuestionIndex = 0
        startPart(currentSection.timedParts[0])
        phase = .testing
    }

    private func startPart(_ part: TOEFLTimedPart) {
        currentQuestionIndex = part.range.lowerBound
        partRemaining = Double(part.seconds)
        secondsRemaining = part.seconds
        lastTick = Date()
    }

    func tick() {
        guard phase == .testing else { return }
        let now = Date()
        let delta = lastTick.map { now.timeIntervalSince($0) } ?? 0
        lastTick = now
        guard !isClockPaused, currentSection.kind != .speaking else { return }
        partRemaining = max(0, partRemaining - delta)
        secondsRemaining = Int(ceil(partRemaining))
        if partRemaining <= 0 { advancePart() }
    }

    func goNext() {
        if isLastInPart {
            advancePart()
        } else {
            currentQuestionIndex = currentScreen.upperBound
        }
    }

    func goBack() {
        guard canGoBack else { return }
        currentQuestionIndex = screenRange(containing: currentScreen.lowerBound - 1).lowerBound
    }

    /// Ends the current module or task; time left on its clock is forfeited, as in the real test.
    func advancePart() {
        let parts = currentSection.timedParts
        let part = currentPart
        if currentSection.kind.isAdaptive, currentSection.route == nil, part == parts.first {
            configureAdaptiveModule()
        }
        let refreshed = currentSection.timedParts
        if let index = refreshed.firstIndex(where: { $0.range.lowerBound == part.range.lowerBound }), index + 1 < refreshed.count {
            startPart(refreshed[index + 1])
        } else {
            finishSection()
        }
    }

    // MARK: - Answers

    func selectChoice(_ choiceID: String) {
        var answer = currentAnswer
        answer.selectedChoiceID = choiceID
        answers[currentQuestion.id] = answer
    }

    func updateTextEntry(_ value: String, for questionID: String) {
        var answer = answers[questionID] ?? TOEFLAnswer()
        answer.textEntry = value
        answers[questionID] = answer
    }

    func toggleSentenceToken(_ token: String) {
        var answer = currentAnswer
        if let index = answer.orderedTokens.firstIndex(of: token) {
            answer.orderedTokens.remove(at: index)
        } else if answer.orderedTokens.count < currentQuestion.correctSentenceTokens.count {
            answer.orderedTokens.append(token)
        }
        answers[currentQuestion.id] = answer
    }

    func clearSentence() {
        var answer = currentAnswer
        answer.orderedTokens = []
        answers[currentQuestion.id] = answer
    }

    func updateWrittenResponse(_ value: String) {
        var answer = currentAnswer
        answer.writtenResponse = value
        answers[currentQuestion.id] = answer
    }

    // MARK: - Listening

    func needsStimulus(_ question: TOEFLQuestion) -> Bool {
        question.taskType.isListeningStimulus
            && (question.setIndex ?? 0) == 0
            && !playedStimuli.contains(question.audioKey)
    }

    func markStimulusPlayed(_ question: TOEFLQuestion) {
        playedStimuli.insert(question.audioKey)
    }

    // MARK: - Speaking

    /// Stores a finished recording and scores it on-device in the background.
    func recordSpeaking(question: TOEFLQuestion, duration: Double, url: URL?) {
        var answer = answers[question.id] ?? TOEFLAnswer()
        answer.recordingDuration = duration
        answers[question.id] = answer
        pendingScores += 1
        Task {
            let transcription: SpeechTranscription? = if let url { await SpeechTranscriber.transcribe(url, contextualStrings: question.taskType == .listenRepeat ? [question.audioScript ?? ""] : []) } else { nil }
            let scored: (Int, String) = question.taskType == .listenRepeat
                ? SpeakingRubric.scoreRepeat(target: question.audioScript ?? "", transcription: transcription)
                : SpeakingRubric.scoreInterview(question: question.audioScript ?? "", transcription: transcription, responseSeconds: question.responseSeconds ?? 45)
            var updated = self.answers[question.id] ?? TOEFLAnswer()
            updated.transcript = transcription?.text
            updated.rubricScore = scored.0
            updated.rubricNote = scored.1
            self.answers[question.id] = updated
            self.pendingScores -= 1
            if self.awaitingSectionScores, self.pendingScores == 0 {
                self.awaitingSectionScores = false
                self.summarizeSection()
            }
        }
    }

    // MARK: - Scoring

    func finishSection() {
        guard phase == .testing else { return }
        lastTick = nil
        if currentSection.routerCount != nil && currentSection.route == nil {
            configureAdaptiveModule()
        }
        if currentSection.kind == .speaking, pendingScores > 0 {
            awaitingSectionScores = true
            phase = .scoring
            return
        }
        summarizeSection()
    }

    private func summarizeSection() {
        let section = currentSection
        if section.kind == .writing {
            for question in section.questions where question.taskType == .writeEmail || question.taskType == .academicDiscussion {
                var answer = answers[question.id] ?? TOEFLAnswer()
                let scored = WritingRubric.score(question: question, response: answer.writtenResponse)
                answer.rubricScore = scored.0
                answer.rubricNote = scored.1
                answers[question.id] = answer
            }
        }
        let scoredQuestions = section.questions.filter(\.countsTowardScore)
        let objective = scoredQuestions.compactMap { question -> Bool? in
            TOEFLAnswerMatcher.isCorrect(question: question, answer: answers[question.id] ?? TOEFLAnswer())
        }
        let correct = objective.filter { $0 }.count
        let answered = section.questions.filter { !(answers[$0.id] ?? TOEFLAnswer()).isEmpty }.count
        let band: Double
        let detail: String
        let raw: Int
        switch section.kind {
        case .reading, .listening:
            band = TOEFLPracticeScoring.objectiveBand(correct: correct, total: objective.count, route: section.route)
            raw = correct
            let pretest = section.questions.count - scoredQuestions.count
            detail = "Raw \(correct)/\(objective.count) scored items · \(section.route?.title ?? "Linear") · \(pretest) unscored pretest items not counted."
        case .writing:
            (band, detail, raw) = TOEFLPracticeScoring.writingBand(questions: section.questions, answers: answers)
        case .speaking:
            (band, detail, raw) = TOEFLPracticeScoring.speakingBand(questions: section.questions, answers: answers)
        }
        summaries.append(TOEFLSectionSummary(
            kind: section.kind,
            correct: correct,
            objectiveTotal: objective.count,
            answered: answered,
            total: section.questions.count,
            route: section.route,
            band: band,
            rubricDetail: detail,
            rawScore: raw,
            rawMaximum: section.kind.rawMaximum
        ))

        if currentSectionIndex + 1 < sections.count {
            currentSectionIndex += 1
            currentQuestionIndex = 0
            phase = .directions
        } else {
            completeExam()
        }
    }

    private func configureAdaptiveModule() {
        guard let routerCount = currentSection.routerCount else { return }
        let routerQuestions = Array(currentSection.questions.prefix(routerCount)).filter(\.countsTowardScore)
        let objective = routerQuestions.compactMap { question in
            TOEFLAnswerMatcher.isCorrect(question: question, answer: answers[question.id] ?? TOEFLAnswer())
        }
        let accuracy = objective.isEmpty ? 0 : Double(objective.filter { $0 }.count) / Double(objective.count)
        let route: AdaptiveRoute = accuracy >= 0.65 ? .upper : .lower
        let router = Array(currentSection.questions.prefix(routerCount))
        let module = TOEFLQuestionBank.moduleQuestions(
            for: currentSection.kind,
            route: route,
            seed: formSeed,
            excluding: Set(router.map(\.id))
        )
        sections[currentSectionIndex].questions = router + module
        sections[currentSectionIndex].route = route
    }

    private func completeExam() {
        phase = .completed
        let overall = TOEFLPracticeScoring.overall(summaries.map(\.band))
        let reviews = sections.flatMap { section in
            section.questions.enumerated().map { index, question in
                TOEFLQuestionReview(
                    number: index + 1,
                    question: question,
                    answer: answers[question.id] ?? TOEFLAnswer(),
                    isCorrect: TOEFLAnswerMatcher.isCorrect(question: question, answer: answers[question.id] ?? TOEFLAnswer())
                )
            }
        }
        result = TOEFLExamResult(
            id: UUID(),
            completedAt: Date(),
            mode: mode,
            overallBand: overall,
            comparableTotal120: TOEFLPracticeScoring.comparable120(for: overall),
            cefr: TOEFLPracticeScoring.cefr(for: overall),
            elapsedSeconds: max(0, Int(Date().timeIntervalSince(startedAt))),
            sections: summaries,
            reviews: reviews
        )
    }

    #if DEBUG
    func completeWithSampleAnswersForTesting() {
        var guardCount = 0
        while phase != .completed && guardCount < 10 {
            guardCount += 1
            if phase == .directions { beginSection() }
            if currentSection.routerCount != nil && currentSection.route == nil {
                for question in currentSection.questions.prefix(currentSection.routerCount ?? 0) { answers[question.id] = sampleAnswer(question) }
                configureAdaptiveModule()
            }
            for question in currentSection.questions {
                answers[question.id] = sampleAnswer(question)
            }
            finishSection()
        }
    }

    private func sampleAnswer(_ question: TOEFLQuestion) -> TOEFLAnswer {
        var answer = TOEFLAnswer()
        answer.selectedChoiceID = question.correctChoiceID
        answer.textEntry = question.acceptedTextAnswers.first ?? ""
        answer.orderedTokens = question.correctSentenceTokens
        if question.taskType == .writeEmail || question.taskType == .academicDiscussion {
            answer.writtenResponse = question.sampleAnswer ?? "Dear Professor, I am writing because the schedule changed. However, I would appreciate your help. For example, could we meet on Friday? Thank you. Best regards."
        }
        if question.section == .speaking {
            answer.recordingDuration = Double(question.responseSeconds ?? 10)
            answer.rubricScore = 4
            answer.transcript = question.taskType == .listenRepeat ? question.audioScript : "Sample response."
        }
        return answer
    }
    #endif
}
