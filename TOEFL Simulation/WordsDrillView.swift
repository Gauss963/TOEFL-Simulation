import SwiftUI
import Combine
#if os(macOS)
import AppKit
#endif

/// One Complete the Words paragraph: its 10 blank items in order.
struct WordsDrillParagraph: Identifiable {
    let id: String
    let title: String
    let difficulty: TOEFLDifficulty
    let blanks: [TOEFLQuestion]

    var passage: String { blanks.first?.passage ?? "" }

    static let all: [WordsDrillParagraph] = {
        let items = TOEFLQuestionBank.allQuestions.filter { $0.taskType == .completeWords }
        var order: [String] = []
        var grouped: [String: [TOEFLQuestion]] = [:]
        for item in items {
            let key = item.setID ?? item.id
            if grouped[key] == nil { order.append(key) }
            grouped[key, default: []].append(item)
        }
        return order.compactMap { key in
            guard let blanks = grouped[key]?.sorted(by: { ($0.setIndex ?? 0) < ($1.setIndex ?? 0) }), let first = blanks.first else { return nil }
            return WordsDrillParagraph(id: key, title: first.setting ?? "Paragraph", difficulty: first.difficulty, blanks: blanks)
        }
    }()
}

/// Saved drill progress: every checked attempt (with what was typed), per-paragraph results, and the
/// words most often missed.
@MainActor
final class WordsDrillStore: ObservableObject {
    struct Attempt: Codable, Equatable {
        var attempts: Int
        var bestCorrect: Int
        var lastCorrect: Int
        var lastDate: Date
    }

    /// One checked paragraph. Attempts made before answers were kept (1.5.0) have no entries.
    struct AttemptRecord: Codable, Equatable, Identifiable {
        let id: UUID
        let paragraphID: String
        let date: Date
        let entries: [String]?
        let results: [Bool]?
        let correct: Int

        var paragraph: WordsDrillParagraph? { WordsDrillParagraph.all.first { $0.id == paragraphID } }
    }

    struct MissedWord: Codable, Equatable, Identifiable {
        let word: String
        var partOfSpeech: String?
        var gloss: String?
        var misses: Int
        var lastMissed: Date
        var id: String { word.lowercased() }
    }

    private struct Saved: Codable {
        var attempts: [String: Attempt] = [:]
        var missed: [String: MissedWord] = [:]
        var history: [AttemptRecord] = []

        init() {}

        init(from decoder: Decoder) throws {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            attempts = try container.decodeIfPresent([String: Attempt].self, forKey: .attempts) ?? [:]
            missed = try container.decodeIfPresent([String: MissedWord].self, forKey: .missed) ?? [:]
            history = try container.decodeIfPresent([AttemptRecord].self, forKey: .history) ?? []
        }
    }

    @Published private var saved: Saved
    private let key = "toefl.words-drill.v1"
    private let historyLimit = 2_000
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if let data = defaults.data(forKey: key), let decoded = try? JSONDecoder().decode(Saved.self, from: data) {
            saved = decoded
        } else {
            saved = Saved()
        }
        // Attempts saved before the history existed keep their score and date.
        let logged = Set(saved.history.map(\.paragraphID))
        let earlier = saved.attempts.filter { !logged.contains($0.key) }.map { id, attempt in
            AttemptRecord(id: UUID(), paragraphID: id, date: attempt.lastDate, entries: nil, results: nil, correct: attempt.lastCorrect)
        }
        if !earlier.isEmpty {
            saved.history = (saved.history + earlier).sorted { $0.date < $1.date }
            persist()
        }
    }

    func attempt(for paragraph: WordsDrillParagraph) -> Attempt? { saved.attempts[paragraph.id] }

    var completedCount: Int { saved.attempts.count }

    /// Accuracy across each paragraph's most recent attempt.
    var recentAccuracy: Double? {
        guard !saved.attempts.isEmpty else { return nil }
        return Double(saved.attempts.values.reduce(0) { $0 + $1.lastCorrect }) / Double(saved.attempts.count * 10)
    }

    var wordsToReview: [MissedWord] {
        saved.missed.values.sorted { ($0.misses, $0.lastMissed) > ($1.misses, $1.lastMissed) }
    }

    /// Checked attempts, newest first.
    var history: [AttemptRecord] { saved.history.reversed() }

    /// The latest attempt in which this word was answered wrong.
    func latestMiss(of word: String) -> AttemptRecord? {
        history.first { record in
            guard let results = record.results, let blanks = record.paragraph?.blanks else { return false }
            return zip(blanks, results).contains { blank, isCorrect in !isCorrect && blank.sampleAnswer?.lowercased() == word.lowercased() }
        }
    }

    @discardableResult
    func record(_ paragraph: WordsDrillParagraph, entries: [String], results: [Bool]) -> AttemptRecord {
        let correct = results.filter { $0 }.count
        var attempt = saved.attempts[paragraph.id] ?? Attempt(attempts: 0, bestCorrect: 0, lastCorrect: 0, lastDate: .now)
        attempt.attempts += 1
        attempt.bestCorrect = max(attempt.bestCorrect, correct)
        attempt.lastCorrect = correct
        attempt.lastDate = .now
        saved.attempts[paragraph.id] = attempt
        let record = AttemptRecord(id: UUID(), paragraphID: paragraph.id, date: .now, entries: entries, results: results, correct: correct)
        saved.history.append(record)
        if saved.history.count > historyLimit { saved.history.removeFirst(saved.history.count - historyLimit) }
        for (blank, isCorrect) in zip(paragraph.blanks, results) {
            let word = blank.sampleAnswer ?? ""
            let id = word.lowercased()
            if isCorrect {
                // A word leaves the review list after it is answered correctly twice in a row.
                if var entry = saved.missed[id] {
                    entry.misses -= 1
                    saved.missed[id] = entry.misses <= 0 ? nil : entry
                }
            } else {
                var entry = saved.missed[id] ?? MissedWord(word: word, partOfSpeech: blank.partOfSpeech, gloss: blank.wordGloss, misses: 0, lastMissed: .now)
                entry.misses = max(entry.misses, 0) + 2
                entry.lastMissed = .now
                saved.missed[id] = entry
            }
        }
        persist()
        return record
    }

    /// Unattempted paragraphs first, then the weakest and least recently practiced.
    func next(after current: String?, difficulty: TOEFLDifficulty?) -> WordsDrillParagraph? {
        let pool = WordsDrillParagraph.all.filter { ($0.difficulty == difficulty || difficulty == nil) && $0.id != current }
        let fresh = pool.filter { saved.attempts[$0.id] == nil }
        if let pick = fresh.randomElement() { return pick }
        return pool.min { lhs, rhs in
            let a = saved.attempts[lhs.id]!, b = saved.attempts[rhs.id]!
            return (a.lastCorrect, a.lastDate) < (b.lastCorrect, b.lastDate)
        } ?? WordsDrillParagraph.all.first
    }

    private func persist() {
        if let data = try? JSONEncoder().encode(saved) { defaults.set(data, forKey: key) }
    }

    #if DEBUG
    /// Checks that 1.5.0 data (scores only) migrates into the history and that new attempts keep
    /// what was typed, using a throwaway defaults domain.
    static func selfTestIssues() -> [String] {
        let suite = "r14521220.TOEFL-Simulation.selftest"
        guard let defaults = UserDefaults(suiteName: suite), let paragraph = WordsDrillParagraph.all.first else { return ["No drill test domain"] }
        defer { defaults.removePersistentDomain(forName: suite) }
        let legacy = #"{"attempts":{"\#(paragraph.id)":{"lastCorrect":7,"bestCorrect":7,"attempts":1,"lastDate":814618353.4}},"missed":{}}"#
        defaults.set(Data(legacy.utf8), forKey: "toefl.words-drill.v1")
        var issues: [String] = []
        let store = WordsDrillStore(defaults: defaults)
        if store.history.count != 1 || store.history.first?.correct != 7 || store.history.first?.entries != nil {
            issues.append("1.5.0 drill data did not migrate into the history")
        }
        let entries = paragraph.blanks.map { $0.acceptedTextAnswers.first ?? "" }
        store.record(paragraph, entries: entries, results: Array(repeating: true, count: entries.count))
        let reloaded = WordsDrillStore(defaults: defaults)
        if reloaded.history.count != 2 || reloaded.history.first?.entries != entries || reloaded.attempt(for: paragraph)?.attempts != 2 {
            issues.append("Drill attempts are not saved with their answers")
        }
        return issues
    }
    #endif
}

/// A checked paragraph: each blank shows the letters typed, marked right or wrong, with the correct
/// letters beside wrong answers. Without results (attempts saved before 1.5.1) the answers are shown.
struct CheckedParagraphView: View {
    let paragraph: WordsDrillParagraph
    let entries: [String]?
    let results: [Bool]?

    var body: some View {
        FlowLayout(spacing: 6, lineSpacing: 12) {
            ForEach(Array(CTestParagraph(paragraph.passage).pieces.enumerated()), id: \.offset) { _, piece in
                switch piece {
                case .text(let word):
                    Text(word).font(.title3)
                case .blank(let index, let prefix, _, let suffix):
                    HStack(spacing: 0) {
                        Text(prefix).font(.title3)
                        blank(index)
                        if !suffix.isEmpty { Text(suffix).font(.title3) }
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func blank(_ index: Int) -> some View {
        let answer = paragraph.blanks.indices.contains(index) ? paragraph.blanks[index].acceptedTextAnswers.first ?? "" : ""
        let typed = entries.flatMap { $0.indices.contains(index) ? $0[index] : nil } ?? ""
        let isCorrect = results.flatMap { $0.indices.contains(index) ? $0[index] : nil }
        switch isCorrect {
        case false?:
            HStack(spacing: 4) {
                if !typed.isEmpty {
                    Text(typed).font(.title3.monospaced()).strikethrough().foregroundStyle(.red)
                }
                Text(answer).font(.title3.monospaced().bold()).foregroundStyle(TOEFLTheme.teal)
            }
            .padding(.horizontal, 3).padding(.vertical, 2)
            .background(Color.red.opacity(0.1), in: RoundedRectangle(cornerRadius: 4))
            .overlay(RoundedRectangle(cornerRadius: 4).stroke(Color.red.opacity(0.7)))
        case true?:
            Text(typed).font(.title3.monospaced().bold()).foregroundStyle(TOEFLTheme.teal)
                .padding(.horizontal, 3).padding(.vertical, 2)
                .background(TOEFLTheme.teal.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                .overlay(RoundedRectangle(cornerRadius: 4).stroke(TOEFLTheme.teal.opacity(0.8)))
        case nil:
            Text(answer).font(.title3.monospaced().bold()).foregroundStyle(TOEFLTheme.navy)
                .padding(.horizontal, 3).padding(.vertical, 2)
                .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 4))
                .overlay(RoundedRectangle(cornerRadius: 4).stroke(TOEFLTheme.border))
        }
    }
}

/// The score and, for every blank, the word, its part of speech, its meaning, and why it fits.
struct WordExplanationsView: View {
    let paragraph: WordsDrillParagraph
    let entries: [String]?
    let results: [Bool]?
    let correct: Int

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                Text("\(correct) / 10 correct")
                    .font(.system(size: 28, weight: .bold, design: .rounded))
                    .foregroundStyle(correct >= 8 ? TOEFLTheme.teal : (correct >= 5 ? TOEFLTheme.gold : .red))
                Spacer()
                Text(results == nil
                     ? "Typed answers were not saved for attempts made before version 1.5.1."
                     : (correct == 10 ? "Perfect paragraph." : "Review the explanations for the missed words."))
                    .font(.subheadline).foregroundStyle(.secondary)
            }
            ForEach(Array(paragraph.blanks.enumerated()), id: \.element.id) { index, blank in
                row(number: index + 1, blank: blank,
                    isCorrect: results.flatMap { $0.indices.contains(index) ? $0[index] : nil },
                    typed: entries.flatMap { $0.indices.contains(index) ? $0[index] : nil } ?? "")
                if index < paragraph.blanks.count - 1 { Divider() }
            }
        }
    }

    private func row(number: Int, blank: TOEFLQuestion, isCorrect: Bool?, typed: String) -> some View {
        let word = blank.sampleAnswer ?? ""
        let missing = blank.acceptedTextAnswers.first ?? ""
        let kept = String(word.dropLast(missing.count))
        return HStack(alignment: .top, spacing: 12) {
            Image(systemName: isCorrect == nil ? "circle" : (isCorrect == true ? "checkmark.circle.fill" : "xmark.circle.fill"))
                .font(.title3)
                .foregroundStyle(isCorrect == nil ? Color.secondary : (isCorrect == true ? TOEFLTheme.teal : .red))
            VStack(alignment: .leading, spacing: 5) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text("\(number).").foregroundStyle(.secondary).monospacedDigit()
                    (Text(kept) + Text(missing).bold().foregroundColor(TOEFLTheme.teal))
                        .font(.title3.monospaced())
                    if let pos = blank.partOfSpeech { Text(pos).font(.subheadline.italic()).foregroundStyle(.secondary) }
                    if let gloss = blank.wordGloss { Text(gloss).font(.subheadline.weight(.semibold)) }
                    if isCorrect == false {
                        Text(typed.isEmpty ? "(blank)" : "you typed \(kept)\(typed)")
                            .font(.caption).foregroundStyle(.red)
                    }
                }
                if let reason = blank.blankReason {
                    Text(reason).font(.subheadline).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }
}

/// A past attempt, shown as it was answered, with the explanations.
struct WordsDrillReviewView: View {
    let record: WordsDrillStore.AttemptRecord
    let practiceAgain: (WordsDrillParagraph) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text(record.paragraph?.title ?? "Paragraph").font(.title2.bold())
                    Text("\(record.paragraph?.difficulty.title ?? "") · \(record.date.formatted(date: .abbreviated, time: .shortened))")
                        .font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                if let paragraph = record.paragraph {
                    Button("Practice Again") { dismiss(); practiceAgain(paragraph) }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                }
                Button("Done") { dismiss() }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
                    .keyboardShortcut(.cancelAction)
            }
            .padding(20)
            Divider()
            ScrollView { details }
        }
        .frame(minWidth: 760, idealWidth: 920, minHeight: 600, idealHeight: 820)
        .background(TOEFLTheme.canvas)
    }

    @ViewBuilder
    var details: some View {
        if let paragraph = record.paragraph {
            VStack(alignment: .leading, spacing: 20) {
                CheckedParagraphView(paragraph: paragraph, entries: record.entries, results: record.results)
                    .toeflCard()
                WordExplanationsView(paragraph: paragraph, entries: record.entries, results: record.results, correct: record.correct)
                    .toeflCard()
            }
            .padding(24)
        } else {
            ContentUnavailableView("Paragraph unavailable", systemImage: "doc.questionmark",
                                   description: Text("This paragraph is no longer in the question bank."))
                .padding(40)
        }
    }
}

/// Complete the Words practice with immediate feedback after each paragraph.
struct WordsDrillView: View {
    enum LevelFilter: String, CaseIterable, Identifiable {
        case all = "All levels", intermediate = "Intermediate", advanced = "Advanced"
        var id: String { rawValue }
        var difficulty: TOEFLDifficulty? {
            switch self {
            case .all: nil
            case .intermediate: .intermediate
            case .advanced: .advanced
            }
        }
    }

    @StateObject private var store = WordsDrillStore()
    @State private var level: LevelFilter = .all
    @State private var paragraph: WordsDrillParagraph?
    @State private var entries: [String] = []
    @State private var results: [Bool]?
    @State private var reviewing: WordsDrillStore.AttemptRecord?
    @State private var showsAllHistory = false
    @FocusState private var focused: Int?

    var body: some View {
        ScrollView { content }
        .navigationTitle("Complete the Words")
        .onAppear { if paragraph == nil { load(store.next(after: nil, difficulty: level.difficulty)) } }
        .onChange(of: level) { _, newLevel in load(store.next(after: nil, difficulty: newLevel.difficulty)) }
        .sheet(item: $reviewing) { record in
            WordsDrillReviewView(record: record) { load($0) }
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            if let paragraph {
                exercise(paragraph)
                if let results {
                    WordExplanationsView(paragraph: paragraph, entries: entries, results: results, correct: results.filter { $0 }.count)
                        .toeflCard()
                }
            }
            if !store.history.isEmpty { historyList }
            if !store.wordsToReview.isEmpty { reviewList }
        }
        .frame(maxWidth: 1000, alignment: .leading)
        .padding(30)
    }

    #if DEBUG
    /// Renders the page with a paragraph filled in and checked, and a past attempt's review, for
    /// `-renderWordsDrill <directory>`.
    static func renderSnapshots(to directory: URL) {
        guard let paragraph = WordsDrillParagraph.all.first(where: { $0.blanks.first?.blankReason != nil }) else { return }
        // Two wrong entries, one blank left empty, the rest correct.
        var entries = paragraph.blanks.map { $0.acceptedTextAnswers.first ?? "" }
        entries[1] = "xx"; entries[4] = ""; entries[7] = String(entries[7].dropLast())
        let results = paragraph.blanks.enumerated().map { index, blank in
            TOEFLAnswerMatcher.isCorrect(question: blank, answer: TOEFLAnswer(textEntry: entries[index])) ?? false
        }
        func write<V: View>(_ view: V, _ name: String, width: CGFloat) {
            let renderer = ImageRenderer(content: view.frame(width: width).background(TOEFLTheme.canvas))
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                try? png.write(to: directory.appendingPathComponent("\(name).png"))
            }
            #endif
        }
        for (name, checked) in [("drill-answering", false), ("drill-checked", true)] {
            var view = WordsDrillView()
            view._paragraph = State(initialValue: paragraph)
            view._entries = State(initialValue: entries)
            if checked { view._results = State(initialValue: results) }
            write(view.content, name, width: 1000)
        }
        let record = WordsDrillStore.AttemptRecord(id: UUID(), paragraphID: paragraph.id, date: .now, entries: entries, results: results, correct: results.filter { $0 }.count)
        write(WordsDrillReviewView(record: record) { _ in }.details, "drill-review", width: 920)
        let legacy = WordsDrillStore.AttemptRecord(id: UUID(), paragraphID: paragraph.id, date: .now, entries: nil, results: nil, correct: 7)
        write(WordsDrillReviewView(record: legacy) { _ in }.details, "drill-review-legacy", width: 920)
    }
    #endif

    // MARK: - Header

    private var header: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Complete the Words practice")
                        .font(.system(size: 30, weight: .bold, design: .rounded))
                        .foregroundStyle(TOEFLTheme.navy)
                    Text("Type the missing letters, then check the paragraph to see which blanks are right, what each word means, and why it fits.")
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer()
                Picker("Level", selection: $level) {
                    ForEach(LevelFilter.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)
                .frame(width: 320)
            }
            HStack(spacing: 22) {
                stat("Paragraphs practiced", "\(store.completedCount) / \(WordsDrillParagraph.all.count)")
                stat("Recent accuracy", store.recentAccuracy.map { "\(Int(($0 * 100).rounded()))%" } ?? "—")
                stat("Attempts", "\(store.history.count)")
                stat("Words to review", "\(store.wordsToReview.count)")
            }
        }
        .toeflCard()
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title2.bold()).monospacedDigit()
        }
    }

    // MARK: - Exercise

    private func exercise(_ paragraph: WordsDrillParagraph) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack {
                Text(paragraph.title).font(.headline)
                Text(paragraph.difficulty.title)
                    .font(.caption.bold())
                    .padding(.horizontal, 8).padding(.vertical, 3)
                    .background(TOEFLTheme.aqua.opacity(0.15), in: Capsule())
                Spacer()
                if let attempt = store.attempt(for: paragraph) {
                    Text("Best \(attempt.bestCorrect)/10 · \(attempt.attempts) attempt\(attempt.attempts == 1 ? "" : "s")")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }
            Text("Fill in the missing letters in the paragraph.").font(.subheadline).foregroundStyle(.secondary)
            if let results {
                CheckedParagraphView(paragraph: paragraph, entries: entries, results: results)
            } else {
                FlowLayout(spacing: 6, lineSpacing: 12) {
                    ForEach(Array(CTestParagraph(paragraph.passage).pieces.enumerated()), id: \.offset) { _, piece in
                        switch piece {
                        case .text(let word):
                            Text(word).font(.title3)
                        case .blank(let index, let prefix, let missing, let suffix):
                            HStack(spacing: 0) {
                                Text(prefix).font(.title3)
                                input(index: index, missing: missing, paragraph: paragraph)
                                if !suffix.isEmpty { Text(suffix).font(.title3) }
                            }
                            .accessibilityLabel("Blank \(index + 1): \(prefix), \(missing) missing letters")
                        }
                    }
                }
            }
            HStack(spacing: 12) {
                if results == nil {
                    Button("Check Answers") { check(paragraph) }
                        .buttonStyle(TOEFLPrimaryButtonStyle())
                        .keyboardShortcut(.return, modifiers: .command)
                    Button("Skip") { load(store.next(after: paragraph.id, difficulty: level.difficulty)) }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                } else {
                    Button("Next Paragraph") { load(store.next(after: paragraph.id, difficulty: level.difficulty)) }
                        .buttonStyle(TOEFLPrimaryButtonStyle())
                        .keyboardShortcut(.return, modifiers: .command)
                    Button("Try Again") { load(paragraph) }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                }
                Spacer()
                Text(results == nil ? "Return moves to the next blank · ⌘Return checks" : "⌘Return for the next paragraph")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
        .toeflCard()
    }

    private func input(index: Int, missing: Int, paragraph: WordsDrillParagraph) -> some View {
        TextField(String(repeating: "_", count: missing), text: Binding(
            get: { entries.indices.contains(index) ? entries[index] : "" },
            set: { if entries.indices.contains(index) { entries[index] = String($0.filter { $0.isLetter || $0 == "'" }.prefix(missing + 2)) } }
        ))
        .textFieldStyle(.plain)
        .font(.title3.monospaced())
        .frame(width: CGFloat(missing) * 15 + 12)
        .padding(.vertical, 2).padding(.horizontal, 3)
        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 4))
        .overlay(RoundedRectangle(cornerRadius: 4).stroke(TOEFLTheme.action.opacity(0.5)))
        .focused($focused, equals: index)
        .onSubmit { focused = index + 1 < paragraph.blanks.count ? index + 1 : nil }
        #if os(iOS)
        .textInputAutocapitalization(.never)
        .autocorrectionDisabled()
        #endif
    }

    // MARK: - History and review

    private var historyList: some View {
        let records = showsAllHistory ? store.history : Array(store.history.prefix(8))
        return VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Answer history").font(.headline)
                Spacer()
                if store.history.count > 8 {
                    Button(showsAllHistory ? "Show recent" : "Show all \(store.history.count)") { showsAllHistory.toggle() }
                        .buttonStyle(.borderless)
                }
            }
            Text("Open an attempt to see your answers and the explanation for every word.")
                .font(.caption).foregroundStyle(.secondary)
            ForEach(records) { record in
                Button { reviewing = record } label: {
                    HStack(spacing: 12) {
                        Text("\(record.correct)/10")
                            .font(.headline.monospacedDigit())
                            .foregroundStyle(record.correct >= 8 ? TOEFLTheme.teal : (record.correct >= 5 ? TOEFLTheme.gold : .red))
                            .frame(width: 52, alignment: .leading)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(record.paragraph?.title ?? "Paragraph").font(.subheadline.weight(.semibold))
                            Text("\(record.paragraph?.difficulty.title ?? "") · \(record.date.formatted(date: .abbreviated, time: .shortened))")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        if record.entries == nil {
                            Text("Score only").font(.caption).foregroundStyle(.secondary)
                        }
                        Image(systemName: "chevron.right").font(.caption).foregroundStyle(.secondary)
                    }
                    .padding(10)
                    .contentShape(Rectangle())
                    .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                }
                .buttonStyle(.plain)
            }
        }
        .toeflCard()
    }

    private var reviewList: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Words to review").font(.headline)
            Text("Words you missed, most frequent first. Select one to open the attempt where you missed it. A word leaves the list after you get it right twice.")
                .font(.caption).foregroundStyle(.secondary)
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 220), spacing: 10)], alignment: .leading, spacing: 10) {
                ForEach(store.wordsToReview.prefix(24)) { entry in
                    let miss = store.latestMiss(of: entry.word)
                    Button { reviewing = miss } label: {
                        VStack(alignment: .leading, spacing: 2) {
                            HStack(spacing: 6) {
                                Text(entry.word).font(.headline.monospaced())
                                if let pos = entry.partOfSpeech { Text(pos).font(.caption.italic()).foregroundStyle(.secondary) }
                            }
                            if let gloss = entry.gloss { Text(gloss).font(.caption) }
                        }
                        .padding(10)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .contentShape(Rectangle())
                        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                    }
                    .buttonStyle(.plain)
                    .disabled(miss == nil)
                }
            }
        }
        .toeflCard()
    }

    // MARK: - Actions

    private func load(_ next: WordsDrillParagraph?) {
        paragraph = next
        entries = Array(repeating: "", count: next?.blanks.count ?? 0)
        results = nil
        focused = 0
    }

    private func check(_ paragraph: WordsDrillParagraph) {
        let checked = paragraph.blanks.enumerated().map { index, blank in
            TOEFLAnswerMatcher.isCorrect(question: blank, answer: TOEFLAnswer(textEntry: entries[index])) ?? false
        }
        results = checked
        focused = nil
        store.record(paragraph, entries: entries, results: checked)
    }
}
