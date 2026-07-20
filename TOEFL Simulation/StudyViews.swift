import SwiftUI

struct VocabularyEntry: Identifiable {
    let word: String
    let partOfSpeech: String
    let definition: String
    let chinese: String
    let example: String
    let source: String
    var id: String { word }
}

enum TOEFLStudyBank {
    static let words: [VocabularyEntry] = [
        .init(word: "adapt", partOfSpeech: "v.", definition: "to change in response to conditions", chinese: "適應；調整", example: "Coastal plants adapt to frequent exposure to salt.", source: "Essential Words for the TOEFL"),
        .init(word: "allocate", partOfSpeech: "v.", definition: "to distribute for a particular purpose", chinese: "分配；配置", example: "The committee allocated additional funds to laboratory safety.", source: "Official Guide vocabulary review"),
        .init(word: "ambiguous", partOfSpeech: "adj.", definition: "open to more than one interpretation", chinese: "模稜兩可的；語意不明的", example: "The ambiguous inscription supports several historical interpretations.", source: "Essential Words for the TOEFL"),
        .init(word: "approximate", partOfSpeech: "adj./v.", definition: "close to the actual value; to estimate", chinese: "大約的；估算", example: "Researchers used tree rings to approximate earlier rainfall levels.", source: "Official Guide vocabulary review"),
        .init(word: "coherent", partOfSpeech: "adj.", definition: "logical, consistent, and easy to follow", chinese: "連貫的；條理清楚的", example: "A coherent response connects evidence to a clear claim.", source: "Writing rubric language"),
        .init(word: "compensate", partOfSpeech: "v.", definition: "to make up for a weakness or loss", chinese: "彌補；補償", example: "Magnetic cues compensate when clouds hide visual landmarks.", source: "Official TOEFL reading themes"),
        .init(word: "consecutive", partOfSpeech: "adj.", definition: "following continuously without interruption", chinese: "連續的", example: "The team collected measurements on five consecutive days.", source: "Essential Words for the TOEFL"),
        .init(word: "constitute", partOfSpeech: "v.", definition: "to form or make up a whole", chinese: "構成；組成", example: "Small businesses constitute most of the town's employers.", source: "Essential Words for the TOEFL"),
        .init(word: "corroborate", partOfSpeech: "v.", definition: "to confirm with additional evidence", chinese: "佐證；證實", example: "A second field survey corroborated the original observation.", source: "Academic reading vocabulary"),
        .init(word: "decline", partOfSpeech: "n./v.", definition: "a decrease; to become smaller or weaker", chinese: "下降；衰退", example: "The population began to decline after its habitat was fragmented.", source: "Official TOEFL reading themes"),
        .init(word: "derive", partOfSpeech: "v.", definition: "to obtain or develop from a source", chinese: "源自；取得", example: "Many medicines are derived from compounds found in plants.", source: "Essential Words for the TOEFL"),
        .init(word: "distinct", partOfSpeech: "adj.", definition: "clearly different or separate", chinese: "明顯不同的；獨特的", example: "The two dialects developed in distinct geographic regions.", source: "Official Guide vocabulary review"),
        .init(word: "elaborate", partOfSpeech: "v./adj.", definition: "to add relevant detail; highly detailed", chinese: "闡述；精心設計的", example: "The interviewer asked the student to elaborate on her example.", source: "Speaking rubric language"),
        .init(word: "empirical", partOfSpeech: "adj.", definition: "based on observation or experiment", chinese: "實證的；經驗性的", example: "The claim requires empirical evidence from controlled studies.", source: "Academic reading vocabulary"),
        .init(word: "facilitate", partOfSpeech: "v.", definition: "to make a process easier", chinese: "促進；使便利", example: "Shared notes can facilitate collaboration among group members.", source: "Essential Words for the TOEFL"),
        .init(word: "fluctuate", partOfSpeech: "v.", definition: "to rise and fall irregularly", chinese: "波動；起伏", example: "Daily temperatures fluctuate more in dry environments.", source: "Essential Words for the TOEFL"),
        .init(word: "infer", partOfSpeech: "v.", definition: "to reach a conclusion from evidence", chinese: "推論；推斷", example: "From the worn steps, historians infer that the entrance was heavily used.", source: "Official Guide vocabulary review"),
        .init(word: "inhibit", partOfSpeech: "v.", definition: "to slow down or prevent", chinese: "抑制；阻礙", example: "Low temperatures inhibit the growth of many microorganisms.", source: "Essential Words for the TOEFL"),
        .init(word: "integral", partOfSpeech: "adj.", definition: "necessary and essential to a whole", chinese: "不可或缺的；構成整體的", example: "Seasonal flooding is integral to the wetland ecosystem.", source: "Academic reading vocabulary"),
        .init(word: "maintain", partOfSpeech: "v.", definition: "to keep in an existing state", chinese: "維持；保養", example: "The greenhouse maintains a stable temperature overnight.", source: "Essential Words for the TOEFL"),
        .init(word: "mechanism", partOfSpeech: "n.", definition: "a process or system that produces a result", chinese: "機制；運作方式", example: "The model isolates the mechanism that drives ocean circulation.", source: "Official TOEFL reading themes"),
        .init(word: "mitigate", partOfSpeech: "v.", definition: "to make something harmful less severe", chinese: "減輕；緩和", example: "Urban trees can mitigate heat by shading pavement.", source: "Academic reading vocabulary"),
        .init(word: "negligible", partOfSpeech: "adj.", definition: "too small to be important", chinese: "微不足道的；可忽略的", example: "The second treatment produced only a negligible change.", source: "Essential Words for the TOEFL"),
        .init(word: "persistent", partOfSpeech: "adj.", definition: "continuing for a long time", chinese: "持續的；長久不退的", example: "Persistent drought altered the region's vegetation.", source: "Academic reading vocabulary"),
        .init(word: "plausible", partOfSpeech: "adj.", definition: "seemingly reasonable or likely", chinese: "看似合理的；可能的", example: "The new evidence makes the alternative explanation plausible.", source: "Official Guide vocabulary review"),
        .init(word: "preliminary", partOfSpeech: "adj.", definition: "coming before a more complete result", chinese: "初步的；預備的", example: "Preliminary findings will be reviewed before publication.", source: "Essential Words for the TOEFL"),
        .init(word: "profound", partOfSpeech: "adj.", definition: "very great or deeply significant", chinese: "深遠的；重大的", example: "The printing press had a profound effect on access to knowledge.", source: "Essential Words for the TOEFL"),
        .init(word: "provisional", partOfSpeech: "adj.", definition: "temporary and subject to revision", chinese: "暫定的；臨時的", example: "The committee issued a provisional schedule for the field season.", source: "Academic reading vocabulary"),
        .init(word: "resilient", partOfSpeech: "adj.", definition: "able to recover after difficulty or change", chinese: "有復原力的；韌性的", example: "A diverse ecosystem may be more resilient after disturbance.", source: "Academic reading vocabulary"),
        .init(word: "retain", partOfSpeech: "v.", definition: "to keep or continue to have", chinese: "保留；維持", example: "Dense materials retain heat after sunset.", source: "Official TOEFL reading themes"),
        .init(word: "subsequent", partOfSpeech: "adj.", definition: "coming after something else", chinese: "隨後的；後續的", example: "Subsequent experiments used a larger sample.", source: "Essential Words for the TOEFL"),
        .init(word: "subtle", partOfSpeech: "adj.", definition: "not obvious; delicate in meaning or effect", chinese: "細微的；不易察覺的", example: "The two pigments show subtle differences under ultraviolet light.", source: "Official Guide vocabulary review"),
        .init(word: "sustain", partOfSpeech: "v.", definition: "to support or keep something going", chinese: "維持；支撐", example: "The soil cannot sustain intensive farming without added nutrients.", source: "Essential Words for the TOEFL"),
        .init(word: "tentative", partOfSpeech: "adj.", definition: "not certain or final", chinese: "暫定的；試探性的", example: "The researchers offered a tentative explanation for the anomaly.", source: "Academic reading vocabulary"),
        .init(word: "underlying", partOfSpeech: "adj.", definition: "basic but not immediately visible", chinese: "根本的；潛在的", example: "The discussion focused on the underlying cause of the conflict.", source: "Official Guide vocabulary review"),
        .init(word: "viable", partOfSpeech: "adj.", definition: "capable of working successfully", chinese: "可行的；能存活的", example: "The pilot study showed that the method was viable.", source: "Essential Words for the TOEFL")
    ]
}

struct StudyLibraryView: View {
    @State private var searchText = ""
    @State private var revealed: Set<String> = []

    private var filtered: [VocabularyEntry] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !query.isEmpty else { return TOEFLStudyBank.words }
        return TOEFLStudyBank.words.filter {
            $0.word.lowercased().contains(query)
                || $0.definition.lowercased().contains(query)
                || $0.chinese.contains(query)
        }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                Text("Academic vocabulary lab")
                    .font(.system(size: 30, weight: .bold, design: .rounded))
                    .foregroundStyle(TOEFLTheme.navy)
                Text("A compact first-version study bank based on the authorized TOEFL references. Definitions and examples are written for this simulator; Chinese uses Taiwan Traditional Chinese.")
                    .foregroundStyle(.secondary)

                HStack {
                    Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                    TextField("Search word, definition, or 中文", text: $searchText)
                        .textFieldStyle(.plain)
                    Text("\(filtered.count) words").font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                }
                .padding(12)
                .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 9))
                .overlay(RoundedRectangle(cornerRadius: 9).stroke(TOEFLTheme.border))

                LazyVGrid(columns: [GridItem(.adaptive(minimum: 285), spacing: 14)], spacing: 14) {
                    ForEach(filtered) { entry in
                        Button {
                            withAnimation(.easeInOut(duration: 0.18)) {
                                if revealed.contains(entry.id) { revealed.remove(entry.id) }
                                else { revealed.insert(entry.id) }
                            }
                        } label: {
                            VStack(alignment: .leading, spacing: 9) {
                                HStack(alignment: .firstTextBaseline) {
                                    Text(entry.word).font(.title2.bold()).foregroundStyle(TOEFLTheme.navy)
                                    Text(entry.partOfSpeech).font(.caption).foregroundStyle(.secondary)
                                    Spacer()
                                    Image(systemName: revealed.contains(entry.id) ? "eye.fill" : "eye.slash")
                                        .foregroundStyle(TOEFLTheme.blue)
                                }
                                if revealed.contains(entry.id) {
                                    Text(entry.definition).font(.body.weight(.medium)).foregroundStyle(.primary)
                                    Text(entry.chinese).foregroundStyle(TOEFLTheme.teal)
                                    Text(entry.example).font(.subheadline).italic().foregroundStyle(.secondary)
                                    Text(entry.source).font(.caption2).foregroundStyle(.tertiary)
                                } else {
                                    Text("Tap to reveal meaning").font(.subheadline).foregroundStyle(.secondary)
                                    Spacer(minLength: 70)
                                }
                            }
                            .frame(maxWidth: .infinity, minHeight: 180, alignment: .topLeading)
                            .toeflCard()
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            .frame(maxWidth: 1050, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Study Library")
    }
}

struct ResourcesView: View {
    private let localSources = [
        "The Official Guide to the TOEFL iBT Test",
        "Official TOEFL iBT Tests, Volumes 1 and 2",
        "TOEFL iBT Premium with 8 Online Practice Tests",
        "Princeton Review TOEFL iBT Prep with Audio-Listening Tracks",
        "Essential Words for the TOEFL (authorized editions)",
        "TOEFL Idioms Quiz Book",
        "Authorized reading-comprehension collection and practice test"
    ]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                Text("Standards and sources")
                    .font(.system(size: 30, weight: .bold, design: .rounded))
                    .foregroundStyle(TOEFLTheme.navy)
                Text("The app uses the post-January 21, 2026 TOEFL iBT blueprint. Older authorized books inform skill coverage, interface conventions, and study vocabulary; their retired task structure is not presented as the current exam.")
                    .foregroundStyle(.secondary)

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
                        .font(.footnote).foregroundStyle(.secondary)
                }
                .toeflCard()

                Text("TOEFL® is a registered trademark of ETS. This independent simulator is not endorsed or approved by ETS.")
                    .font(.footnote).foregroundStyle(.secondary)
            }
            .frame(maxWidth: 950, alignment: .leading)
            .padding(30)
        }
        .navigationTitle("Sources")
    }

    private func resourceLink(_ title: String, _ url: String) -> some View {
        Link(destination: URL(string: url)!) {
            Label(title, systemImage: "arrow.up.right.square")
        }
    }
}
