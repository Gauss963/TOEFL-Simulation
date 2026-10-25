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

/// Saved drill progress: per-paragraph results and the words most often missed.
@MainActor
final class WordsDrillStore: ObservableObject {
    struct Attempt: Codable, Equatable {
        var attempts: Int
        var bestCorrect: Int
        var lastCorrect: Int
        var lastDate: Date
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
    }

    @Published private var saved: Saved
    private let key = "toefl.words-drill.v1"

    init() {
        if let data = UserDefaults.standard.data(forKey: key), let decoded = try? JSONDecoder().decode(Saved.self, from: data) {
            saved = decoded
        } else {
            saved = Saved()
        }
    }

    func attempt(for paragraph: WordsDrillParagraph) -> Attempt? { saved.attempts[paragraph.id] }

    var completedCount: Int { saved.attempts.count }
    var blanksAnswered: Int { saved.attempts.values.reduce(0) { $0 + $1.attempts * 10 } }

    /// Accuracy across each paragraph's most recent attempt.
    var recentAccuracy: Double? {
        guard !saved.attempts.isEmpty else { return nil }
        return Double(saved.attempts.values.reduce(0) { $0 + $1.lastCorrect }) / Double(saved.attempts.count * 10)
    }

    var wordsToReview: [MissedWord] {
        saved.missed.values.sorted { ($0.misses, $0.lastMissed) > ($1.misses, $1.lastMissed) }
    }

    func record(_ paragraph: WordsDrillParagraph, results: [Bool]) {
        let correct = results.filter { $0 }.count
        var attempt = saved.attempts[paragraph.id] ?? Attempt(attempts: 0, bestCorrect: 0, lastCorrect: 0, lastDate: .now)
        attempt.attempts += 1
        attempt.bestCorrect = max(attempt.bestCorrect, correct)
        attempt.lastCorrect = correct
        attempt.lastDate = .now
        saved.attempts[paragraph.id] = attempt
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
        if let data = try? JSONEncoder().encode(saved) { UserDefaults.standard.set(data, forKey: key) }
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
    @FocusState private var focused: Int?

    var body: some View {
        ScrollView { content }
        .navigationTitle("Complete the Words")
        .onAppear { if paragraph == nil { load(store.next(after: nil, difficulty: level.difficulty)) } }
        .onChange(of: level) { _, newLevel in load(store.next(after: nil, difficulty: newLevel.difficulty)) }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            if let paragraph {
                exercise(paragraph)
                if let results { feedback(paragraph, results: results) }
            }
            if !store.wordsToReview.isEmpty { reviewList }
        }
        .frame(maxWidth: 1000, alignment: .leading)
        .padding(30)
    }

    #if DEBUG
    /// Renders the page with a paragraph filled in and checked, for `-renderWordsDrill <directory>`.
    static func renderSnapshots(to directory: URL) {
        guard let paragraph = WordsDrillParagraph.all.first(where: { $0.blanks.first?.blankReason != nil }) else { return }
        // Two wrong entries, one blank left empty, the rest correct.
        var entries = paragraph.blanks.map { $0.acceptedTextAnswers.first ?? "" }
        entries[1] = "xx"; entries[4] = ""; entries[7] = String(entries[7].dropLast())
        for (name, checked) in [("drill-answering", false), ("drill-checked", true)] {
            var view = WordsDrillView()
            view._paragraph = State(initialValue: paragraph)
            view._entries = State(initialValue: entries)
            if checked {
                view._results = State(initialValue: paragraph.blanks.enumerated().map { index, blank in
                    TOEFLAnswerMatcher.isCorrect(question: blank, answer: TOEFLAnswer(textEntry: entries[index])) ?? false
                })
            }
            let renderer = ImageRenderer(content: view.content.frame(width: 1000).background(TOEFLTheme.canvas))
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                try? png.write(to: directory.appendingPathComponent("\(name).png"))
            }
            #endif
        }
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
            FlowLayout(spacing: 6, lineSpacing: 12) {
                ForEach(Array(CTestParagraph(paragraph.passage).pieces.enumerated()), id: \.offset) { _, piece in
                    switch piece {
                    case .text(let word):
                        Text(word).font(.title3)
                    case .blank(let index, let prefix, let missing, let suffix):
                        HStack(spacing: 0) {
                            blank(index: index, prefix: prefix, missing: missing, paragraph: paragraph)
                            if !suffix.isEmpty { Text(suffix).font(.title3) }
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

    private func blank(index: Int, prefix: String, missing: Int, paragraph: WordsDrillParagraph) -> some View {
        let checked = results.map { $0.indices.contains(index) ? $0[index] : false }
        let answer = paragraph.blanks.indices.contains(index) ? paragraph.blanks[index].acceptedTextAnswers.first ?? "" : ""
        return HStack(spacing: 0) {
            Text(prefix).font(.title3)
            if let checked, !checked {
                // Wrong: the typed letters struck through, then the correct letters.
                HStack(spacing: 4) {
                    if !entries[index].isEmpty {
                        Text(entries[index]).font(.title3.monospaced()).strikethrough().foregroundStyle(.red)
                    }
                    Text(answer).font(.title3.monospaced().bold()).foregroundStyle(TOEFLTheme.teal)
                }
                .padding(.horizontal, 3).padding(.vertical, 2)
                .background(Color.red.opacity(0.1), in: RoundedRectangle(cornerRadius: 4))
                .overlay(RoundedRectangle(cornerRadius: 4).stroke(Color.red.opacity(0.7)))
            } else if checked == true {
                Text(entries[index]).font(.title3.monospaced().bold()).foregroundStyle(TOEFLTheme.teal)
                    .padding(.horizontal, 3).padding(.vertical, 2)
                    .background(TOEFLTheme.teal.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                    .overlay(RoundedRectangle(cornerRadius: 4).stroke(TOEFLTheme.teal.opacity(0.8)))
            } else {
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
        }
        .accessibilityLabel("Blank \(index + 1): \(prefix), \(missing) missing letters")
    }

    // MARK: - Feedback

    private func feedback(_ paragraph: WordsDrillParagraph, results: [Bool]) -> some View {
        let correct = results.filter { $0 }.count
        return VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                Text("\(correct) / 10 correct")
                    .font(.system(size: 28, weight: .bold, design: .rounded))
                    .foregroundStyle(correct >= 8 ? TOEFLTheme.teal : (correct >= 5 ? TOEFLTheme.gold : .red))
                Spacer()
                Text(correct == 10 ? "Perfect paragraph." : "Review the explanations for the missed words.")
                    .font(.subheadline).foregroundStyle(.secondary)
            }
            ForEach(Array(paragraph.blanks.enumerated()), id: \.element.id) { index, blank in
                explanationRow(number: index + 1, blank: blank, isCorrect: results[index], typed: entries[index])
                if index < paragraph.blanks.count - 1 { Divider() }
            }
        }
        .toeflCard()
    }

    private func explanationRow(number: Int, blank: TOEFLQuestion, isCorrect: Bool, typed: String) -> some View {
        let word = blank.sampleAnswer ?? ""
        let missing = blank.acceptedTextAnswers.first ?? ""
        let kept = String(word.dropLast(missing.count))
        return HStack(alignment: .top, spacing: 12) {
            Image(systemName: isCorrect ? "checkmark.circle.fill" : "xmark.circle.fill")
                .font(.title3)
                .foregroundStyle(isCorrect ? TOEFLTheme.teal : .red)
            VStack(alignment: .leading, spacing: 5) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text("\(number).").foregroundStyle(.secondary).monospacedDigit()
                    (Text(kept) + Text(missing).bold().foregroundColor(TOEFLTheme.teal))
                        .font(.title3.monospaced())
                    if let pos = blank.partOfSpeech { Text(pos).font(.subheadline.italic()).foregroundStyle(.secondary) }
                    if let gloss = blank.wordGloss { Text(gloss).font(.subheadline.weight(.semibold)) }
                    if !isCorrect {
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

    private var reviewList: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Words to review").font(.headline)
            Text("Words you missed, most frequent first. A word leaves the list after you get it right twice.")
                .font(.caption).foregroundStyle(.secondary)
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 220), spacing: 10)], alignment: .leading, spacing: 10) {
                ForEach(store.wordsToReview.prefix(24)) { entry in
                    VStack(alignment: .leading, spacing: 2) {
                        HStack(spacing: 6) {
                            Text(entry.word).font(.headline.monospaced())
                            if let pos = entry.partOfSpeech { Text(pos).font(.caption.italic()).foregroundStyle(.secondary) }
                        }
                        if let gloss = entry.gloss { Text(gloss).font(.caption) }
                    }
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
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
        store.record(paragraph, results: checked)
    }
}
