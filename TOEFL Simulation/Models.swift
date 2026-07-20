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
            "Type the complete word that best restores the meaning of the text."
        case .readDailyLife, .readAcademicPassage:
            "Read the text and select the best answer."
        case .listenChooseResponse:
            "Listen once and select the most appropriate response."
        case .listenConversation, .listenAnnouncement, .listenAcademicTalk:
            "Listen once, take notes if useful, and select the best answer."
        case .buildSentence:
            "Select each phrase in the order that forms a complete grammatical sentence."
        case .writeEmail:
            "Write a clear email that addresses every part of the situation."
        case .academicDiscussion:
            "State and support your position while contributing to the discussion."
        case .listenRepeat:
            "Listen once, then record yourself repeating the sentence as accurately as possible."
        case .interview:
            "Listen to the interviewer and record a clear, developed response."
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

    var isConstructedResponse: Bool {
        taskType == .writeEmail || taskType == .academicDiscussion || taskType == .listenRepeat || taskType == .interview
    }

    var isAudioTask: Bool { audioScript != nil }
}

struct TOEFLAnswer: Codable, Equatable {
    var selectedChoiceID: String? = nil
    var textEntry = ""
    var orderedTokens: [String] = []
    var writtenResponse = ""
    var recordingDuration: Double = 0
    var speakingSelfRating: Int = 0

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
        case .readingPractice: "Reading Timed Practice"
        case .listeningPractice: "Listening Timed Practice"
        case .writingPractice: "Writing Timed Practice"
        case .speakingPractice: "Speaking Timed Practice"
        }
    }

    var subtitle: String {
        switch self {
        case .fullLength: "120 items · about 90 min section time; allow ~2 hr overall"
        case .readingPractice: "15 items · 10 minutes"
        case .listeningPractice: "14 items · 10 minutes"
        case .writingPractice: "12 items · 23 minutes"
        case .speakingPractice: "11 items · 8 minutes"
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

struct TOEFLExamSection: Identifiable {
    let id: String
    let kind: TOEFLSectionKind
    let durationSeconds: Int
    var questions: [TOEFLQuestion]
    let routerCount: Int?
    var route: AdaptiveRoute?

    var displayTitle: String { kind.title }
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
    static func objectiveBand(correct: Int, total: Int, route: AdaptiveRoute?) -> Double {
        guard total > 0 else { return 1 }
        let accuracy = Double(correct) / Double(total)
        var estimate = 1 + accuracy * 5
        if route == .lower { estimate = min(estimate, 4.5) }
        if route == .upper { estimate = max(estimate, 2.5) }
        return halfBand(estimate)
    }

    static func writingBand(questions: [TOEFLQuestion], answers: [String: TOEFLAnswer]) -> (Double, String) {
        let sentenceItems = questions.filter { $0.taskType == .buildSentence }
        let sentenceCorrect = sentenceItems.filter {
            normalizedTokens(answers[$0.id]?.orderedTokens ?? []) == normalizedTokens($0.correctSentenceTokens)
        }.count
        let grammarRatio = sentenceItems.isEmpty ? 0 : Double(sentenceCorrect) / Double(sentenceItems.count)

        let longResponses = questions.filter { $0.taskType == .writeEmail || $0.taskType == .academicDiscussion }
        let responseScores = longResponses.map { responseQuality(answers[$0.id]?.writtenResponse ?? "", task: $0.taskType) }
        let responseAverage = responseScores.isEmpty ? 0 : responseScores.reduce(0, +) / Double(responseScores.count)
        let combined = (grammarRatio * 0.35) + ((responseAverage / 5) * 0.65)
        let band = halfBand(1 + combined * 5)
        let formattedAverage = String(format: "%.1f", responseAverage)
        let detail = "Build a Sentence: \(sentenceCorrect)/\(sentenceItems.count). Local writing rubric average: \(formattedAverage)/5."
        return (band, detail)
    }

    static func speakingBand(questions: [TOEFLQuestion], answers: [String: TOEFLAnswer]) -> (Double, String) {
        guard !questions.isEmpty else { return (1, "No speaking items.") }
        let scores = questions.map { question -> Double in
            let answer = answers[question.id] ?? TOEFLAnswer()
            guard answer.recordingDuration > 0 else { return 0 }
            let selfScore = Double(max(0, min(5, answer.speakingSelfRating)))
            let minimum = question.taskType == .listenRepeat ? 2.0 : 10.0
            let completion = min(1, answer.recordingDuration / minimum)
            return (selfScore * 0.8) + (completion * 5 * 0.2)
        }
        let average = scores.reduce(0, +) / Double(scores.count)
        let recorded = questions.filter { (answers[$0.id]?.recordingDuration ?? 0) > 0 }.count
        return (halfBand(1 + average), "Recorded \(recorded)/\(questions.count) responses. Estimate combines completion and your rubric self-check.")
    }

    static func overall(_ bands: [Double]) -> Double {
        guard !bands.isEmpty else { return 1 }
        return halfBand(bands.reduce(0, +) / Double(bands.count))
    }

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

    private static func responseQuality(_ text: String, task: TOEFLTaskType) -> Double {
        let words = text.split { !$0.isLetter && $0 != "'" }
        guard !words.isEmpty else { return 0 }
        let lowered = words.map { $0.lowercased() }
        let uniqueRatio = Double(Set(lowered).count) / Double(words.count)
        let sentenceCount = max(1, text.filter { ".!?".contains($0) }.count)
        let target = task == .writeEmail ? 90.0 : 100.0
        let length = min(1, Double(words.count) / target)
        let development = min(1, Double(sentenceCount) / 5)
        let transitions = ["because", "however", "therefore", "for example", "although", "while", "also"].filter {
            text.lowercased().contains($0)
        }.count
        let cohesion = min(1, Double(transitions) / 3)
        let convention: Double
        if task == .writeEmail {
            let lower = text.lowercased()
            convention = (lower.contains("dear") || lower.contains("hello")) && (lower.contains("thank") || lower.contains("sincerely") || lower.contains("best")) ? 1 : 0.5
        } else {
            convention = 1
        }
        return min(5, 5 * (length * 0.3 + development * 0.25 + min(1, uniqueRatio / 0.65) * 0.2 + cohesion * 0.15 + convention * 0.1))
    }

    private static func halfBand(_ value: Double) -> Double {
        min(6, max(1, (value * 2).rounded() / 2))
    }

    private static func normalizedTokens(_ tokens: [String]) -> [String] {
        tokens.map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
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
