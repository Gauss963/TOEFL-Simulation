import Foundation
import Combine

enum TOEFLExamPhase: Equatable {
    case directions
    case testing
    case completed
}

@MainActor
final class TOEFLExamSession: ObservableObject {
    let mode: ExamMode

    @Published private(set) var sections: [TOEFLExamSection]
    @Published private(set) var currentSectionIndex = 0
    @Published var currentQuestionIndex = 0
    @Published var answers: [String: TOEFLAnswer] = [:]
    @Published var markedQuestionIDs: Set<String> = []
    @Published private(set) var playedPromptIDs: Set<String> = []
    @Published private(set) var phase: TOEFLExamPhase = .directions
    @Published private(set) var secondsRemaining = 0
    @Published var isTimeVisible = true
    @Published private(set) var result: TOEFLExamResult?

    private let startedAt = Date()
    private let formSeed: UInt64
    private var sectionDeadline: Date?
    private var summaries: [TOEFLSectionSummary] = []

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

    var currentSection: TOEFLExamSection { sections[currentSectionIndex] }
    var currentQuestion: TOEFLQuestion { currentSection.questions[currentQuestionIndex] }
    var currentAnswer: TOEFLAnswer { answers[currentQuestion.id] ?? TOEFLAnswer() }
    var isCurrentMarked: Bool { markedQuestionIDs.contains(currentQuestion.id) }
    var isLastQuestion: Bool { currentQuestionIndex == currentSection.questions.count - 1 }
    var progress: Double { Double(currentQuestionIndex + 1) / Double(max(1, currentSection.questions.count)) }
    var answeredCount: Int { currentSection.questions.filter { !(answers[$0.id] ?? TOEFLAnswer()).isEmpty }.count }
    var markedCount: Int { currentSection.questions.filter { markedQuestionIDs.contains($0.id) }.count }
    var canReview: Bool { currentSection.kind == .reading || currentSection.kind == .writing }
    var minimumReachableIndex: Int {
        guard let router = currentSection.routerCount, currentSection.route != nil, currentQuestionIndex >= router else { return 0 }
        return router
    }
    var canGoBack: Bool {
        guard currentSection.kind != .listening && currentSection.kind != .speaking else { return false }
        return currentQuestionIndex > minimumReachableIndex
    }
    var routeLabel: String? { currentSection.route?.title }

    func beginSection() {
        secondsRemaining = currentSection.durationSeconds
        sectionDeadline = Date().addingTimeInterval(TimeInterval(secondsRemaining))
        currentQuestionIndex = 0
        phase = .testing
    }

    func tick() {
        guard phase == .testing, let sectionDeadline else { return }
        secondsRemaining = max(0, Int(ceil(sectionDeadline.timeIntervalSinceNow)))
        if secondsRemaining == 0 { finishSection() }
    }

    func goBack() {
        guard canGoBack else { return }
        currentQuestionIndex -= 1
    }

    func goNext() {
        if isLastQuestion {
            finishSection()
            return
        }
        if let routerCount = currentSection.routerCount,
           currentSection.route == nil,
           currentQuestionIndex + 1 == routerCount {
            configureAdaptiveModule()
        }
        currentQuestionIndex += 1
    }

    func goToQuestion(_ index: Int) {
        guard canReview,
              currentSection.questions.indices.contains(index),
              index >= minimumReachableIndex else { return }
        currentQuestionIndex = index
    }

    func toggleMark() {
        if isCurrentMarked { markedQuestionIDs.remove(currentQuestion.id) }
        else { markedQuestionIDs.insert(currentQuestion.id) }
    }

    func selectChoice(_ choiceID: String) {
        var answer = currentAnswer
        answer.selectedChoiceID = choiceID
        answers[currentQuestion.id] = answer
    }

    func updateTextEntry(_ value: String) {
        var answer = currentAnswer
        answer.textEntry = value
        answers[currentQuestion.id] = answer
    }

    func toggleSentenceToken(_ token: String) {
        var answer = currentAnswer
        if let index = answer.orderedTokens.firstIndex(of: token) {
            answer.orderedTokens.remove(at: index)
        } else if answer.orderedTokens.count < currentQuestion.sentenceTokens.count {
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

    func updateRecording(duration: Double) {
        var answer = currentAnswer
        answer.recordingDuration = max(answer.recordingDuration, duration)
        answers[currentQuestion.id] = answer
    }

    func updateSpeakingSelfRating(_ value: Int) {
        var answer = currentAnswer
        answer.speakingSelfRating = max(0, min(5, value))
        answers[currentQuestion.id] = answer
    }

    func markPromptPlayed() {
        playedPromptIDs.insert(currentQuestion.id)
    }

    func canPlayPrompt(for question: TOEFLQuestion) -> Bool {
        mode != .fullLength || !playedPromptIDs.contains(question.id)
    }

    func status(for question: TOEFLQuestion) -> QuestionProgressStatus {
        if markedQuestionIDs.contains(question.id) { return .marked }
        if !(answers[question.id] ?? TOEFLAnswer()).isEmpty { return .answered }
        return .unanswered
    }

    func finishSection() {
        guard phase == .testing else { return }
        sectionDeadline = nil
        if currentSection.routerCount != nil && currentSection.route == nil {
            configureAdaptiveModule()
        }
        let section = currentSection
        let objective = section.questions.compactMap { question -> Bool? in
            TOEFLAnswerMatcher.isCorrect(question: question, answer: answers[question.id] ?? TOEFLAnswer())
        }
        let correct = objective.filter { $0 }.count
        let answered = section.questions.filter { !(answers[$0.id] ?? TOEFLAnswer()).isEmpty }.count
        let score: (Double, String)
        switch section.kind {
        case .reading, .listening:
            let band = TOEFLPracticeScoring.objectiveBand(correct: correct, total: objective.count, route: section.route)
            let routeText = section.route.map { " Route: \($0.title)." } ?? ""
            score = (band, "Machine-scored practice accuracy: \(correct)/\(objective.count).\(routeText)")
        case .writing:
            score = TOEFLPracticeScoring.writingBand(questions: section.questions, answers: answers)
        case .speaking:
            score = TOEFLPracticeScoring.speakingBand(questions: section.questions, answers: answers)
        }
        summaries.append(TOEFLSectionSummary(
            kind: section.kind,
            correct: correct,
            objectiveTotal: objective.count,
            answered: answered,
            total: section.questions.count,
            route: section.route,
            band: score.0,
            rubricDetail: score.1
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
        let routerQuestions = Array(currentSection.questions.prefix(routerCount))
        let objective = routerQuestions.compactMap { question in
            TOEFLAnswerMatcher.isCorrect(question: question, answer: answers[question.id] ?? TOEFLAnswer())
        }
        let accuracy = objective.isEmpty ? 0 : Double(objective.filter { $0 }.count) / Double(objective.count)
        let route: AdaptiveRoute = accuracy >= 0.65 ? .upper : .lower
        let module = TOEFLQuestionBank.moduleQuestions(
            for: currentSection.kind,
            route: route,
            seed: formSeed,
            excluding: Set(routerQuestions.map(\.id))
        )
        sections[currentSectionIndex].questions = routerQuestions + module
        sections[currentSectionIndex].route = route
    }

    private func completeExam() {
        phase = .completed
        let bands = summaries.map(\.band)
        let overall = TOEFLPracticeScoring.overall(bands)
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
            for question in currentSection.questions {
                var answer = TOEFLAnswer()
                answer.selectedChoiceID = question.correctChoiceID
                answer.textEntry = question.acceptedTextAnswers.first ?? ""
                answer.orderedTokens = question.correctSentenceTokens
                if question.taskType == .writeEmail || question.taskType == .academicDiscussion {
                    answer.writtenResponse = question.sampleAnswer ?? "I support this position because it offers a practical benefit. For example, students can apply what they learn and reflect on the result. However, flexible options would make the policy fairer."
                }
                if question.section == .speaking {
                    answer.recordingDuration = question.taskType == .listenRepeat ? 6 : 25
                    answer.speakingSelfRating = 4
                }
                answers[question.id] = answer
            }
            if currentSection.routerCount != nil && currentSection.route == nil { configureAdaptiveModule() }
            finishSection()
        }
    }
    #endif
}

enum QuestionProgressStatus {
    case unanswered
    case answered
    case marked
}
