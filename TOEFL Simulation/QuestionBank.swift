import Foundation

/// Assembles test forms that follow the ETS TOEFL iBT 2026 blueprint.
///
/// Reading and Listening are two-stage adaptive: a router module, then a lower or upper module.
/// Scored counts per module follow the ETS technical manual (Reading 20 + 15, Listening 20 + 15);
/// unscored pretest items bring every path to the published 50 and 47 items.
enum TOEFLQuestionBank {
    static let allQuestions: [TOEFLQuestion] = {
        let candidates = [
            Bundle.main.url(forResource: "TOEFL2026Bank", withExtension: "json"),
            Bundle.main.url(forResource: "TOEFL2026Bank", withExtension: "json", subdirectory: "Resources")
        ]
        for url in candidates.compactMap({ $0 }) {
            guard let data = try? Data(contentsOf: url) else { continue }
            do {
                return try JSONDecoder().decode([TOEFLQuestion].self, from: data)
            } catch {
                assertionFailure("Could not decode TOEFL2026Bank.json: \(error)")
            }
        }
        assertionFailure("Missing bundled resource: TOEFL2026Bank.json")
        return []
    }()

    /// Items grouped by shared stimulus, in their authored order.
    private static let sets: [[TOEFLQuestion]] = {
        var order: [String] = []
        var grouped: [String: [TOEFLQuestion]] = [:]
        for question in allQuestions {
            let key = question.setID ?? question.id
            if grouped[key] == nil { order.append(key) }
            grouped[key, default: []].append(question)
        }
        return order.map { grouped[$0]!.sorted { ($0.setIndex ?? 0) < ($1.setIndex ?? 0) } }
    }()

    /// A block of sets to draw: `sizes` restricts set sizes (e.g. 2- and 3-item Daily Life sets).
    private struct Draw {
        let task: TOEFLTaskType
        let sizes: [Int?]
        var unscored = false

        init(_ task: TOEFLTaskType, sets: Int, unscored: Bool = false) {
            self.task = task
            self.sizes = Array(repeating: nil, count: sets)
            self.unscored = unscored
        }

        init(_ task: TOEFLTaskType, sizes: [Int], unscored: Bool = false) {
            self.task = task
            self.sizes = sizes
            self.unscored = unscored
        }
    }

    private enum Stage { case router, lower, upper, linear }

    // MARK: - Blueprint

    private static func plan(for kind: TOEFLSectionKind, stage: Stage) -> [Draw] {
        switch (kind, stage) {
        case (.reading, .router):
            return [
                Draw(.completeWords, sets: 1), Draw(.completeWords, sets: 1, unscored: true),
                Draw(.readDailyLife, sizes: [2, 3]),
                Draw(.readAcademicPassage, sets: 1)
            ]
        case (.reading, .lower):
            return [
                Draw(.completeWords, sets: 1),
                Draw(.readDailyLife, sizes: [2, 3]), Draw(.readDailyLife, sizes: [2, 3], unscored: true)
            ]
        case (.reading, .upper):
            return [
                Draw(.completeWords, sets: 1),
                Draw(.readAcademicPassage, sets: 1), Draw(.readAcademicPassage, sets: 1, unscored: true)
            ]
        case (.listening, .router):
            return [
                Draw(.listenChooseResponse, sets: 8), Draw(.listenChooseResponse, sets: 4, unscored: true),
                Draw(.listenConversation, sets: 2), Draw(.listenConversation, sets: 1, unscored: true),
                Draw(.listenAnnouncement, sets: 2),
                Draw(.listenAcademicTalk, sets: 1)
            ]
        case (.listening, .lower):
            return [
                Draw(.listenChooseResponse, sets: 7),
                Draw(.listenConversation, sets: 2),
                Draw(.listenAnnouncement, sets: 2), Draw(.listenAnnouncement, sets: 1, unscored: true),
                Draw(.listenAcademicTalk, sets: 1, unscored: true)
            ]
        case (.listening, .upper):
            return [
                Draw(.listenChooseResponse, sets: 3),
                Draw(.listenConversation, sets: 2),
                Draw(.listenAnnouncement, sets: 1, unscored: true),
                Draw(.listenAcademicTalk, sets: 2), Draw(.listenAcademicTalk, sets: 1, unscored: true)
            ]
        case (.writing, _):
            return [Draw(.buildSentence, sets: 10), Draw(.writeEmail, sets: 1), Draw(.academicDiscussion, sets: 1)]
        case (.speaking, _):
            return [Draw(.listenRepeat, sets: 1), Draw(.interview, sets: 1)]
        default:
            return []
        }
    }

    private static func difficulties(for stage: Stage) -> Set<TOEFLDifficulty> {
        switch stage {
        case .router, .linear: [.intermediate]
        case .lower: [.foundational, .intermediate]
        case .upper: [.advanced]
        }
    }

    // MARK: - Forms

    static func fullSections(seed: UInt64 = UInt64.random(in: UInt64.min...UInt64.max)) -> [TOEFLExamSection] {
        TOEFLSectionKind.allCases.map { section(for: $0, seed: seed) }
    }

    static func practiceSection(
        for kind: TOEFLSectionKind,
        seed: UInt64 = UInt64.random(in: UInt64.min...UInt64.max)
    ) -> TOEFLExamSection {
        section(for: kind, seed: seed)
    }

    private static func section(for kind: TOEFLSectionKind, seed: UInt64) -> TOEFLExamSection {
        if kind.isAdaptive {
            let router = questions(plan: plan(for: kind, stage: .router), stage: .router, seed: mixedSeed(seed, label: "\(kind.rawValue)-router"))
            // A provisional lower module keeps the item count visible until routing decides the real one.
            let provisional = moduleQuestions(for: kind, route: .lower, seed: seed, excluding: Set(router.map(\.id)))
            return TOEFLExamSection(
                id: "\(kind.rawValue)-section", kind: kind, durationSeconds: kind.officialMinutes * 60,
                questions: router + provisional, routerCount: router.count, route: nil
            )
        }
        return TOEFLExamSection(
            id: "\(kind.rawValue)-section", kind: kind, durationSeconds: kind.officialMinutes * 60,
            questions: questions(plan: plan(for: kind, stage: .linear), stage: .linear, seed: mixedSeed(seed, label: "\(kind.rawValue)-linear")),
            routerCount: nil, route: nil
        )
    }

    static func moduleQuestions(
        for kind: TOEFLSectionKind,
        route: AdaptiveRoute,
        seed: UInt64 = UInt64.random(in: UInt64.min...UInt64.max),
        excluding excludedIDs: Set<String> = []
    ) -> [TOEFLQuestion] {
        let stage: Stage = route == .lower ? .lower : .upper
        return questions(
            plan: plan(for: kind, stage: stage),
            stage: stage,
            seed: mixedSeed(seed, label: "\(kind.rawValue)-\(route.rawValue)"),
            excluding: excludedIDs
        )
    }

    private static func questions(plan: [Draw], stage: Stage, seed: UInt64, excluding excludedIDs: Set<String> = []) -> [TOEFLQuestion] {
        var generator = SeededGenerator(seed: seed)
        var usedSets = Set(excludedIDs.compactMap { id in allQuestions.first { $0.id == id }.map { $0.setID ?? $0.id } })
        var usedTopics = Set<String>()
        // Draws of the same task are collected first, then interleaved so unscored sets are not predictable.
        var blocks: [(TOEFLTaskType, [[TOEFLQuestion]])] = []
        for draw in plan {
            var drawn: [[TOEFLQuestion]] = []
            for size in draw.sizes {
                let preferred = difficulties(for: stage)
                let pool = sets.filter { set in
                    guard let first = set.first, first.taskType == draw.task else { return false }
                    let key = first.setID ?? first.id
                    return !usedSets.contains(key) && (size == nil || set.count == size)
                }
                let ranked = pool.filter { preferred.contains($0[0].difficulty) }.shuffled(using: &generator)
                    + pool.filter { !preferred.contains($0[0].difficulty) }.shuffled(using: &generator)
                let fresh = ranked.first { !usedTopics.contains(topicKey($0[0])) } ?? ranked.first
                guard var chosen = fresh else {
                    preconditionFailure("Not enough \(draw.task.title) sets in the bank")
                }
                if draw.unscored {
                    chosen = chosen.map { question in
                        var copy = question
                        copy.isScored = false
                        return copy
                    }
                }
                usedSets.insert(chosen[0].setID ?? chosen[0].id)
                usedTopics.insert(topicKey(chosen[0]))
                drawn.append(chosen)
            }
            if let index = blocks.firstIndex(where: { $0.0 == draw.task }) {
                blocks[index].1 += drawn
            } else {
                blocks.append((draw.task, drawn))
            }
        }
        return blocks.flatMap { task, drawn -> [TOEFLQuestion] in
            let ordered = task == .listenRepeat || task == .interview ? drawn : drawn.shuffled(using: &generator)
            return ordered.flatMap { $0 }
        }
    }

    /// Avoids, for example, a Complete the Words paragraph and an academic passage on the same subject.
    private static func topicKey(_ question: TOEFLQuestion) -> String {
        question.setting ?? question.setID ?? question.id
    }

    // MARK: - Validation

    static func validate() -> [String] {
        var issues: [String] = []
        if Set(allQuestions.map(\.id)).count != allQuestions.count { issues.append("Duplicate question IDs.") }
        for question in allQuestions {
            if question.choices.isEmpty == false, question.correctChoiceID == nil { issues.append("\(question.id) has no key.") }
            if question.taskType.isListeningStimulus || question.section == .speaking {
                if !TOEFLExamAudio.hasClip(question.audioKey) { issues.append("\(question.id) has no recording.") }
            }
            if question.taskType == .buildSentence {
                let blanks = (question.sentenceTemplate ?? "").components(separatedBy: "___").count - 1
                if blanks != question.correctSentenceTokens.count { issues.append("\(question.id) blank count mismatch.") }
            }
        }
        for seed in UInt64(1)...UInt64(30) {
            for kind in TOEFLSectionKind.allCases {
                var section = practiceSection(for: kind, seed: seed &* 7919)
                for route in [AdaptiveRoute.lower, .upper] where kind.isAdaptive {
                    let router = Array(section.questions.prefix(section.routerCount ?? 0))
                    section.questions = router + moduleQuestions(for: kind, route: route, seed: seed, excluding: Set(router.map(\.id)))
                    let scored = section.questions.filter(\.countsTowardScore).count
                    if section.questions.count != kind.officialItemCount { issues.append("\(kind.title) \(route) has \(section.questions.count) items.") }
                    if scored != 35 { issues.append("\(kind.title) \(route) has \(scored) scored items.") }
                    if Set(section.questions.map(\.id)).count != section.questions.count { issues.append("\(kind.title) \(route) repeats an item.") }
                }
                if !kind.isAdaptive, section.questions.count != kind.officialItemCount {
                    issues.append("\(kind.title) form has \(section.questions.count) items.")
                }
            }
        }
        return Array(Set(issues)).sorted()
    }

    // MARK: - Seeding

    private static func mixedSeed(_ seed: UInt64, label: String) -> UInt64 {
        var hash = seed ^ 0xcbf29ce484222325
        for byte in label.utf8 {
            hash ^= UInt64(byte)
            hash &*= 0x100000001b3
        }
        return hash
    }

    private struct SeededGenerator: RandomNumberGenerator {
        private var state: UInt64

        init(seed: UInt64) {
            state = seed &+ 0x9e3779b97f4a7c15
        }

        mutating func next() -> UInt64 {
            state &+= 0x9e3779b97f4a7c15
            var value = state
            value = (value ^ (value >> 30)) &* 0xbf58476d1ce4e5b9
            value = (value ^ (value >> 27)) &* 0x94d049bb133111eb
            return value ^ (value >> 31)
        }
    }
}
