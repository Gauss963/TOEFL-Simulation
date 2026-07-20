import SwiftUI

private enum TOEFLVocabularyStudyMode: String, CaseIterable, Identifiable {
    case browse = "Browse"
    case flashCards = "Flash Cards"

    var id: String { rawValue }
}

struct VocabularyView: View {
    @State private var searchText = ""
    @State private var revealed: Set<String> = []
    @State private var selectedSource = "All sources"
    @State private var studyMode: TOEFLVocabularyStudyMode = .browse

    init() {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("-openFlashcards") {
            _studyMode = State(initialValue: .flashCards)
        }
        #endif
    }

    private var filtered: [VocabularyEntry] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return TOEFLStudyBank.words.filter { entry in
            let matchesSource = selectedSource == "All sources" || entry.sources.contains(selectedSource)
            let matchesQuery = query.isEmpty
                || entry.word.lowercased().contains(query)
                || entry.definition.lowercased().contains(query)
                || entry.chinese.contains(query)
                || entry.synonyms.contains(where: { $0.lowercased().contains(query) })
            return matchesSource && matchesQuery
        }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                VStack(alignment: .leading, spacing: 7) {
                    Text("High-frequency vocabulary lab")
                        .font(.system(size: 30, weight: .bold, design: .rounded))
                        .foregroundStyle(TOEFLTheme.navy)
                    Text("Browse the authorized offline word bank or work through shuffled flash-card sessions with persistent mastery progress.")
                        .foregroundStyle(.secondary)
                }

                Picker("Study mode", selection: $studyMode) {
                    ForEach(TOEFLVocabularyStudyMode.allCases) { mode in
                        Text(mode.rawValue).tag(mode)
                    }
                }
                .pickerStyle(.segmented)
                .frame(maxWidth: 360)

                if studyMode == .browse {
                    ViewThatFits(in: .horizontal) {
                        HStack(spacing: 12) { searchField; sourcePicker }
                        VStack(spacing: 10) { searchField; sourcePicker }
                    }

                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 285), spacing: 14)], spacing: 14) {
                        ForEach(filtered) { entry in
                            vocabularyCard(entry)
                        }
                    }

                    Text("Entries retain their authorized source tags after deduplication. Chinese definitions use Taiwan Traditional Chinese. English glosses and examples are compacted for card display; consult the authorized source material for complete study notes.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .padding(.top, 6)
                } else {
                    TOEFLFlashcardStudyView(words: TOEFLStudyBank.words)
                }
            }
            .frame(maxWidth: 1050, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Vocabulary")
    }

    private var searchField: some View {
        HStack {
            Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
            TextField("Search words, meanings, or synonyms", text: $searchText)
                .textFieldStyle(.plain)
            Text("\(filtered.count) words")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.secondary)
        }
        .padding(12)
        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
    }

    private var sourcePicker: some View {
        Picker("Source", selection: $selectedSource) {
            Text("All sources").tag("All sources")
            ForEach(TOEFLStudyBank.sourceOptions, id: \.self) { source in
                Text(source).tag(source)
            }
        }
        .pickerStyle(.menu)
        .frame(minWidth: 190)
        .padding(.horizontal, 8)
        .padding(.vertical, 5)
        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))
    }

    private func vocabularyCard(_ entry: VocabularyEntry) -> some View {
        Button {
            withAnimation(.easeInOut(duration: 0.18)) {
                if revealed.contains(entry.id) { revealed.remove(entry.id) }
                else { revealed.insert(entry.id) }
            }
        } label: {
            VStack(alignment: .leading, spacing: 9) {
                HStack(alignment: .firstTextBaseline) {
                    Text(entry.word)
                        .font(.title2.bold())
                        .foregroundStyle(TOEFLTheme.navy)
                    if !entry.pronunciation.isEmpty {
                        Text(entry.pronunciation)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Text(entry.partOfSpeech)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Spacer()
                    if entry.isHighFrequency {
                        Image(systemName: "flame.fill")
                            .foregroundStyle(TOEFLTheme.gold)
                            .accessibilityLabel("High frequency")
                    }
                    Image(systemName: revealed.contains(entry.id) ? "eye.fill" : "eye.slash")
                        .foregroundStyle(TOEFLTheme.blue)
                }
                if revealed.contains(entry.id) {
                    if !entry.definition.isEmpty {
                        Text(entry.definition)
                            .font(.body.weight(.medium))
                            .foregroundStyle(.primary)
                    }
                    Text(entry.chinese)
                        .foregroundStyle(TOEFLTheme.teal)
                    if !entry.synonyms.isEmpty {
                        Text(entry.synonyms.joined(separator: " · "))
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(.secondary)
                    }
                    Text(entry.example)
                        .font(.subheadline)
                        .italic()
                        .foregroundStyle(.secondary)
                    Text(entry.sources.joined(separator: " · "))
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                } else {
                    Text("Tap to reveal meaning")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                    Spacer(minLength: 52)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 165, alignment: .topLeading)
            .toeflCard()
        }
        .buttonStyle(.plain)
    }
}

struct ResourcesView: View {
    private let localSources = [
        "The Official Guide to the TOEFL iBT Test",
        "Official TOEFL iBT Tests, Volumes 1 and 2",
        "TOEFL iBT Premium with 8 Online Practice Tests",
        "Princeton Review TOEFL iBT Prep with Audio-Listening Tracks",
        "Essential Words for the TOEFL (authorized editions)",
        "托福中階500單字、托福進階500單字、高階500英文難字",
        "TOEFL 托福分類字彙、Toefl 詞彙紅寶書",
        "TOEFL Idioms Quiz Book",
        "Authorized reading-comprehension collection and practice test"
    ]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                Text("Official resources")
                    .font(.system(size: 30, weight: .bold, design: .rounded))
                    .foregroundStyle(TOEFLTheme.navy)
                Text("The app uses the post-January 21, 2026 TOEFL iBT blueprint. Older authorized books inform skill coverage, interface conventions, and study vocabulary; their retired task structure is not presented as the current exam.")
                    .foregroundStyle(.secondary)

                HStack(spacing: 18) {
                    Label("\(TOEFLStudyBank.words.count) vocabulary entries", systemImage: "text.book.closed.fill")
                    Label("Offline study cards", systemImage: "rectangle.stack.fill")
                }
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(TOEFLTheme.teal)

                VStack(alignment: .leading, spacing: 12) {
                    Text("Current official references").font(.title2.bold())
                    resourceLink("ETS: Test content and structure", "https://www.ets.org/toefl/test-takers/ibt/about/content.html")
                    resourceLink("ETS: 2026 blueprint and specifications", "https://www.ets.org/content/dam/ets-india/pdfs/toefl/toefl-ibt-test-specifications-2026.pdf")
                    resourceLink("ETS: Understanding 1–6 scores", "https://www.ets.org/toefl/test-takers/ibt/scores/understand-scores.html")
                    resourceLink("ETS: Reading task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/reading.html")
                    resourceLink("ETS: Listening task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/listening.html")
                    resourceLink("ETS: Writing task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/writing.html")
                    resourceLink("ETS: Speaking task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/speaking.html")
                }
                .toeflCard()

                VStack(alignment: .leading, spacing: 12) {
                    Text("Authorized local reference library").font(.title2.bold())
                    ForEach(localSources, id: \.self) { source in
                        Label(source, systemImage: "checkmark.seal.fill")
                            .foregroundStyle(TOEFLTheme.navy)
                    }
                    Text("The large source files remain outside the app bundle and Git repository.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
                .toeflCard()

                Text("TOEFL® is a registered trademark of ETS. This independent simulator is not endorsed or approved by ETS.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: 950, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Official Resources")
    }

    private func resourceLink(_ title: String, _ url: String) -> some View {
        Link(destination: URL(string: url)!) {
            Label(title, systemImage: "arrow.up.right.square")
        }
    }
}
