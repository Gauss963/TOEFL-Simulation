import Foundation

struct VocabularyEntry: Identifiable, Hashable, Codable {
    let word: String
    let pronunciation: String
    let partOfSpeech: String
    let definition: String
    let chinese: String
    let synonyms: [String]
    let example: String
    let sources: [String]
    let isHighFrequency: Bool

    var id: String { word.lowercased() }
}

enum TOEFLStudyBank {
    private static let bundledWords: [VocabularyEntry] = {
        let candidates = [
            Bundle.main.url(forResource: "AuthorizedTOEFLVocabulary", withExtension: "json"),
            Bundle.main.url(
                forResource: "AuthorizedTOEFLVocabulary",
                withExtension: "json",
                subdirectory: "Resources"
            )
        ]
        for url in candidates.compactMap({ $0 }) {
            guard let data = try? Data(contentsOf: url) else { continue }
            do {
                return try JSONDecoder().decode([VocabularyEntry].self, from: data)
            } catch {
                assertionFailure("Could not decode AuthorizedTOEFLVocabulary.json: \(error)")
            }
        }
        assertionFailure("Missing bundled resource: AuthorizedTOEFLVocabulary.json")
        return []
    }()

    private static let curatedWords: [VocabularyEntry] = [
        word("adapt", "v.", "to change in response to conditions; to adjust; to modify a book, play, etc. for another form", "適應；調整；改編", "Coastal plants adapt to frequent exposure to salt.", "Essential Words for the TOEFL"),
        word("allocate", "v.", "to distribute or assign for a particular purpose", "分配；撥給；配置", "The committee allocated additional funds to laboratory safety.", "Official Guide vocabulary review"),
        word("ambiguous", "adj.", "open to more than one interpretation; unclear or uncertain", "模稜兩可的；語意不明的；含糊的；不明確的", "The ambiguous inscription supports several historical interpretations.", "Essential Words for the TOEFL"),
        word("approximate", "adj./v.", "close to the actual value but not exact; to estimate; to come close to in quality or amount", "adj. 大約的；近似的；v. 估算；接近；近似於", "Researchers used tree rings to approximate earlier rainfall levels.", "Official Guide vocabulary review"),
        word("coherent", "adj.", "logical, consistent, and easy to follow; forming a unified whole", "連貫的；條理清楚的；一致的", "A coherent response connects evidence to a clear claim.", "Writing rubric language"),
        word("compensate", "v.", "to make up for a weakness or loss; to pay someone for loss, injury, or work", "彌補；補償；賠償；抵銷", "Magnetic cues compensate when clouds hide visual landmarks.", "Official TOEFL reading themes"),
        word("consecutive", "adj.", "following one after another without interruption", "連續的；連貫的", "The team collected measurements on five consecutive days.", "Essential Words for the TOEFL"),
        word("constitute", "v.", "to form or make up a whole; to amount to; to establish", "構成；組成；被視為；設立", "Small businesses constitute most of the town's employers.", "Essential Words for the TOEFL"),
        word("corroborate", "v.", "to confirm or give support to a statement or theory with additional evidence", "證實；佐證；支持（說法）", "A second field survey corroborated the original observation.", "Academic reading vocabulary"),
        word("decline", "n./v.", "a gradual decrease or loss of strength; to become smaller or weaker; to politely refuse", "n. 下降；衰退；減少；v. 下降；衰退；婉拒", "The population began to decline after its habitat was fragmented.", "Official TOEFL reading themes"),
        word("derive", "v.", "to obtain or develop from a source; to originate from; to work out by reasoning", "源自；取得；獲得；推導出", "Many medicines are derived from compounds found in plants.", "Essential Words for the TOEFL"),
        word("distinct", "adj.", "clearly different or separate; easily noticed or clearly perceived", "明顯不同的；有區別的；清楚的；明確的", "The two dialects developed in distinct geographic regions.", "Official Guide vocabulary review"),
        word("elaborate", "adj./v.", "detailed and carefully planned; complicated; to explain or add more details", "adj. 精心設計的；精緻的；複雜的；v. 詳細說明；闡述", "The interviewer asked the student to elaborate on her example.", "Speaking rubric language"),
        word("empirical", "adj.", "based on observation, experiment, or experience rather than theory", "實證的；經驗主義的；以實驗為根據的", "The claim requires empirical evidence from controlled studies.", "Academic reading vocabulary"),
        word("facilitate", "v.", "to make an action or process easier or more likely", "促進；使便利；協助", "Shared notes can facilitate collaboration among group members.", "Essential Words for the TOEFL"),
        word("fluctuate", "v.", "to rise and fall irregularly in number or amount", "波動；起伏；變動", "Daily temperatures fluctuate more in dry environments.", "Essential Words for the TOEFL"),
        word("infer", "v.", "to reach a conclusion from evidence and reasoning; to imply", "推論；推斷；推測", "From the worn steps, historians infer that the entrance was heavily used.", "Official Guide vocabulary review"),
        word("inhibit", "v.", "to slow down or prevent a process; to make someone self-conscious and restrained", "抑制；阻礙；使拘束", "Low temperatures inhibit the growth of many microorganisms.", "Essential Words for the TOEFL"),
        word("integral", "adj.", "necessary to make a whole complete; essential; (mathematics) relating to integrals", "不可或缺的；構成整體的；完整的；（數學）積分的", "Seasonal flooding is integral to the wetland ecosystem.", "Academic reading vocabulary"),
        word("maintain", "v.", "to keep in an existing state; to keep in good condition; to state firmly that something is true; to support financially", "維持；保養；堅持主張；扶養", "The greenhouse maintains a stable temperature overnight.", "Essential Words for the TOEFL"),
        word("mechanism", "n.", "a process or system that produces a result; a set of moving parts in a machine", "機制；機械裝置；運作方式", "The model isolates the mechanism that drives ocean circulation.", "Official TOEFL reading themes"),
        word("mitigate", "v.", "to make something harmful or bad less severe", "減輕；緩和", "Urban trees can mitigate heat by shading pavement.", "Academic reading vocabulary"),
        word("negligible", "adj.", "too small or unimportant to be worth considering", "微不足道的；可忽略的", "The second treatment produced only a negligible change.", "Essential Words for the TOEFL"),
        word("persistent", "adj.", "continuing for a long time; continuing firmly despite opposition or difficulty", "持續的；堅持不懈的；頑固的", "Persistent drought altered the region's vegetation.", "Academic reading vocabulary"),
        word("plausible", "adj.", "seemingly reasonable or probable; believable; (of a person) skilled at producing persuasive but possibly deceptive arguments", "看似合理的；可信的；（人）花言巧語的", "The new evidence makes the alternative explanation plausible.", "Official Guide vocabulary review"),
        word("preliminary", "adj./n.", "coming before and preparing for something more important; a preliminary action or event; a preliminary round in a competition", "adj. 初步的；預備的；開端的；n. 初步行動；預賽", "Preliminary findings will be reviewed before publication.", "Essential Words for the TOEFL"),
        word("profound", "adj.", "very great or far-reaching; showing great knowledge or insight; demanding deep study; intensely felt", "深遠的；重大的；深刻的；深奧的；強烈的", "The printing press had a profound effect on access to knowledge.", "Essential Words for the TOEFL"),
        word("provisional", "adj.", "arranged for the present, possibly to be changed later; temporary", "暫時的；臨時的；暫定的", "The committee issued a provisional schedule for the field season.", "Academic reading vocabulary"),
        word("resilient", "adj.", "able to recover quickly from difficulties or change; adaptable; (of a material) able to spring back into shape", "有復原力的；有韌性的；適應力強的；（物體）有彈性的", "A diverse ecosystem may be more resilient after disturbance.", "Academic reading vocabulary"),
        word("retain", "v.", "to continue to have or keep; to keep in place; to remember; to hire a lawyer by paying a fee", "保留；保持；維持；記住；聘請（律師等）", "Dense materials retain heat after sunset.", "Official TOEFL reading themes"),
        word("subsequent", "adj.", "coming after something in time; following", "隨後的；後續的；之後的", "Subsequent experiments used a larger sample.", "Essential Words for the TOEFL"),
        word("subtle", "adj.", "not obvious; hard to notice; delicate in meaning or effect; clever", "細微的；不易察覺的；微妙的；巧妙的", "The two pigments show subtle differences under ultraviolet light.", "Official Guide vocabulary review"),
        word("sustain", "v.", "to keep something going over time; to support physically or mentally; to suffer an injury or loss", "維持；支撐；遭受（損失或傷害）", "The soil cannot sustain intensive farming without added nutrients.", "Essential Words for the TOEFL"),
        word("tentative", "adj.", "not certain or fixed; provisional; done without confidence", "暫定的；試探性的；猶豫的", "The researchers offered a tentative explanation for the anomaly.", "Academic reading vocabulary"),
        word("underlying", "adj.", "basic and fundamental but not immediately obvious; lying beneath", "根本的；潛在的；在下面的", "The discussion focused on the underlying cause of the conflict.", "Official Guide vocabulary review"),
        word("viable", "adj.", "capable of working successfully; feasible; able to survive or grow", "可行的；切實可行的；能存活的", "The pilot study showed that the method was viable.", "Essential Words for the TOEFL")
    ]

    static let words: [VocabularyEntry] = {
        var merged = Dictionary(uniqueKeysWithValues: bundledWords.map { ($0.id, $0) })
        for curated in curatedWords {
            if let imported = merged[curated.id] {
                merged[curated.id] = VocabularyEntry(
                    word: curated.word,
                    pronunciation: imported.pronunciation,
                    partOfSpeech: curated.partOfSpeech,
                    definition: curated.definition,
                    chinese: curated.chinese,
                    synonyms: imported.synonyms,
                    example: curated.example,
                    sources: Array(Set(imported.sources + curated.sources)).sorted(),
                    isHighFrequency: true
                )
            } else {
                merged[curated.id] = curated
            }
        }
        return merged.values.sorted {
            $0.word.localizedCaseInsensitiveCompare($1.word) == .orderedAscending
        }
    }()

    static var sourceOptions: [String] {
        Array(Set(words.flatMap(\.sources))).sorted()
    }

    static func validate() -> [String] {
        var issues: [String] = []
        if words.count < 1_300 { issues.append("Vocabulary bank unexpectedly contains fewer than 1,300 entries.") }
        if Set(words.map(\.id)).count != words.count { issues.append("Vocabulary bank has duplicate IDs.") }
        if words.contains(where: { $0.word.isEmpty || $0.chinese.isEmpty || $0.example.isEmpty }) {
            issues.append("Vocabulary bank contains an incomplete card.")
        }
        if words.contains(where: { $0.definition.isEmpty || $0.chinese.contains("/") || $0.chinese.contains(";") }) {
            issues.append("Vocabulary bank contains an unreviewed definition or sense separator.")
        }
        let missingAudio = words.filter { WordPronunciationLibrary.shared.clip(for: $0.word) == nil }
        if !missingAudio.isEmpty {
            issues.append("\(missingAudio.count) words have no bundled pronunciation, e.g. \(missingAudio.prefix(3).map(\.word)).")
        }
        return issues
    }

    private static func word(
        _ word: String,
        _ partOfSpeech: String,
        _ definition: String,
        _ chinese: String,
        _ example: String,
        _ source: String
    ) -> VocabularyEntry {
        VocabularyEntry(
            word: word,
            pronunciation: "",
            partOfSpeech: partOfSpeech,
            definition: definition,
            chinese: chinese,
            synonyms: [],
            example: example,
            sources: [source],
            isHighFrequency: true
        )
    }
}
