import SwiftUI
import Combine
#if os(macOS)
import AppKit
#endif

/// Reading question types, with the labels shown in practice.
enum ReadingQuestionType: String, CaseIterable, Identifiable {
    case vocabulary, factual, negative_factual, detail, inference, application, purpose, relationship, paraphrase, main_idea, attitude
    var id: String { rawValue }
    var title: String {
        switch self {
        case .vocabulary: "Vocabulary 詞彙"
        case .factual: "Factual 細節"
        case .negative_factual: "NOT/EXCEPT 否定細節"
        case .detail: "Detail 細節"
        case .inference: "Inference 推論"
        case .application: "Application 規則應用"
        case .purpose: "Purpose 目的"
        case .relationship: "Relationship 段落關係"
        case .paraphrase: "Paraphrase 句意改寫"
        case .main_idea: "Main idea 主旨"
        case .attitude: "Attitude 作者態度"
        }
    }
    var tip: String {
        switch self {
        case .vocabulary: "用整句意思判斷，答案是「在這句話裡」的意思，常不是最常見的字義；把選項代回句子檢查。"
        case .factual, .detail: "先在文中找到對應句，再比對選項；正確答案通常是改寫，照抄原文字詞的選項要小心。"
        case .negative_factual: "NOT/EXCEPT：逐一在文中確認三個「有提到」的選項，剩下的才是答案。"
        case .inference: "答案不會直接寫在文中，但必須能由兩句以上的資訊推出；過度延伸或太絕對的選項通常錯。"
        case .application: "把規則套到題目的人物或情境，注意例外條件、日期與金額，必要時一步步計算。"
        case .purpose: "問作者「為什麼提到」某內容：看它前後支持哪個論點，而不是它本身的內容。"
        case .relationship: "判斷段落之間的關係：舉例、反駁、修正前面的觀點、提出原因或結果。"
        case .paraphrase: "找保留原句主要資訊與邏輯關係（因果、對比、讓步）的選項；改變關係或漏掉重點的都錯。"
        case .main_idea: "主旨要涵蓋全文，不能只講某一段的細節。"
        case .attitude: "注意作者用的評價字與保留語氣（may, appears, however），選擇程度相符的選項。"
        }
    }
}

/// One Read in Daily Life or Read an Academic Passage set.
struct ReadingDrillSet: Identifiable {
    let id: String
    let task: TOEFLTaskType
    let difficulty: TOEFLDifficulty
    let passage: String
    let questions: [TOEFLQuestion]

    var title: String { passage.components(separatedBy: "\n").first ?? "Passage" }
    var body: String {
        let lines = passage.components(separatedBy: "\n")
        return lines.dropFirst().joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    static let all: [ReadingDrillSet] = {
        let items = TOEFLQuestionBank.allQuestions.filter { $0.taskType == .readDailyLife || $0.taskType == .readAcademicPassage }
        var order: [String] = []
        var grouped: [String: [TOEFLQuestion]] = [:]
        for item in items {
            let key = item.setID ?? item.id
            if grouped[key] == nil { order.append(key) }
            grouped[key, default: []].append(item)
        }
        return order.compactMap { key in
            guard let questions = grouped[key]?.sorted(by: { ($0.setIndex ?? 0) < ($1.setIndex ?? 0) }), let first = questions.first else { return nil }
            return ReadingDrillSet(id: key, task: first.taskType, difficulty: first.difficulty, passage: first.passage ?? "", questions: questions)
        }
    }()
}

/// Saved Reading practice: every checked set with the options chosen.
@MainActor
final class ReadingDrillStore: ObservableObject {
    struct Record: Codable, Identifiable, Equatable {
        let id: UUID
        let setID: String
        let date: Date
        let choices: [String?]
        let results: [Bool]
        var correct: Int { results.filter { $0 }.count }
        var set: ReadingDrillSet? { ReadingDrillSet.all.first { $0.id == setID } }
    }

    struct TypeAccuracy: Identifiable {
        let type: ReadingQuestionType
        let correct: Int
        let total: Int
        var id: String { type.rawValue }
        var accuracy: Double { total == 0 ? 0 : Double(correct) / Double(total) }
    }

    @Published private var records: [Record]
    private let key = "toefl.reading-drill.v1"

    init() {
        if let data = UserDefaults.standard.data(forKey: key), let decoded = try? JSONDecoder().decode([Record].self, from: data) {
            records = decoded
        } else {
            records = []
        }
    }

    var history: [Record] { records.reversed() }
    var practicedSets: Int { Set(records.map(\.setID)).count }

    private var latestBySet: [String: Record] {
        var latest: [String: Record] = [:]
        for record in records { latest[record.setID] = record }
        return latest
    }

    var recentAccuracy: Double? {
        let latest = latestBySet.values
        let total = latest.reduce(0) { $0 + $1.results.count }
        return total == 0 ? nil : Double(latest.reduce(0) { $0 + $1.correct }) / Double(total)
    }

    /// Accuracy per question type from each set's most recent attempt.
    var accuracyByType: [TypeAccuracy] {
        var sums: [ReadingQuestionType: (Int, Int)] = [:]
        for record in latestBySet.values {
            guard let set = record.set else { continue }
            for (question, isCorrect) in zip(set.questions, record.results) {
                guard let type = question.questionType.flatMap(ReadingQuestionType.init(rawValue:)) else { continue }
                let current = sums[type] ?? (0, 0)
                sums[type] = (current.0 + (isCorrect ? 1 : 0), current.1 + 1)
            }
        }
        return ReadingQuestionType.allCases.compactMap { type in sums[type].map { TypeAccuracy(type: type, correct: $0.0, total: $0.1) } }
    }

    /// The two lowest-accuracy question types with at least four answered questions.
    var weakTypes: [ReadingQuestionType] {
        Array(accuracyByType.filter { $0.total >= 4 }.sorted { $0.accuracy < $1.accuracy }.prefix(2).map(\.type))
    }

    func record(_ set: ReadingDrillSet, choices: [String?], results: [Bool]) {
        records.append(Record(id: UUID(), setID: set.id, date: .now, choices: choices, results: results))
        if records.count > 2_000 { records.removeFirst(records.count - 2_000) }
        if let data = try? JSONEncoder().encode(records) { UserDefaults.standard.set(data, forKey: key) }
    }

    func lastResult(for set: ReadingDrillSet) -> Record? { latestBySet[set.id] }

    /// Unpracticed sets first (those with the most questions of `focus` types first), then the
    /// weakest and least recently practiced.
    func next(after current: String?, task: TOEFLTaskType?, difficulty: TOEFLDifficulty?, focus: Set<ReadingQuestionType>?) -> ReadingDrillSet? {
        let pool = ReadingDrillSet.all.filter {
            $0.id != current && (task == nil || $0.task == task) && (difficulty == nil || $0.difficulty == difficulty)
        }
        let latest = latestBySet
        func focusCount(_ set: ReadingDrillSet) -> Int {
            guard let focus else { return 0 }
            return set.questions.filter { $0.questionType.flatMap(ReadingQuestionType.init(rawValue:)).map(focus.contains) == true }.count
        }
        let fresh = pool.filter { latest[$0.id] == nil }.shuffled()
        if let pick = fresh.max(by: { focusCount($0) < focusCount($1) }) { return pick }
        return pool.min { lhs, rhs in
            let a = latest[lhs.id]!, b = latest[rhs.id]!
            let left = (Double(a.correct) / Double(max(1, a.results.count)), a.date)
            let right = (Double(b.correct) / Double(max(1, b.results.count)), b.date)
            return left < right
        }
    }
}

/// Answers one set and shows right/wrong, the explanation, and the question type for each question.
struct ReadingSetView: View {
    let set: ReadingDrillSet
    @Binding var choices: [String?]
    let results: [Bool]?

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 10) {
                Text(set.title).font(.title3.bold())
                Text(set.body)
                    .font(.system(size: 17, design: .serif))
                    .lineSpacing(6)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .toeflCard()
            ForEach(Array(set.questions.enumerated()), id: \.element.id) { index, question in
                questionCard(index: index, question: question)
            }
        }
    }

    private func questionCard(index: Int, question: TOEFLQuestion) -> some View {
        let checked = results.map { $0.indices.contains(index) ? $0[index] : false }
        let type = question.questionType.flatMap(ReadingQuestionType.init(rawValue:))
        return VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .firstTextBaseline) {
                Text("\(index + 1). \(question.prompt)").font(.headline).fixedSize(horizontal: false, vertical: true)
                Spacer()
                if let checked {
                    Image(systemName: checked ? "checkmark.circle.fill" : "xmark.circle.fill")
                        .font(.title3).foregroundStyle(checked ? TOEFLTheme.teal : .red)
                }
            }
            ForEach(question.choices, id: \.id) { choice in
                let selected = choices.indices.contains(index) && choices[index] == choice.id
                let isKey = choice.id == question.correctChoiceID
                Button {
                    if results == nil, choices.indices.contains(index) { choices[index] = choice.id }
                } label: {
                    HStack(alignment: .top, spacing: 10) {
                        Image(systemName: selected ? "largecircle.fill.circle" : "circle")
                            .foregroundStyle(selected ? TOEFLTheme.action : .secondary)
                        Text(choice.text).fixedSize(horizontal: false, vertical: true)
                        Spacer(minLength: 0)
                    }
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(background(selected: selected, isKey: isKey), in: RoundedRectangle(cornerRadius: 8))
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
            if results != nil {
                VStack(alignment: .leading, spacing: 6) {
                    if let type {
                        Text(type.title).font(.caption.bold()).foregroundStyle(TOEFLTheme.teal)
                    }
                    Text(question.explanation).font(.subheadline).fixedSize(horizontal: false, vertical: true)
                    if let type, checked == false {
                        Label(type.tip, systemImage: "lightbulb").font(.caption).foregroundStyle(.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(10)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
            }
        }
        .toeflCard()
    }

    private func background(selected: Bool, isKey: Bool) -> Color {
        guard results != nil else { return selected ? TOEFLTheme.action.opacity(0.12) : TOEFLTheme.input }
        if isKey { return TOEFLTheme.teal.opacity(0.18) }
        if selected { return Color.red.opacity(0.14) }
        return TOEFLTheme.input
    }
}

/// Reading practice with immediate feedback, accuracy by question type, and an answer history.
struct ReadingDrillView: View {
    enum TaskFilter: String, CaseIterable, Identifiable {
        case all = "All tasks", daily = "Daily Life", academic = "Academic Passage"
        var id: String { rawValue }
        var task: TOEFLTaskType? {
            switch self {
            case .all: nil
            case .daily: .readDailyLife
            case .academic: .readAcademicPassage
            }
        }
    }

    @StateObject private var store = ReadingDrillStore()
    @State private var taskFilter: TaskFilter = .all
    @State private var level = "all"
    @State private var focusWeak = false
    @State private var current: ReadingDrillSet?
    @State private var choices: [String?] = []
    @State private var results: [Bool]?
    @State private var reviewing: ReadingDrillStore.Record?
    @State private var showsAllHistory = false

    var body: some View {
        ScrollView { content }
            .navigationTitle("Reading Practice")
            .onAppear { if current == nil { load(nextSet(after: nil)) } }
            .onChange(of: taskFilter) { _, _ in load(nextSet(after: nil)) }
            .onChange(of: level) { _, _ in load(nextSet(after: nil)) }
            .onChange(of: focusWeak) { _, _ in load(nextSet(after: nil)) }
            .sheet(item: $reviewing) { record in reviewSheet(record) }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            if let current {
                HStack {
                    Text(current.task == .readDailyLife ? "Read in Daily Life" : "Read an Academic Passage").font(.headline)
                    Text(current.difficulty.title).font(.caption.bold())
                        .padding(.horizontal, 8).padding(.vertical, 3)
                        .background(TOEFLTheme.aqua.opacity(0.15), in: Capsule())
                    Spacer()
                    if let last = store.lastResult(for: current) {
                        Text("Last time \(last.correct)/\(last.results.count)").font(.caption).foregroundStyle(.secondary)
                    }
                }
                ReadingSetView(set: current, choices: $choices, results: results)
                actions(current)
            }
            if !store.history.isEmpty { historyList }
        }
        .frame(maxWidth: 1000, alignment: .leading)
        .padding(30)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Reading practice")
                        .font(.system(size: 30, weight: .bold, design: .rounded))
                        .foregroundStyle(TOEFLTheme.navy)
                    Text("Answer one passage at a time, then check it to see the correct answers, the explanations, and the question type of each item.")
                        .foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 8) {
                    Picker("Task", selection: $taskFilter) {
                        ForEach(TaskFilter.allCases) { Text($0.rawValue).tag($0) }
                    }
                    .pickerStyle(.segmented).frame(width: 360)
                    HStack {
                        Picker("Level", selection: $level) {
                            Text("All levels").tag("all")
                            Text("Intermediate").tag(TOEFLDifficulty.intermediate.rawValue)
                            Text("Advanced").tag(TOEFLDifficulty.advanced.rawValue)
                        }
                        .frame(width: 200)
                        Toggle("Focus on weak types", isOn: $focusWeak)
                    }
                }
            }
            HStack(spacing: 22) {
                stat("Sets practiced", "\(store.practicedSets) / \(ReadingDrillSet.all.count)")
                stat("Recent accuracy", store.recentAccuracy.map { "\(Int(($0 * 100).rounded()))%" } ?? "—")
                stat("Attempts", "\(store.history.count)")
            }
            typeAccuracy
        }
        .toeflCard()
    }

    @ViewBuilder
    private var typeAccuracy: some View {
        let types = store.accuracyByType.sorted { $0.accuracy < $1.accuracy }
        if !types.isEmpty {
            let weak = Set(store.weakTypes)
            VStack(alignment: .leading, spacing: 6) {
                Text("Accuracy by question type (latest attempt of each set)").font(.caption).foregroundStyle(.secondary)
                FlowLayout(spacing: 8, lineSpacing: 8) {
                    ForEach(types) { entry in
                        HStack(spacing: 6) {
                            Text(entry.type.title).font(.caption.weight(.semibold))
                            Text("\(Int((entry.accuracy * 100).rounded()))%").font(.caption.bold().monospacedDigit())
                                .foregroundStyle(entry.accuracy >= 0.8 ? TOEFLTheme.teal : (entry.accuracy >= 0.65 ? TOEFLTheme.gold : .red))
                            Text("· \(entry.total)").font(.caption2).foregroundStyle(.secondary)
                        }
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background(weak.contains(entry.type) ? Color.red.opacity(0.08) : TOEFLTheme.input, in: Capsule())
                        .overlay(Capsule().stroke(weak.contains(entry.type) ? Color.red.opacity(0.35) : Color.clear))
                    }
                }
                if let first = store.weakTypes.first {
                    Label("Weakest: \(first.title) — \(first.tip)", systemImage: "lightbulb")
                        .font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
            }
        }
    }

    private func stat(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title2.bold()).monospacedDigit()
        }
    }

    private func actions(_ set: ReadingDrillSet) -> some View {
        HStack(spacing: 12) {
            if let results {
                Text("\(results.filter { $0 }.count) / \(results.count) correct")
                    .font(.title2.bold())
                    .foregroundStyle(results.allSatisfy { $0 } ? TOEFLTheme.teal : (Double(results.filter { $0 }.count) / Double(results.count) >= 0.6 ? TOEFLTheme.gold : .red))
                Spacer()
                Button("Try Again") { load(set) }.buttonStyle(TOEFLSecondaryButtonStyle())
                Button("Next Set") { load(nextSet(after: set.id)) }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
                    .keyboardShortcut(.return, modifiers: .command)
            } else {
                Text("\(choices.compactMap { $0 }.count) of \(set.questions.count) answered").font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button("Skip") { load(nextSet(after: set.id)) }.buttonStyle(TOEFLSecondaryButtonStyle())
                Button("Check Answers") { check(set) }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
                    .keyboardShortcut(.return, modifiers: .command)
            }
        }
        .toeflCard()
    }

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
            ForEach(records) { record in
                Button { reviewing = record } label: {
                    HStack(spacing: 12) {
                        Text("\(record.correct)/\(record.results.count)").font(.headline.monospacedDigit())
                            .foregroundStyle(record.results.allSatisfy { $0 } ? TOEFLTheme.teal : TOEFLTheme.gold)
                            .frame(width: 44, alignment: .leading)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(record.set?.title ?? "Set").font(.subheadline.weight(.semibold))
                            Text("\(record.set?.task == .readDailyLife ? "Daily Life" : "Academic") · \(record.date.formatted(date: .abbreviated, time: .shortened))")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Image(systemName: "chevron.right").font(.caption).foregroundStyle(.secondary)
                    }
                    .padding(10).contentShape(Rectangle())
                    .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                }
                .buttonStyle(.plain)
            }
        }
        .toeflCard()
    }

    private func reviewSheet(_ record: ReadingDrillStore.Record) -> some View {
        VStack(spacing: 0) {
            HStack {
                Text(record.date.formatted(date: .abbreviated, time: .shortened)).font(.headline)
                Spacer()
                if let set = record.set {
                    Button("Practice Again") { reviewing = nil; load(set) }.buttonStyle(TOEFLSecondaryButtonStyle())
                }
                Button("Done") { reviewing = nil }.buttonStyle(TOEFLPrimaryButtonStyle()).keyboardShortcut(.cancelAction)
            }
            .padding(20)
            Divider()
            ScrollView {
                if let set = record.set {
                    ReadingSetView(set: set, choices: .constant(record.choices), results: record.results).padding(24)
                }
            }
        }
        .frame(minWidth: 760, idealWidth: 920, minHeight: 600, idealHeight: 820)
        .background(TOEFLTheme.canvas)
    }

    #if DEBUG
    /// Renders a checked academic set with two wrong answers, for `-renderReadingDrill <directory>`.
    static func renderSnapshot(to directory: URL) {
        guard let set = ReadingDrillSet.all.first(where: { $0.id.hasPrefix("ap5") }) else { return }
        var choices: [String?] = set.questions.map(\.correctChoiceID)
        for index in [0, 2] {
            choices[index] = set.questions[index].choices.first { $0.id != set.questions[index].correctChoiceID }?.id
        }
        let results = set.questions.enumerated().map { index, question in choices[index] == question.correctChoiceID }
        let view = ReadingSetView(set: set, choices: .constant(choices), results: results).padding(30)
        let renderer = ImageRenderer(content: view.frame(width: 1000).background(TOEFLTheme.canvas))
        renderer.scale = 1
        #if os(macOS)
        if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
            try? png.write(to: directory.appendingPathComponent("reading-checked.png"))
        }
        #endif
    }
    #endif

    // MARK: - Actions

    private func nextSet(after current: String?) -> ReadingDrillSet? {
        let focus = focusWeak && !store.weakTypes.isEmpty ? Set(store.weakTypes) : nil
        let difficulty = TOEFLDifficulty(rawValue: level)
        return store.next(after: current, task: taskFilter.task, difficulty: difficulty, focus: focus)
            ?? store.next(after: current, task: taskFilter.task, difficulty: nil, focus: focus)
            ?? ReadingDrillSet.all.first
    }

    private func load(_ set: ReadingDrillSet?) {
        current = set
        choices = Array(repeating: nil, count: set?.questions.count ?? 0)
        results = nil
    }

    private func check(_ set: ReadingDrillSet) {
        let checked = set.questions.enumerated().map { index, question in choices[index] == question.correctChoiceID }
        results = checked
        store.record(set, choices: choices, results: checked)
    }
}
