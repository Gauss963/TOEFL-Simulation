import Foundation
#if os(macOS)
import AppKit
#else
import UIKit
#endif

/// Scores Write an Email and Write for an Academic Discussion on the official 0–5 rubrics.
///
/// ETS uses AI engines trained on certified human ratings (features include sentence count, coherence,
/// similarity to the prompt, syntactic variety, word frequency, politeness markers, and grammar, usage,
/// and mechanics errors). This local engine measures the same observable features and maps them to the
/// rubric points; it is a practice estimate, not an ETS score.
enum WritingRubric {
    private static let stopWords: Set<String> = [
        "the", "a", "an", "and", "or", "but", "of", "to", "in", "on", "at", "for", "with", "is", "are", "was", "were",
        "be", "been", "it", "this", "that", "these", "those", "i", "you", "we", "they", "he", "she", "my", "your",
        "our", "their", "as", "by", "from", "so", "if", "not", "do", "does", "did", "have", "has", "had", "will",
        "would", "can", "could", "should", "about", "what", "which", "who", "how", "why", "more", "most", "some"
    ]

    private static let connectors = [
        "because", "however", "therefore", "for example", "for instance", "although", "while", "also", "in addition",
        "moreover", "as a result", "on the other hand", "first", "second", "finally", "since", "unless", "instead",
        "in contrast", "consequently", "that is why", "for this reason"
    ]

    static func score(question: TOEFLQuestion, response: String) -> (Int, String) {
        let text = response.trimmingCharacters(in: .whitespacesAndNewlines)
        let words = tokens(text)
        guard words.count >= 5 else { return (0, "Blank or too short to evaluate.") }

        let promptText = [question.prompt, question.passage ?? "", question.context ?? ""].joined(separator: " ")
        if copiedRatio(response: words, prompt: tokens(promptText)) > 0.6 {
            return (0, "The response mostly repeats the prompt.")
        }

        let isEmail = question.taskType == .writeEmail
        let target = isEmail ? 110.0 : 120.0
        let sentences = text.split(whereSeparator: { ".!?".contains($0) }).map { tokens(String($0)).count }.filter { $0 > 2 }
        let content = words.filter { !stopWords.contains($0) && $0.count > 2 }
        let diversity = Double(Set(content).count) / Double(max(1, content.count))
        let averageLength = Double(content.reduce(0) { $0 + $1.count }) / Double(max(1, content.count))
        let lowered = text.lowercased()
        let connectorCount = connectors.filter { lowered.contains($0) }.count
        let variety: Double = {
            guard sentences.count > 2 else { return 0.3 }
            let mean = Double(sentences.reduce(0, +)) / Double(sentences.count)
            let deviation = sqrt(sentences.map { pow(Double($0) - mean, 2) }.reduce(0, +) / Double(sentences.count))
            return min(1, deviation / 6)
        }()
        let misspelled = misspellingCount(text)
        let errorRate = Double(misspelled) / Double(words.count) * 100

        let promptKeys = Set(tokens(promptText).filter { !stopWords.contains($0) && $0.count > 3 })
        let relevance = min(1, Double(Set(content).intersection(promptKeys).count) / 4)

        let taskCompletion: Double
        let conventions: Double
        if isEmail {
            let hasGreeting = ["dear", "hello", "hi ", "good morning", "good afternoon"].contains { lowered.hasPrefix($0) || lowered.contains("\n\($0)") || lowered.prefix(40).contains($0) }
            let hasClosing = ["sincerely", "best", "regards", "thank you", "thanks", "warmly"].contains { lowered.suffix(120).contains($0) }
            conventions = (hasGreeting ? 0.5 : 0) + (hasClosing ? 0.5 : 0)
            let politeness = ["please", "could you", "would you", "i would appreciate", "thank"].filter { lowered.contains($0) }.count
            taskCompletion = min(1, Double(sentences.count) / 6) * 0.7 + min(1, Double(politeness) / 2) * 0.3
        } else {
            let engagesPeers = question.passage.map { passage in
                passage.split(separator: "\n").compactMap { $0.split(separator: ":").first.map(String.init) }
                    .contains { name in name.count < 20 && lowered.contains(name.lowercased()) }
            } ?? false
            let stance = ["i agree", "i disagree", "i believe", "i think", "in my opinion", "i support", "i would argue"].contains { lowered.contains($0) }
            taskCompletion = (stance ? 0.5 : 0.2) + (engagesPeers ? 0.3 : 0) + min(0.2, Double(sentences.count) / 25)
            conventions = 1
        }

        let elaboration = min(1, Double(words.count) / target)
        let language = min(1, 0.35 * min(1, diversity / 0.7) + 0.25 * min(1, (averageLength - 4) / 2.5) + 0.2 * variety + 0.2 * min(1, Double(connectorCount) / 3))
        let mechanics = max(0, 1 - errorRate / 8)

        var raw = 5 * (0.25 * taskCompletion + 0.25 * elaboration + 0.25 * language + 0.15 * mechanics + 0.05 * conventions + 0.05 * relevance)
        if words.count < 30 { raw = min(raw, 1.5) } else if words.count < 60 { raw = min(raw, 2.5) } else if words.count < 90 { raw = min(raw, 3.5) }
        if errorRate > 10 { raw = min(raw, 2.5) } else if errorRate > 5 { raw = min(raw, 3.5) }
        if relevance == 0 { raw = min(raw, 2) }
        let score = max(1, min(5, Int(raw.rounded())))

        let descriptor = switch score {
        case 5: "Fully successful: well elaborated, varied, and nearly error-free."
        case 4: "Generally successful: adequate elaboration with few errors."
        case 3: "Partially successful: some elaboration missing or unclear; noticeable errors."
        case 2: "Mostly unsuccessful: limited elaboration and accumulating errors."
        default: "Unsuccessful: very limited, unclear language."
        }
        return (score, "\(descriptor) \(words.count) words · \(sentences.count) sentences · \(misspelled) spelling errors · \(connectorCount) connectors.")
    }

    private static func tokens(_ text: String) -> [String] {
        text.lowercased().split { !$0.isLetter && $0 != "'" }.map(String.init)
    }

    private static func copiedRatio(response: [String], prompt: [String]) -> Double {
        guard response.count >= 4 else { return 0 }
        func grams(_ words: [String]) -> Set<String> {
            guard words.count >= 4 else { return [] }
            return Set((0...(words.count - 4)).map { words[$0..<($0 + 4)].joined(separator: " ") })
        }
        let responseGrams = grams(response)
        return Double(responseGrams.intersection(grams(prompt)).count) / Double(max(1, responseGrams.count))
    }

    private static func misspellingCount(_ text: String) -> Int {
        #if os(macOS)
        let checker = NSSpellChecker.shared
        let tag = NSSpellChecker.uniqueSpellDocumentTag()
        defer { checker.closeSpellDocument(withTag: tag) }
        var count = 0
        var offset = 0
        let length = (text as NSString).length
        while offset < length {
            let range = checker.checkSpelling(of: text, startingAt: offset, language: "en_US", wrap: false, inSpellDocumentWithTag: tag, wordCount: nil)
            if range.location == NSNotFound { break }
            count += 1
            offset = range.location + max(1, range.length)
        }
        return count
        #else
        let checker = UITextChecker()
        var count = 0
        var offset = 0
        let length = (text as NSString).length
        while offset < length {
            let range = checker.rangeOfMisspelledWord(in: text, range: NSRange(location: 0, length: length), startingAt: offset, wrap: false, language: "en_US")
            if range.location == NSNotFound { break }
            count += 1
            offset = range.location + max(1, range.length)
        }
        return count
        #endif
    }
}
