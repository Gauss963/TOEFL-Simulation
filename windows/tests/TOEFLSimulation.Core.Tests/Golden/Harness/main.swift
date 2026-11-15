import Foundation
import AppKit

/// Command-line harness that records the Mac app's behavior for the Windows Core parity tests.
/// It is compiled together with copies of the Mac sources by generate.sh.
@main
struct Harness {
    static func main() {
        TOEFLQuestionBank.usesExposureHistory = false
        var out: [String: Any] = [:]
        let bank = TOEFLQuestionBank.allQuestions
        out["questionCount"] = bank.count

        // Forms for fixed seeds (no exposure history), and with a synthetic history.
        func form(_ kind: TOEFLSectionKind, _ seed: UInt64, _ recent: [String]) -> [String: Any] {
            let section = TOEFLQuestionBank.practiceSection(for: kind, seed: seed, recent: recent)
            var entry: [String: Any] = [
                "kind": kind.rawValue, "seed": String(seed),
                "questions": section.questions.map(\.id),
                "scored": section.questions.map(\.countsTowardScore),
                "routerCount": section.routerCount ?? -1,
            ]
            if kind.isAdaptive {
                let router = Array(section.questions.prefix(section.routerCount ?? 0))
                for route in [AdaptiveRoute.lower, .upper] {
                    let module = TOEFLQuestionBank.moduleQuestions(for: kind, route: route, seed: seed, excluding: Set(router.map(\.id)), recent: recent)
                    entry[route.rawValue] = module.map(\.id)
                }
            }
            return entry
        }
        let seeds: [UInt64] = [0, 1, 2, 42, 7919, 104_729, 123_456_789, 0xDEADBEEF, UInt64.max, 9_876_543_210_123]
        var forms: [[String: Any]] = []
        var formsWithHistory: [[String: Any]] = []
        // A history that has seen a slice of every task pool, oldest first.
        var seenKeys: [String] = []
        var seenSet = Set<String>()
        for (index, question) in bank.enumerated() where index % 3 == 0 {
            let key = question.setID ?? question.id
            if seenSet.insert(key).inserted { seenKeys.append(key) }
        }
        for seed in seeds {
            for kind in TOEFLSectionKind.allCases {
                forms.append(form(kind, seed, []))
                formsWithHistory.append(form(kind, seed, seenKeys))
            }
        }
        out["forms"] = forms
        out["history"] = seenKeys
        out["formsWithHistory"] = formsWithHistory
        out["rotationIssues"] = TOEFLQuestionBank.rotationIssues()
        out["validate"] = TOEFLQuestionBank.validate()
        out["vocabularyIds"] = TOEFLStudyBank.words.map(\.id)
        out["vocabularySources"] = TOEFLStudyBank.words.map { $0.sources }
        out["vocabularyHighFrequency"] = TOEFLStudyBank.words.map { $0.isHighFrequency }
        out["sourceOptions"] = TOEFLStudyBank.sourceOptions
        out["vocabularyValidate"] = TOEFLStudyBank.validate()

        // Scoring tables.
        var objective: [[String: Any]] = []
        for route in ["lower", "upper", "none"] {
            for correct in 0...35 {
                let r: AdaptiveRoute? = route == "none" ? nil : AdaptiveRoute(rawValue: route)
                objective.append(["route": route, "correct": correct, "total": 35, "band": TOEFLPracticeScoring.objectiveBand(correct: correct, total: 35, route: r)])
            }
        }
        out["objectiveBands"] = objective
        var halves: [[String: Any]] = []
        for tenth in 0...70 {
            let value = Double(tenth) / 10
            halves.append(["value": value, "half": TOEFLPracticeScoring.halfBand(value), "comparable": TOEFLPracticeScoring.comparable120(for: value), "cefr": TOEFLPracticeScoring.cefr(for: value)])
        }
        out["halfBands"] = halves

        // Speaking rubric.
        func transcription(_ text: String, gapEvery: Int = 0, wordSeconds: Double = 0.42) -> SpeechTranscription {
            var segments: [SpeechTranscription.Segment] = []
            var t = 0.6
            for (index, word) in text.split(separator: " ").enumerated() {
                segments.append(.init(text: String(word), start: t, duration: wordSeconds * 0.8))
                t += wordSeconds
                if gapEvery > 0, index % gapEvery == gapEvery - 1 { t += 1.5 }
            }
            return SpeechTranscription(text: text, segments: segments)
        }
        func encode(_ t: SpeechTranscription?) -> Any {
            guard let t else { return NSNull() }
            return ["text": t.text, "segments": t.segments.map { ["text": $0.text, "start": $0.start, "duration": $0.duration] }]
        }
        var repeats: [[String: Any]] = []
        let repeatItems = bank.filter { $0.taskType == .listenRepeat }
        for (index, item) in repeatItems.enumerated() {
            let target = item.audioScript ?? ""
            var words = target.split(separator: " ").map(String.init)
            var variants: [String] = [target]
            if words.count > 3 {
                variants.append(words.dropLast().joined(separator: " "))
                variants.append(words.prefix(words.count / 2).joined(separator: " "))
                variants.append(words.reversed().joined(separator: " "))
                variants.append(words.prefix(2).joined(separator: " "))
                words[1] = words[1] + "s"
                variants.append(words.joined(separator: " "))
                words[words.count - 2] = "banana"
                variants.append(words.joined(separator: " ") + " um yes")
            }
            if index % 10 == 0 { variants.append("") }
            for text in variants {
                let t = transcription(text)
                let scored = SpeakingRubric.scoreRepeat(target: target, transcription: t)
                repeats.append(["target": target, "transcription": encode(t), "score": scored.0, "detail": scored.1])
            }
        }
        repeats.append(["target": "Hello there.", "transcription": NSNull(), "score": SpeakingRubric.scoreRepeat(target: "Hello there.", transcription: nil).0, "detail": SpeakingRubric.scoreRepeat(target: "Hello there.", transcription: nil).1])
        out["repeat"] = repeats

        var interviews: [[String: Any]] = []
        let interviewItems = bank.filter { $0.taskType == .interview }
        let answers = bank.filter { [.writeEmail, .academicDiscussion, .interview].contains($0.taskType) }.compactMap { $0.sampleAnswer }.filter { !$0.isEmpty }
        for (index, item) in interviewItems.enumerated() {
            let question = item.audioScript ?? ""
            let answer = answers[index % answers.count]
            let words = answer.split(separator: " ").map(String.init)
            let variants: [(String, Int, Double)] = [
                (answer, 0, 0.42), (answer, 7, 0.35), (words.prefix(30).joined(separator: " "), 0, 0.5),
                (words.prefix(12).joined(separator: " "), 3, 0.7), (question + " " + answer, 0, 0.3),
                ("um I think uh " + words.prefix(50).joined(separator: " "), 5, 0.45),
                ("I have 3 cats and 21 dogs since 1999, and it's 2024 now.", 0, 0.4), ("yes", 0, 0.4), ("", 0, 0.4),
            ]
            for (text, gap, seconds) in variants {
                let t = transcription(text, gapEvery: gap, wordSeconds: seconds)
                let scored = SpeakingRubric.scoreInterview(question: question, transcription: t, responseSeconds: item.responseSeconds ?? 45)
                interviews.append(["question": question, "responseSeconds": item.responseSeconds ?? 45, "transcription": encode(t), "score": scored.0, "detail": scored.1])
            }
        }
        let noSegments = SpeechTranscription(text: "I really think that studying with friends helps because we can share notes and explain ideas.", segments: [])
        let scoredNoSegments = SpeakingRubric.scoreInterview(question: "Do you prefer studying with friends?", transcription: noSegments, responseSeconds: 45)
        interviews.append(["question": "Do you prefer studying with friends?", "responseSeconds": 45, "transcription": encode(noSegments), "score": scoredNoSegments.0, "detail": scoredNoSegments.1])
        out["interview"] = interviews

        var wordCases: [[String: Any]] = []
        var numberTexts = ["It's 0 and 7, 13, 20, 21, 99, 100, 101, 110, 999, 1000, 1001, 1500, 1999, 2000, 2024, 10000, 12345, 100000, 999999, 1000000, 1234567, 1000000000, 2500000000000, 9223372036854775807, 99999999999999999999",
                           "I won't go; you can't stop me. They're here, we'll see, I've been, I'm fine. That's it, there's more, let's go. Don’t worry!",
                           "007 agents and 'quoted' words, rock'n'roll, ½ cup, 3rd place, 4th"]
        for n in [0, 5, 11, 15, 19, 30, 45, 58, 70, 88, 200, 305, 412, 1_010, 1_100, 7_000, 15_015, 20_000, 101_001, 1_000_001, 43_000_000, 5_000_000_000] { numberTexts.append("\(n)") }
        for text in numberTexts { wordCases.append(["text": text, "words": SpeakingRubric.words(text)]) }
        out["words"] = wordCases

        // Writing rubric, with the macOS spell checker's count recorded for each response.
        var writing: [[String: Any]] = []
        let checker = WritingMisspellings()
        for item in bank where item.taskType == .writeEmail || item.taskType == .academicDiscussion {
            let sample = item.sampleAnswer ?? ""
            let sampleWords = sample.split(separator: " ").map(String.init)
            var variants = [sample, sampleWords.prefix(25).joined(separator: " "), sampleWords.prefix(55).joined(separator: " "),
                            sampleWords.prefix(85).joined(separator: " "), item.prompt, "Too short.", sample.replacingOccurrences(of: "e", with: "ee")]
            if item.id.hasSuffix("1") { variants.append("") }
            for text in variants {
                let scored = WritingRubric.score(question: item, response: text)
                writing.append(["questionID": item.id, "response": text, "misspellings": checker.count(text), "score": scored.0, "note": scored.1])
            }
        }
        out["writing"] = writing

        out["beep"] = TOEFLExamAudio.beep.base64EncodedString()
        out["endTone"] = TOEFLExamAudio.endTone.base64EncodedString()
        out["examClipCount"] = TOEFLExamAudio.pack.clipCount
        out["wordClipCount"] = WordPronunciationLibrary.shared.clipCount
        var clipSizes: [String: Int] = [:]
        for key in ["an-01", "intro-cv-001", "lr-01-1", "nonexistent"] + bank.prefix(4000).filter { $0.taskType.isListeningStimulus || $0.section == .speaking }.enumerated().filter { $0.offset % 97 == 0 }.map(\.element.audioKey) {
            clipSizes[key] = TOEFLExamAudio.pack.clip(for: key)?.count ?? -1
        }
        out["examClipSizes"] = clipSizes
        var wordSizes: [String: Int] = [:]
        for key in ["Adapt", " abhor ", "a penny pincher", "zzzz"] + TOEFLStudyBank.words.enumerated().filter { $0.offset % 53 == 0 }.map(\.element.word) {
            wordSizes[key] = WordPronunciationLibrary.shared.clip(for: key)?.count ?? -1
        }
        out["wordClipSizes"] = wordSizes

        // Complete the Words paragraphs.
        var paragraphs: [[String: Any]] = []
        var seenSets = Set<String>()
        for item in bank where item.taskType == .completeWords {
            guard let setID = item.setID, seenSets.insert(setID).inserted, let passage = item.passage else { continue }
            let pieces = CTestParagraph(passage).pieces.map { piece -> [String: Any] in
                switch piece {
                case .text(let text): ["text": text]
                case .blank(let index, let prefix, let missing, let suffix): ["index": index, "prefix": prefix, "missing": missing, "suffix": suffix]
                }
            }
            paragraphs.append(["passage": passage, "pieces": pieces])
        }
        for passage in ["A {wo|rd}, then {an|other}. {E|nd}", "{x|y}{p|q} tail", "no blanks here", " lead  {sp|ace} "] {
            let pieces = CTestParagraph(passage).pieces.map { piece -> [String: Any] in
                switch piece {
                case .text(let text): ["text": text]
                case .blank(let index, let prefix, let missing, let suffix): ["index": index, "prefix": prefix, "missing": missing, "suffix": suffix]
                }
            }
            paragraphs.append(["passage": passage, "pieces": pieces])
        }
        out["ctest"] = paragraphs

        out["meaningGroups"] = bank.filter { $0.taskType == .listenRepeat }.map { ["text": $0.audioScript ?? "", "groups": GuideHarness.meaningGroups($0.audioScript ?? "")] }

        let data = try! JSONSerialization.data(withJSONObject: out, options: [.sortedKeys])
        try! data.write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
    }
}

/// The same NSSpellChecker loop as WritingRubric.misspellingCount, exposed for recording.
struct WritingMisspellings {
    func count(_ text: String) -> Int {
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
    }
}
