import Foundation
import Combine

enum TOEFLSectionKind: String, Codable, CaseIterable, Identifiable {
    case reading
    case listening
    case writing
    case speaking

    var id: String { rawValue }

    var title: String { rawValue.capitalized }

    var icon: String {
        switch self {
        case .reading: "book.pages"
        case .listening: "headphones"
        case .writing: "keyboard"
        case .speaking: "waveform.and.mic"
        }
    }

    /// Items delivered on every path, including unscored (pretest) items, per the ETS 2026 blueprint.
    var officialItemCount: Int {
        switch self {
        case .reading: 50
        case .listening: 47
        case .writing: 12
        case .speaking: 11
        }
    }

    var officialMinutes: Int {
        switch self {
        case .reading: 30
        case .listening: 29
        case .writing: 23
        case .speaking: 8
        }
    }

    /// Raw score points published in the ETS 2026 specification.
    var rawMaximum: Int {
        switch self {
        case .reading, .listening: 35
        case .writing: 20
        case .speaking: 55
        }
    }

    var isAdaptive: Bool { self == .reading || self == .listening }
}

enum TOEFLTaskType: String, Codable, CaseIterable, Identifiable {
    case completeWords
    case readDailyLife
    case readAcademicPassage
    case listenChooseResponse
    case listenConversation
    case listenAnnouncement
    case listenAcademicTalk
    case buildSentence
    case writeEmail
    case academicDiscussion
    case listenRepeat
    case interview

    var id: String { rawValue }

    var title: String {
        switch self {
        case .completeWords: "Complete the Words"
        case .readDailyLife: "Read in Daily Life"
        case .readAcademicPassage: "Read an Academic Passage"
        case .listenChooseResponse: "Listen and Choose a Response"
        case .listenConversation: "Listen to a Conversation"
        case .listenAnnouncement: "Listen to an Announcement"
        case .listenAcademicTalk: "Listen to an Academic Talk"
        case .buildSentence: "Build a Sentence"
        case .writeEmail: "Write an Email"
        case .academicDiscussion: "Write for an Academic Discussion"
        case .listenRepeat: "Listen and Repeat"
        case .interview: "Take an Interview"
        }
    }

    var directions: String {
        switch self {
        case .completeWords:
            "Fill in the missing letters in the paragraph."
        case .readDailyLife, .readAcademicPassage:
            "Read the text and answer the questions."
        case .listenChooseResponse:
            "You will hear a person speak. Choose the best response."
        case .listenConversation:
            "Listen to a conversation. Then answer the questions."
        case .listenAnnouncement:
            "Listen to an announcement. Then answer the questions."
        case .listenAcademicTalk:
            "Listen to an academic talk. Then answer the questions."
        case .buildSentence:
            "Move the words in the boxes to create a grammatical sentence."
        case .writeEmail:
            "You will read some information and use it to write an email. You will have 7 minutes to write the email."
        case .academicDiscussion:
            "Your professor is teaching a class. Write a post responding to the professor's question. You will have 10 minutes to write."
        case .listenRepeat:
            "You will listen as someone speaks to you. Listen carefully and then repeat what you have heard. You will not have time to prepare."
        case .interview:
            "An interviewer will ask you questions. Answer each question as fully as you can. You will have 45 seconds to answer each question."
        }
    }

    var isListeningStimulus: Bool {
        switch self {
        case .listenChooseResponse, .listenConversation, .listenAnnouncement, .listenAcademicTalk: true
        default: false
        }
    }
}

enum TOEFLDifficulty: String, Codable, CaseIterable {
    case foundational
    case intermediate
    case advanced

    var title: String { rawValue.capitalized }
}

enum AdaptiveRoute: String, Codable {
    case lower
    case upper

    var title: String { self == .upper ? "Upper module" : "Lower module" }
}

struct TOEFLChoice: Identifiable, Hashable, Codable {
    let id: String
    let text: String
}

struct TOEFLSpeaker: Hashable, Codable {
    let role: String
    var name: String? = nil
    let gender: String

    var displayName: String { name.map { "\($0) · \(role.capitalized)" } ?? role.capitalized }
}

struct TOEFLQuestion: Identifiable, Hashable, Codable {
    let id: String
    let section: TOEFLSectionKind
    let taskType: TOEFLTaskType
    let difficulty: TOEFLDifficulty
    let prompt: String
    let passage: String?
    let audioScript: String?
    let choices: [TOEFLChoice]
    let correctChoiceID: String?
    let acceptedTextAnswers: [String]
    let sentenceTokens: [String]
    let correctSentenceTokens: [String]
    let responseGuidance: String?
    let sampleAnswer: String?
    let explanation: String
    let sourceNote: String
    /// Items that share one text or recording form a set (a Complete the Words paragraph, a conversation, …).
    var setID: String? = nil
    var setIndex: Int? = nil
    /// Unscored pretest items are delivered like any other item but excluded from the score.
    var isScored: Bool? = nil
    /// Narrator line or scenario introduction, shown and played before the first item of a set.
    var intro: String? = nil
    var speakers: [TOEFLSpeaker]? = nil
    /// Build a Sentence: the first speaker's line and the reply with ___ blanks.
    var context: String? = nil
    var sentenceTemplate: String? = nil
    /// Speaking: recording window in seconds.
    var responseSeconds: Int? = nil
    /// Listen and Repeat illustration: the setting and the places visited in order.
    var setting: String? = nil
    var stops: [String]? = nil
    /// Complete the Words study notes: the word's part of speech, its meaning in context (Traditional
    /// Chinese), and how the context and spelling lead to the answer.
    var partOfSpeech: String? = nil
    var wordGloss: String? = nil
    var blankReason: String? = nil

    var countsTowardScore: Bool { isScored ?? true }

    var isConstructedResponse: Bool {
        taskType == .writeEmail || taskType == .academicDiscussion || taskType == .listenRepeat || taskType == .interview
    }

    var isAudioTask: Bool { audioScript != nil }

    /// Key of the pre-rendered recording that belongs to this item.
    var audioKey: String {
        switch taskType {
        case .listenConversation, .listenAnnouncement, .listenAcademicTalk: setID ?? id
        default: id
        }
    }
}

struct TOEFLAnswer: Codable, Equatable {
    var selectedChoiceID: String? = nil
    var textEntry = ""
    var orderedTokens: [String] = []
    var writtenResponse = ""
    var recordingDuration: Double = 0
    var speakingSelfRating: Int = 0
    var transcript: String? = nil
    /// Rubric score (0–5) for Email, Academic Discussion, and Speaking items.
    var rubricScore: Int? = nil
    var rubricNote: String? = nil

    var isEmpty: Bool {
        selectedChoiceID == nil
            && textEntry.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && orderedTokens.isEmpty
            && writtenResponse.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && recordingDuration <= 0
    }
}

enum ExamMode: String, Codable, CaseIterable, Identifiable {
    case fullLength
    case readingPractice
    case listeningPractice
    case writingPractice
    case speakingPractice

    var id: String { rawValue }

    var title: String {
        switch self {
        case .fullLength: "Full-length 2026 Practice Test"
        case .readingPractice: "Reading Section"
        case .listeningPractice: "Listening Section"
        case .writingPractice: "Writing Section"
        case .speakingPractice: "Speaking Section"
        }
    }

    var subtitle: String {
        switch self {
        case .fullLength: "Reading → Listening → Writing → Speaking · 120 items · about 90 min"
        case .readingPractice: "50 items · 30 min · two adaptive modules"
        case .listeningPractice: "47 items · 29 min · two adaptive modules"
        case .writingPractice: "12 items · 23 min · three timed tasks"
        case .speakingPractice: "11 items · about 8 min · automatic timing"
        }
    }

    var sectionKinds: [TOEFLSectionKind] {
        switch self {
        case .fullLength: TOEFLSectionKind.allCases
        case .readingPractice: [.reading]
        case .listeningPractice: [.listening]
        case .writingPractice: [.writing]
        case .speakingPractice: [.speaking]
        }
    }
}

/// A block of items with its own clock, matching the module and task timers of the real test.
struct TOEFLTimedPart: Equatable {
    let title: String
    let range: Range<Int>
    let seconds: Int
    let allowsBack: Bool
}

struct TOEFLExamSection: Identifiable {
    let id: String
    let kind: TOEFLSectionKind
    let durationSeconds: Int
    var questions: [TOEFLQuestion]
    let routerCount: Int?
    var route: AdaptiveRoute?

    var displayTitle: String { kind.title }

    var timedParts: [TOEFLTimedPart] {
        switch kind {
        case .reading, .listening:
            let router = routerCount ?? questions.count
            let first = kind == .reading ? 21 * 60 : 18 * 60
            let second = kind == .reading ? 9 * 60 : 11 * 60
            var parts = [TOEFLTimedPart(title: "Module 1", range: 0..<router, seconds: first, allowsBack: kind == .reading)]
            if router < questions.count {
                parts.append(TOEFLTimedPart(title: "Module 2", range: router..<questions.count, seconds: second, allowsBack: kind == .reading))
            }
            return parts
        case .writing:
            var parts: [TOEFLTimedPart] = []
            var index = 0
            while index < questions.count {
                let task = questions[index].taskType
                var end = index
                while end < questions.count, questions[end].taskType == task { end += 1 }
                let seconds = switch task {
                case .writeEmail: 7 * 60
                case .academicDiscussion: 10 * 60
                default: 6 * 60
                }
                parts.append(TOEFLTimedPart(title: task.title, range: index..<end, seconds: seconds, allowsBack: task == .buildSentence))
                index = end
            }
            return parts
        case .speaking:
            return [TOEFLTimedPart(title: "Speaking", range: 0..<questions.count, seconds: durationSeconds, allowsBack: false)]
        }
    }
}

struct TOEFLSectionSummary: Codable, Equatable, Identifiable {
    let kind: TOEFLSectionKind
    let correct: Int
    let objectiveTotal: Int
    let answered: Int
    let total: Int
    let route: AdaptiveRoute?
    let band: Double
    let rubricDetail: String
    var rawScore: Int? = nil
    var rawMaximum: Int? = nil

    var id: String { kind.rawValue }
}

struct TOEFLQuestionReview: Codable, Equatable, Identifiable {
    let number: Int
    let question: TOEFLQuestion
    let answer: TOEFLAnswer
    let isCorrect: Bool?

    var id: String { question.id }
}

struct TOEFLExamResult: Codable, Equatable, Identifiable {
    let id: UUID
    let completedAt: Date
    let mode: ExamMode
    let overallBand: Double
    let comparableTotal120: Int
    let cefr: String
    let elapsedSeconds: Int
    let sections: [TOEFLSectionSummary]
    let reviews: [TOEFLQuestionReview]
}

@MainActor
final class TOEFLHistoryStore: ObservableObject {
    @Published private(set) var results: [TOEFLExamResult] = []

    private let key = "toefl.practice.results.v1"

    init() {
        guard let data = UserDefaults.standard.data(forKey: key),
              let decoded = try? JSONDecoder().decode([TOEFLExamResult].self, from: data) else { return }
        results = decoded.sorted { $0.completedAt > $1.completedAt }
    }

    func add(_ result: TOEFLExamResult) {
        results.insert(result, at: 0)
        save()
    }

    func clear() {
        results = []
        save()
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(results) else { return }
        UserDefaults.standard.set(data, forKey: key)
    }
}

enum TOEFLPracticeScoring {
    /// Reading and Listening: 35 scored items across the router and the second module.
    /// The lower module tops out below the upper module, mirroring ETS path-based equating.
    static func objectiveBand(correct: Int, total: Int, route: AdaptiveRoute?) -> Double {
        guard total > 0 else { return 1 }
        let accuracy = Double(correct) / Double(total)
        switch route {
        case .lower: return halfBand(1 + accuracy * 3.5)
        case .upper: return halfBand(2.5 + accuracy * 3.5)
        case nil: return halfBand(1 + accuracy * 5)
        }
    }

    /// Writing raw score 0–20: Build a Sentence (10 × 1) + Email (0–5) + Academic Discussion (0–5).
    static func writingBand(questions: [TOEFLQuestion], answers: [String: TOEFLAnswer]) -> (Double, String, Int) {
        let sentenceItems = questions.filter { $0.taskType == .buildSentence && $0.countsTowardScore }
        let sentenceCorrect = sentenceItems.filter {
            TOEFLAnswerMatcher.isCorrect(question: $0, answer: answers[$0.id] ?? TOEFLAnswer()) == true
        }.count
        let email = questions.first { $0.taskType == .writeEmail }.flatMap { answers[$0.id]?.rubricScore } ?? 0
        let discussion = questions.first { $0.taskType == .academicDiscussion }.flatMap { answers[$0.id]?.rubricScore } ?? 0
        let raw = sentenceCorrect + email + discussion
        let band = halfBand(1 + Double(raw) / 20 * 5)
        let detail = "Raw \(raw)/20 · Build a Sentence \(sentenceCorrect)/10 · Email \(email)/5 · Academic Discussion \(discussion)/5 (local rubric engine)."
        return (band, detail, raw)
    }

    /// Speaking raw score 0–55: eleven items scored 0–5 on the official rubrics.
    static func speakingBand(questions: [TOEFLQuestion], answers: [String: TOEFLAnswer]) -> (Double, String, Int) {
        guard !questions.isEmpty else { return (1, "No speaking items.", 0) }
        let repeatScores = questions.filter { $0.taskType == .listenRepeat }.map { answers[$0.id]?.rubricScore ?? 0 }
        let interviewScores = questions.filter { $0.taskType == .interview }.map { answers[$0.id]?.rubricScore ?? 0 }
        let raw = repeatScores.reduce(0, +) + interviewScores.reduce(0, +)
        let maximum = questions.count * 5
        let band = halfBand(1 + Double(raw) / Double(max(1, maximum)) * 5)
        let detail = "Raw \(raw)/\(maximum) · Listen and Repeat \(repeatScores.reduce(0, +))/\(repeatScores.count * 5) · Interview \(interviewScores.reduce(0, +))/\(interviewScores.count * 5) (on-device speech recognition + official rubrics)."
        return (band, detail, raw)
    }

    static func overall(_ bands: [Double]) -> Double {
        guard !bands.isEmpty else { return 1 }
        return halfBand(bands.reduce(0, +) / Double(bands.count))
    }

    /// Midpoint of the ETS comparable 0–120 range for each band (score scale update, 2026).
    static func comparable120(for band: Double) -> Int {
        switch halfBand(band) {
        case 6: 117
        case 5.5: 110
        case 5: 101
        case 4.5: 90
        case 4: 79
        case 3.5: 65
        case 3: 51
        case 2.5: 39
        case 2: 29
        case 1.5: 18
        default: 6
        }
    }

    static func cefr(for band: Double) -> String {
        switch halfBand(band) {
        case 6: "C2"
        case 5, 5.5: "C1"
        case 4, 4.5: "B2"
        case 3, 3.5: "B1"
        case 2, 2.5: "A2"
        default: "A1"
        }
    }

    static func halfBand(_ value: Double) -> Double {
        min(6, max(1, (value * 2).rounded() / 2))
    }
}

enum TOEFLAnswerMatcher {
    static func isCorrect(question: TOEFLQuestion, answer: TOEFLAnswer) -> Bool? {
        if let correctChoiceID = question.correctChoiceID {
            return answer.selectedChoiceID == correctChoiceID
        }
        if !question.acceptedTextAnswers.isEmpty {
            let candidate = normalize(answer.textEntry)
            return question.acceptedTextAnswers.contains { normalize($0) == candidate }
        }
        if !question.correctSentenceTokens.isEmpty {
            return answer.orderedTokens.map(normalize) == question.correctSentenceTokens.map(normalize)
        }
        return nil
    }

    nonisolated private static func normalize(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters)).lowercased()
    }
}
