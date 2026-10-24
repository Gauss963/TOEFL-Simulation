import Foundation
import Speech

/// On-device transcription of a recorded response. Audio never leaves the device.
nonisolated struct SpeechTranscription: Sendable {
    struct Segment: Sendable {
        let text: String
        let start: Double
        let duration: Double
    }

    let text: String
    let segments: [Segment]
}

nonisolated enum SpeechTranscriber {
    static func requestAuthorization() async -> Bool {
        if SFSpeechRecognizer.authorizationStatus() == .authorized { return true }
        return await withCheckedContinuation { continuation in
            SFSpeechRecognizer.requestAuthorization { continuation.resume(returning: $0 == .authorized) }
        }
    }

    static var isOnDeviceAvailable: Bool {
        guard let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en-US")) else { return false }
        return recognizer.isAvailable && recognizer.supportsOnDeviceRecognition
    }

    static func transcribe(_ url: URL, contextualStrings: [String] = []) async -> SpeechTranscription? {
        guard SFSpeechRecognizer.authorizationStatus() == .authorized,
              let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en-US")),
              recognizer.isAvailable, recognizer.supportsOnDeviceRecognition else { return nil }
        let request = SFSpeechURLRecognitionRequest(url: url)
        request.requiresOnDeviceRecognition = true
        request.shouldReportPartialResults = false
        request.addsPunctuation = true
        request.taskHint = .dictation
        request.contextualStrings = contextualStrings
        return await withCheckedContinuation { continuation in
            var finished = false
            recognizer.recognitionTask(with: request) { result, error in
                guard !finished else { return }
                if let result, result.isFinal {
                    finished = true
                    let segments = result.bestTranscription.segments.map {
                        SpeechTranscription.Segment(text: $0.substring, start: $0.timestamp, duration: $0.duration)
                    }
                    continuation.resume(returning: SpeechTranscription(text: result.bestTranscription.formattedString, segments: segments))
                } else if error != nil {
                    finished = true
                    // No speech detected is a valid, empty response.
                    continuation.resume(returning: SpeechTranscription(text: "", segments: []))
                }
            }
        }
    }
}

/// Scores Speaking responses on the official 0–5 TOEFL iBT 2026 rubrics.
///
/// ETS combines proprietary AI engines with certified human raters. This engine uses the same
/// observable features ETS lists for its scoring models (repeat accuracy; speaking rate, pauses,
/// elaboration, vocabulary, cohesion, and relevance) and maps them onto the published rubric points.
nonisolated enum SpeakingRubric {
    private static let functionWords: Set<String> = [
        "a", "an", "the", "of", "to", "in", "on", "at", "for", "with", "by", "from", "and", "or", "but", "so",
        "is", "are", "was", "were", "be", "been", "am", "do", "does", "did", "have", "has", "had", "will", "would",
        "can", "could", "should", "may", "might", "must", "it", "its", "this", "that", "these", "those", "there",
        "here", "as", "if", "than", "then", "your", "you", "our", "we", "they", "their", "he", "she", "his", "her",
        "i", "my", "me", "us", "them", "all", "some", "any", "each", "every", "not", "no", "up", "out", "into"
    ]

    private static let connectors = [
        "because", "so", "for example", "for instance", "however", "although", "though", "also", "first", "second",
        "finally", "then", "in addition", "on the other hand", "i think", "i believe", "in my opinion", "that's why",
        "which", "while", "since", "as a result", "especially", "actually", "but"
    ]

    // MARK: Listen and Repeat

    static func scoreRepeat(target: String, transcription: SpeechTranscription?) -> (Int, String) {
        guard let transcription else { return (0, "Speech recognition was unavailable, so this response could not be scored.") }
        let expected = words(target)
        let spoken = words(transcription.text)
        guard !spoken.isEmpty else { return (0, "No speech was detected.") }

        let alignment = align(expected, spoken)
        let missingContent = alignment.missing.filter { !functionWords.contains($0) }.count
        let missingFunction = alignment.missing.count - missingContent
        // A changed ending on the same word (open/opens, study/studied) is a tense or number slip.
        let inflections = alignment.substituted.filter { stem($0.expected) == stem($0.spoken) || $0.expected.hasPrefix($0.spoken) || $0.spoken.hasPrefix($0.expected) }.count
        let changed = alignment.substituted.filter { !(stem($0.expected) == stem($0.spoken) || $0.expected.hasPrefix($0.spoken) || $0.spoken.hasPrefix($0.expected)) }
        let wrongContent = changed.filter { !functionWords.contains($0.expected) }.count
        let wrongFunction = changed.count - wrongContent + inflections
        let coverage = Double(alignment.matched + inflections) / Double(max(1, expected.count))

        let sameWordsReordered = Set(expected) == Set(spoken) && expected.count == spoken.count

        let score: Int
        let reason: String
        if alignment.matched == expected.count && alignment.extra == 0 {
            score = 5; reason = "Exact repetition."
        } else if sameWordsReordered || (missingContent + wrongContent <= 1 && missingFunction + wrongFunction + alignment.extra <= 2 && coverage >= 0.8) {
            score = 4; reason = "Meaning captured with minor changes (a function word, tense/number, or word order)."
        } else if coverage >= 0.65 && missingContent + wrongContent <= 3 {
            score = 3; reason = "Essentially full sentence, but the meaning is not captured accurately."
        } else if coverage >= 0.35 {
            score = 2; reason = "A significant part of the sentence is missing or changed."
        } else {
            score = 1; reason = "Very little of the sentence was reproduced."
        }
        let detail = "\(reason) Words matched: \(alignment.matched)/\(expected.count)."
        return (score, detail)
    }

    // MARK: Take an Interview

    static func scoreInterview(question: String, transcription: SpeechTranscription?, responseSeconds: Int) -> (Int, String) {
        guard let transcription else { return (0, "Speech recognition was unavailable, so this response could not be scored.") }
        let spoken = words(transcription.text)
        guard spoken.count >= 3 else { return (0, "No meaningful speech was detected.") }

        let segments = transcription.segments
        let speakingStart = segments.first?.start ?? 0
        let speakingEnd = segments.last.map { $0.start + $0.duration } ?? Double(responseSeconds)
        let speakingSpan = max(1, speakingEnd - speakingStart)
        let rate = Double(spoken.count) / speakingSpan * 60
        let longPauses = zip(segments, segments.dropFirst()).filter { $1.start - ($0.start + $0.duration) > 1.2 }.count
        let fillers = spoken.filter { ["um", "uh", "er", "hmm"].contains($0) }.count

        let content = spoken.filter { !functionWords.contains($0) && $0.count > 2 }
        let diversity = Double(Set(content).count) / Double(max(1, content.count))
        let lowered = transcription.text.lowercased()
        let connectorCount = connectors.filter { lowered.contains($0) }.count
        let questionKeys = Set(words(question).filter { !functionWords.contains($0) && $0.count > 3 }.map(stem))
        let overlap = Set(content.map(stem)).intersection(questionKeys).count

        let development = min(1, Double(spoken.count) / 85)
        let fluency: Double = {
            var value = rate >= 95 && rate <= 175 ? 1 : max(0, 1 - abs(rate - 135) / 110)
            value -= Double(longPauses) * 0.08 + Double(fillers) * 0.03
            return max(0, min(1, value))
        }()
        let language = min(1, 0.5 * min(1, diversity / 0.75) + 0.5 * min(1, Double(connectorCount) / 3))
        let relevance = min(1, Double(overlap) / 2)

        var raw = 5 * (0.35 * development + 0.25 * fluency + 0.2 * language + 0.2 * relevance)
        if overlap == 0 { raw = min(raw, 2.4) }
        if spoken.count < 10 { raw = min(raw, 1.4) }
        else if spoken.count < 25 { raw = min(raw, 2.4) }
        else if spoken.count < 45 { raw = min(raw, 3.4) }
        let score = max(1, min(5, Int(raw.rounded())))

        let descriptor = switch score {
        case 5: "Fully addresses the question; clear, fluent, and well elaborated."
        case 4: "Addresses the question; reasonably clear with adequate elaboration."
        case 3: "Limited elaboration or clarity; choppy pace or limited range."
        case 2: "Minimally connected to the question with little elaboration."
        default: "Vague or very limited response."
        }
        let detail = "\(descriptor) \(spoken.count) words · \(Int(rate.rounded())) words/min · \(longPauses) long pauses · \(connectorCount) connectors."
        return (score, detail)
    }

    // MARK: Helpers

    static func words(_ text: String) -> [String] {
        let expanded = text.lowercased()
            .replacingOccurrences(of: "’", with: "'")
            .replacingOccurrences(of: "won't", with: "will not")
            .replacingOccurrences(of: "can't", with: "cannot")
            .replacingOccurrences(of: "n't", with: " not")
            .replacingOccurrences(of: "'re", with: " are")
            .replacingOccurrences(of: "'ll", with: " will")
            .replacingOccurrences(of: "'ve", with: " have")
            .replacingOccurrences(of: "'m", with: " am")
            .replacingOccurrences(of: "it's", with: "it is")
            .replacingOccurrences(of: "that's", with: "that is")
            .replacingOccurrences(of: "there's", with: "there is")
            .replacingOccurrences(of: "let's", with: "let us")
        var result: [String] = []
        for token in expanded.split(whereSeparator: { !$0.isLetter && !$0.isNumber && $0 != "'" }) {
            let word = String(token).trimmingCharacters(in: CharacterSet(charactersIn: "'"))
            guard !word.isEmpty else { continue }
            if let number = Int(word) {
                let formatter = NumberFormatter()
                formatter.numberStyle = .spellOut
                formatter.locale = Locale(identifier: "en_US")
                result += words(formatter.string(from: NSNumber(value: number)) ?? word)
            } else {
                result.append(word)
            }
        }
        return result
    }

    private static func stem(_ word: String) -> String {
        for suffix in ["ing", "ed", "es", "s", "ly"] where word.count > suffix.count + 3 && word.hasSuffix(suffix) {
            return String(word.dropLast(suffix.count))
        }
        return word
    }

    private struct Alignment {
        var matched = 0
        var substituted: [(expected: String, spoken: String)] = []
        var missing: [String] = []
        var extra = 0
    }

    /// Word-level Levenshtein alignment between the target and the spoken words.
    private static func align(_ expected: [String], _ spoken: [String]) -> Alignment {
        let n = expected.count, m = spoken.count
        var cost = Array(repeating: Array(repeating: 0, count: m + 1), count: n + 1)
        for i in 0...n { cost[i][0] = i }
        for j in 0...m { cost[0][j] = j }
        if n > 0 && m > 0 {
            for i in 1...n {
                for j in 1...m {
                    let same = expected[i - 1] == spoken[j - 1] ? 0 : 1
                    cost[i][j] = min(cost[i - 1][j] + 1, cost[i][j - 1] + 1, cost[i - 1][j - 1] + same)
                }
            }
        }
        var result = Alignment()
        var i = n, j = m
        while i > 0 || j > 0 {
            if i > 0, j > 0, cost[i][j] == cost[i - 1][j - 1] + (expected[i - 1] == spoken[j - 1] ? 0 : 1) {
                if expected[i - 1] == spoken[j - 1] { result.matched += 1 } else { result.substituted.append((expected[i - 1], spoken[j - 1])) }
                i -= 1; j -= 1
            } else if i > 0, cost[i][j] == cost[i - 1][j] + 1 {
                result.missing.append(expected[i - 1]); i -= 1
            } else {
                result.extra += 1; j -= 1
            }
        }
        return result
    }
}
