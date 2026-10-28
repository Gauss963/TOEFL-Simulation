import SwiftUI
#if os(macOS)
import AppKit
#endif

/// A study guide for the 2026 Speaking section, combining ETS's task descriptions and scoring
/// criteria with test-prep advice, plus practice widgets that play real prompts from the bank.
/// Explanations are in Traditional Chinese; phrases to use in answers are in English.
struct SpeakingGuideView: View {
    enum Topic: String, CaseIterable, Identifiable {
        case repeatTask = "Listen and Repeat"
        case interview = "Take an Interview"
        case delivery = "Pronunciation & Fluency"
        var id: String { rawValue }
    }

    let start: (ExamMode) -> Void
    @State private var topic: Topic = .repeatTask

    var body: some View {
        ScrollView { content }
            .navigationTitle("Speaking Guide")
    }

    var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            switch topic {
            case .repeatTask: repeatGuide
            case .interview: interviewGuide
            case .delivery: deliveryGuide
            }
        }
        .frame(maxWidth: 1000, alignment: .leading)
        .padding(30)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Speaking guide")
                .font(.system(size: 30, weight: .bold, design: .rounded))
                .foregroundStyle(TOEFLTheme.navy)
            Text("新制 Speaking 共 11 題、約 8 分鐘：Listen and Repeat 7 題 + Take an Interview 4 題，全部沒有準備時間。ETS 說明這一科評量你能否清楚、有效地用英文溝通：複述是否準確、聽起來是否清楚易懂、說話節奏是否自然、用字與文法是否恰當。題目取材自校園與學術情境，可能聽到北美、英國、紐西蘭或澳洲口音。")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack {
                Picker("Topic", selection: $topic) {
                    ForEach(Topic.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)
                .frame(maxWidth: 560)
                Spacer()
                Button("Practice the Speaking section") { start(.speakingPractice) }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
            }
        }
        .toeflCard()
    }

    // MARK: - Listen and Repeat

    private var repeatGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("題型重點", icon: "repeat") {
                Guide.facts([
                    ("題數", "7 句，同一個情境（例如在動物園當志工，聽主管說明）"),
                    ("作答", "每句播完後立刻複述一次；回答時間約 8–12 秒，句子越後面越長、越複雜"),
                    ("畫面", "會出現情境示意圖，只是背景；任務只有「照原句說一遍」"),
                    ("評分", "每句 0–5 分：完全照原句說出且清楚易懂得 5 分；意思正確但有小改動（漏冠詞、時態或單複數、語序微調）約 4 分；句子大致完整但意思跑掉約 3 分；漏掉大段約 2 分；只說出一點點約 1 分；沒說或聽不懂 0 分"),
                ])
                Guide.note("這題不考你的意見或組織能力，而是聽辨、短期記憶、發音與語調。目標是一字不差，不是改寫。")
            }

            Guide.card("三步驟作答法", icon: "list.number") {
                Guide.structure([
                    ("1", "邊聽邊切意群", "不要一個字一個字記，而是把句子切成 2–3 個「意群」（有意義的片段）。", "If a visitor feels sick / or gets hurt, / bring them to the first aid booth right away."),
                    ("2", "先抓骨架", "先記住主詞、動詞和句尾的關鍵資訊（地點、時間、數字），再補細節。句尾資訊最容易忘。", "visitor sick / hurt → bring → first aid booth → right away"),
                    ("3", "提示音後立刻說", "照原句的停頓與語調說出來，說完就停。忘了一小段時，保持句子文法完整、意思不變，不要整句放棄或重新開始。", ""),
                ])
            }

            listenRepeatTrainer

            Guide.card("最常被扣分的地方", icon: "exclamationmark.triangle") {
                Guide.mistakes([
                    ("The penguin are fed every morning.", "The penguins are fed every morning.", "漏掉字尾 -s、-ed：這類小字最容易被吞掉，會從 5 分掉到 4 分。"),
                    ("Volunteers get small discount at gift shop.", "Volunteers get a small discount at the gift shop.", "漏掉冠詞 a、the。中文沒有冠詞，要刻意聽、刻意說。"),
                    ("You can get a map here.", "You can grab a map right here.", "換成自己的說法（paraphrase）：意思對也只有 4 分，原句用字要照說。"),
                    ("…bring them to the… uh… sorry, if a visitor…", "停頓半秒後接著說完", "重新開始或道歉會浪費時間，也讓評分聽起來不流暢。"),
                    ("每句都用同一個平調唸完", "模仿原句的重音和語調", "評分包含語調是否自然；把重音放在關鍵字（名詞、動詞、數字）。"),
                ])
            }

            Guide.card("練習方法", icon: "figure.run") {
                Guide.phraseGroup("每天 10–15 分鐘 Shadowing（影子跟讀）", [
                    "選校園對話、Podcast 或本 app 的 Listening 錄音，以意群為單位，比原音晚半拍跟著說。",
                    "先看文字跟讀，熟了以後不看文字，最後只聽一次就複述。",
                ])
                Guide.phraseGroup("錄音比對", [
                    "做完 Speaking 練習後，在成績報告的答案檢討中看「辨識出的文字」，和原句逐字比對。",
                    "辨識結果和原句不同的地方，通常就是發音不清或漏字的地方。",
                ])
                Guide.phraseGroup("逐步拉長", [
                    "先練 6–8 個字的短句，穩定 5 分後再練 12–16 個字、含子句的長句。",
                    "長句練「意群記憶」：聽完先在心裡默念骨架，再完整說出。",
                ])
            }
        }
    }

    // MARK: - Interview

    private var interviewGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("題型重點", icon: "person.crop.rectangle") {
                Guide.facts([
                    ("題數", "4 題，同一個主題的模擬訪談（影片中的訪談者提問）"),
                    ("作答", "每題 45 秒，沒有準備時間；題目只用聽的，不會顯示在螢幕上"),
                    ("難度", "逐題提高：描述自己的習慣 → 講一次經驗 → 比較或討論挑戰 → 表達立場"),
                    ("評分", "每題 0–5 分：是否切題、內容是否充分展開、說話節奏是否穩定自然、發音是否清楚（重音與語調能傳達意思）、文法與用字是否正確多樣"),
                    ("長度", "以正常語速說滿約 40–45 秒，大約 90–120 字"),
                ])
            }

            Guide.card("四題的漸進邏輯", icon: "stairs") {
                Guide.phraseTable(["題目", "常見問法", "重點"], [
                    ["第 1 題 暖身", "Where do you usually…? What does a typical… look like?", "用現在式描述習慣；直接回答，再補 2–3 個細節"],
                    ["第 2 題 經驗", "Think about a time when… How did it turn out?", "用過去式講一個具體事件：背景 → 你做了什麼 → 結果"],
                    ["第 3 題 比較", "Some people…, while others… Which do you prefer?", "選一邊，說主要理由 + 例子，可以承認另一邊的優點"],
                    ["第 4 題 立場", "Do you think schools should…? Why or why not?", "清楚表態，一個理由說完整 + 現實例子，比三個理由都說一半好"],
                ])
            }

            Guide.card("45 秒回答結構", icon: "list.number") {
                Guide.structure([
                    ("1", "直接回答（約 5–8 秒）", "第一句就回答問題，不要先鋪陳背景（中文習慣先講背景，英文要先講重點）。", "Honestly, I'd say I prefer studying alone."),
                    ("2", "主要理由（約 15 秒）", "說明為什麼，加一點細節。", "The main reason is that I can control my own pace."),
                    ("3", "具體例子（約 15 秒）", "親身經驗、具體的人事時地物——這是拉開分數的關鍵。", "For example, last semester my study group met for three hours but only covered one problem set."),
                    ("4", "收尾（約 5 秒，可省略）", "一句話回扣答案；如果還有時間，也可以繼續補充例子直到時間到。", "So overall, I study alone and join a group only for final review."),
                ])
            }

            interviewTrainer

            Guide.card("範例回答（第 1 組：大學生的讀書習慣）", icon: "text.bubble") {
                Text("以下四題來自題庫的同一組訪談，示範難度逐題提高時回答怎麼調整。每段約 95–110 字，以正常語速約 40–45 秒。")
                    .font(.subheadline).foregroundStyle(.secondary)
                ForEach(Array(Self.modelAnswers.enumerated()), id: \.offset) { index, answer in
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Q\(index + 1). \(answer.question)").font(.subheadline.bold())
                        ForEach(Array(answer.parts.enumerated()), id: \.offset) { _, part in
                            HStack(alignment: .top, spacing: 12) {
                                Text(part.0).font(.caption.bold()).foregroundStyle(TOEFLTheme.teal).frame(width: 90, alignment: .leading)
                                Text(part.1).font(.body).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                            }
                        }
                        Text("\(answer.parts.map(\.1).joined(separator: " ").split(whereSeparator: \.isWhitespace).count) words · \(answer.note)")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                    .padding(12)
                    .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                }
            }

            Guide.card("常用句型", icon: "text.alignleft") {
                Guide.phraseGroup("立刻開口回答", [
                    "Honestly, I'd say…",
                    "For me, it's definitely…",
                    "I usually… / Most of the time, I…",
                    "That's an interesting question. I think…（只在需要一秒思考時用一次）",
                ])
                Guide.phraseGroup("說明理由", [
                    "The main reason is that…",
                    "This is mainly because…",
                    "What I like most about it is that…",
                ])
                Guide.phraseGroup("舉例與講經驗（用過去式）", [
                    "For example, last semester I…",
                    "I remember one time when…",
                    "At first, I… Then I… In the end, it turned out…",
                ])
                Guide.phraseGroup("比較、讓步與收尾", [
                    "That said, group study is useful right before an exam.",
                    "On the other hand, …",
                    "So overall, I think…",
                    "That's why I…",
                ])
            }

            Guide.card("常見失分原因", icon: "exclamationmark.triangle") {
                Guide.mistakes([
                    ("先講一大段背景，最後才回答", "第一句直接回答", "英文溝通習慣先講主旨；先鋪陳會讓評分者抓不到重點。"),
                    ("第 1 題講太長、後面才發現時間不夠", "每題都用同樣的四步結構", "每題獨立計時 45 秒，但第 1 題說太多會影響整體節奏與專注。"),
                    ("I think it's good because it's good.", "One clear reason + a real example", "循環論證沒有內容；具體的例子才是「充分展開」。"),
                    ("背好的長模板硬套", "記住幾個開頭句型，內容即時發揮", "新制沒有準備時間，模板聽起來不自然，也常答非所問。"),
                    ("講經驗時用現在式", "Last year, I had to…", "第 2 題講過去的事件要用過去式，時態錯誤很容易被扣分。"),
                    ("um… uh… like…", "短暫停頓或 Well, …", "贅詞過多會影響「流暢度」；寧可停半秒。"),
                    ("說到 30 秒就停", "用例子或補充說滿 40–45 秒", "太短通常無法「充分展開」，也會被扣分。"),
                ])
            }

            Guide.card("建立個人素材庫", icon: "tray.full") {
                Text("訪談主題多半是校園與日常：讀書方法、科技使用、交通、飲食、運動、打工、社團、旅行、環保。先為每個主題準備 1–2 個真實經驗（時間、地點、發生什麼、結果），臨場就能快速套用，比背模板更自然。")
                    .font(.subheadline).fixedSize(horizontal: false, vertical: true)
                Guide.checklist([
                    "一次準備周全的考試（讀書方法、團體 vs 個人）",
                    "一次用科技解決問題（App、線上課程、AI 工具）",
                    "一次社團或打工的挑戰（時間管理、團隊合作）",
                    "一次旅行或搬家的經驗（適應新環境）",
                    "一個改變你習慣的決定（運動、飲食、作息）",
                ])
            }
        }
    }

    // MARK: - Pronunciation & fluency

    private var deliveryGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("評分看的「表達」是什麼", icon: "waveform") {
                Guide.facts([
                    ("清楚易懂", "評分者不需要費力就聽得懂；不要求美式或英式口音"),
                    ("節奏與語速", "穩定的對話語速，約每分鐘 130–150 字；不要為了塞內容而說太快"),
                    ("重音與語調", "重要的字加重、句尾語調正確，能幫助傳達意思"),
                    ("流暢度", "停頓自然，贅詞與重新開始少"),
                ])
            }

            Guide.card("台灣考生常見發音問題", icon: "mouth") {
                Guide.phraseTable(["問題", "例子", "怎麼練"], [
                    ["字尾子音被吞掉", "need、hard、book、twelve", "刻意把最後的 /d/ /t/ /k/ 收乾淨，錄音聽看看有沒有"],
                    ["-s、-ed 字尾漏掉", "students、helped、decided", "Listen and Repeat 最常因此扣分；名詞複數和過去式特別注意"],
                    ["th 發成 s 或 d", "think、three、this、other", "舌尖輕放在上下齒之間送氣"],
                    ["v 和 w、b 混淆", "very、video、vote", "上齒輕碰下唇再發聲"],
                    ["l 和 n、r 混淆", "light / night、right / light", "l 舌尖頂上齒齦；r 舌頭不碰任何地方"],
                    ["長短母音不分", "ship / sheep、full / fool", "短母音短而放鬆，長母音拉長"],
                    ["每個音節都一樣重", "comPUter、deVElop、PHOtograph", "英文是重音節奏，非重音音節要輕、短"],
                    ["字與字斷開唸", "pick it up → pi-ki-tup", "子音接母音時連音，句子會更自然"],
                ])
            }

            Guide.card("用這個 app 找出自己的問題", icon: "magnifyingglass") {
                Guide.checklist([
                    "做 Speaking 練習後，打開成績報告的答案檢討，看每題「辨識出的文字」",
                    "Listen and Repeat：辨識結果和原句不同的字，多半是發音不清或漏字",
                    "Interview：檢查是否有很多 um、uh，回答是否說滿 40 秒以上",
                    "把常錯的字記下來，用上表的方法單獨練，再回到整句",
                ])
            }

            Guide.card("四週練習計畫", icon: "calendar") {
                Guide.phraseTable(["週", "Listen and Repeat", "Take an Interview"], [
                    ["第 1 週", "每天 Shadowing 10 分鐘；熟悉意群切分", "背熟每個步驟的 2–3 個開頭句；每天答 2 題，不計時"],
                    ["第 2 週", "每天練 1 組（7 句），錄音比對", "每天 1 組（4 題）計時 45 秒，錄音聽自己的結構"],
                    ["第 3 週", "專練長句與常漏的字尾", "建立個人素材庫；練第 2、4 題的經驗與立場"],
                    ["第 4 週", "完整 Speaking 模擬，連續作答", "完整模擬並檢討贅詞、時態與長度；考前降低強度"],
                ])
            }
        }
    }

    // MARK: - Practice widgets

    private var listenRepeatTrainer: some View {
        Guide.card("練習：聽一句、複述一句", icon: "speaker.wave.2") {
            ListenRepeatTrainer()
        }
    }

    private var interviewTrainer: some View {
        Guide.card("練習：聽題目、45 秒作答", icon: "timer") {
            InterviewTrainer()
        }
    }

    // MARK: - Model answers

    struct ModelAnswer {
        let question: String
        let parts: [(String, String)]
        let note: String
    }

    /// Original model answers for interview set iv-01 (study habits of university students).
    static let modelAnswers: [ModelAnswer] = [
        ModelAnswer(
            question: "Could you tell me where you usually study and what a typical study session looks like for you?",
            parts: [
                ("直接回答", "I usually study on the third floor of the library, because it's quiet and I don't get distracted there."),
                ("細節", "A typical session starts with a quick plan: I write down two or three tasks I want to finish. Then I work in blocks of about forty minutes and take a short break in between, maybe to get some water or walk around."),
                ("具體做法", "I also put my phone in my bag, because otherwise I'd keep checking messages."),
                ("收尾", "At the end, I note what I finished and what's next, so it's easier to start the following day."),
            ],
            note: "現在式描述習慣；用 first / then / at the end 讓步驟清楚。"
        ),
        ModelAnswer(
            question: "Think about a time when you had to prepare for a really important exam. How did you get ready, and how did it turn out?",
            parts: [
                ("背景", "Last year, I had to prepare for my organic chemistry final, which was worth forty percent of my grade."),
                ("你做了什麼", "About three weeks before the exam, I made a schedule and reviewed one chapter every day. The most helpful thing was doing old practice problems, because they showed me exactly which reactions I kept mixing up. I also met a classmate twice a week, and we explained the mechanisms to each other out loud."),
                ("結果與收穫", "In the end, it turned out really well. I got an A minus, much higher than my midterm score, and it taught me that steady practice works better than cramming."),
            ],
            note: "過去式講一個具體事件：背景 → 行動 → 結果。"
        ),
        ModelAnswer(
            question: "Some students like to study in groups, while others prefer to work alone. Which do you prefer, and why do you think it works better for you?",
            parts: [
                ("直接回答", "I definitely prefer studying alone, at least for most subjects."),
                ("主要理由", "The main reason is that I can control my own pace. In a group, we often spend a long time on topics I already understand, or the conversation drifts to something else."),
                ("具體例子", "For example, last semester my economics study group met for three hours, but we only finished one problem set."),
                ("讓步與收尾", "That said, I think groups are useful right before an exam, when you can quiz each other. So overall, I study alone for most of the work and join a group only for the final review."),
            ],
            note: "選一邊 → 理由 → 例子，再用 That said 承認另一邊，顯得成熟。"
        ),
        ModelAnswer(
            question: "Do you think schools should require all first-year students to take a course on how to study effectively? Why or why not?",
            parts: [
                ("表明立場", "Yes, I think universities should require a course like that for first-year students."),
                ("主要理由", "The biggest reason is that studying in college is very different from high school. In high school, teachers check your progress all the time, but in college you have to manage long projects on your own, and many students don't know how."),
                ("具體例子", "For instance, several of my friends did badly on their first midterms simply because they started reviewing the night before."),
                ("收尾", "A short required course could teach skills like planning and active recall early, before students fall behind. That's why I think it would save a lot of people a stressful first year."),
            ],
            note: "一個理由說完整 + 現實例子，比列三個理由更有說服力。"
        ),
    ]

    #if DEBUG
    /// Renders each topic, for `-renderSpeakingGuide <directory>`.
    static func renderSnapshots(to directory: URL) {
        for topic in Topic.allCases {
            var view = SpeakingGuideView(start: { _ in })
            view._topic = State(initialValue: topic)
            let renderer = ImageRenderer(content: view.content.frame(width: 1000).background(TOEFLTheme.canvas))
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                let name = topic.rawValue.replacingOccurrences(of: " ", with: "-").replacingOccurrences(of: "&", with: "and")
                try? png.write(to: directory.appendingPathComponent("speaking-\(name).png"))
            }
            #endif
        }
    }
    #endif
}

/// Plays Listen and Repeat sentences from the bank one at a time; the text, split into meaning
/// groups, is shown after the learner has tried repeating it.
private struct ListenRepeatTrainer: View {
    @StateObject private var player = ExamAudioPlayer()
    @State private var setIndex = Int.random(in: 0..<max(1, ListenRepeatTrainer.sets.count))
    @State private var sentenceIndex = 0
    @State private var revealed = false

    static let sets: [[TOEFLQuestion]] = {
        let items = TOEFLQuestionBank.allQuestions.filter { $0.taskType == .listenRepeat }
        return Dictionary(grouping: items) { $0.setID ?? $0.id }
            .values
            .map { $0.sorted { ($0.setIndex ?? 0) < ($1.setIndex ?? 0) } }
            .sorted { ($0.first?.id ?? "") < ($1.first?.id ?? "") }
    }()

    private var sentence: TOEFLQuestion? {
        guard Self.sets.indices.contains(setIndex), Self.sets[setIndex].indices.contains(sentenceIndex) else { return nil }
        return Self.sets[setIndex][sentenceIndex]
    }

    var body: some View {
        if let sentence {
            VStack(alignment: .leading, spacing: 12) {
                if let setting = sentence.setting {
                    Text("\(setting) · sentence \(sentenceIndex + 1) of \(Self.sets[setIndex].count)").font(.subheadline.bold())
                }
                Text("播放後，在心裡切意群，然後大聲複述一次（實際考試約有 \(sentence.responseSeconds ?? 10) 秒）。說完再按「顯示原句」核對。")
                    .font(.caption).foregroundStyle(.secondary)
                HStack(spacing: 10) {
                    Button(player.isPlaying ? "Playing…" : "Play sentence") {
                        player.play(key: sentence.audioKey, fallbackText: sentence.audioScript) {}
                    }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
                    .disabled(player.isPlaying)
                    Button(revealed ? "Hide sentence" : "Show sentence") { revealed.toggle() }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                    Button("Next sentence") { advance() }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                    Button("New set") { setIndex = Int.random(in: 0..<Self.sets.count); sentenceIndex = 0; revealed = false; player.stop() }
                        .buttonStyle(.borderless)
                }
                if revealed, let text = sentence.audioScript {
                    Text(Self.meaningGroups(text))
                        .font(.title3)
                        .textSelection(.enabled)
                        .padding(12)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                    Text("「/」標示建議的意群切分；字尾 -s、-ed 與冠詞要照說。").font(.caption).foregroundStyle(.secondary)
                }
            }
            .onDisappear { player.stop() }
        } else {
            Text("No Listen and Repeat items in the bank.").foregroundStyle(.secondary)
        }
    }

    private func advance() {
        player.stop()
        revealed = false
        if sentenceIndex + 1 < Self.sets[setIndex].count {
            sentenceIndex += 1
        } else {
            setIndex = (setIndex + 1) % Self.sets.count
            sentenceIndex = 0
        }
    }

    /// Marks likely meaning-group boundaries: after commas, and before a conjunction, relative word,
    /// or preposition once the current group has at least three words.
    static func meaningGroups(_ sentence: String) -> String {
        let breakers: Set<String> = ["and", "but", "so", "or", "because", "if", "when", "where", "while", "which", "who", "that",
                                     "to", "at", "for", "in", "on", "with", "from", "behind", "near", "during", "before", "after", "until"]
        var output: [String] = []
        var groupLength = 0
        for word in sentence.split(separator: " ").map(String.init) {
            let bare = word.lowercased().trimmingCharacters(in: .punctuationCharacters)
            if groupLength >= 3, breakers.contains(bare) {
                output.append("/")
                groupLength = 0
            }
            output.append(word)
            groupLength += 1
            if word.hasSuffix(",") {
                output.append("/")
                groupLength = 0
            }
        }
        return output.joined(separator: " ").replacingOccurrences(of: "/ /", with: "/")
    }
}

/// Plays an interview question from the bank, then counts down 45 seconds for a spoken answer.
private struct InterviewTrainer: View {
    @StateObject private var player = ExamAudioPlayer()
    @State private var setIndex = Int.random(in: 0..<max(1, InterviewTrainer.sets.count))
    @State private var questionIndex = 0
    @State private var revealed = false
    @State private var remaining: Double?
    @State private var countdown: Task<Void, Never>?

    static let sets: [[TOEFLQuestion]] = {
        let items = TOEFLQuestionBank.allQuestions.filter { $0.taskType == .interview }
        return Dictionary(grouping: items) { $0.setID ?? $0.id }
            .values
            .map { $0.sorted { ($0.setIndex ?? 0) < ($1.setIndex ?? 0) } }
            .sorted { ($0.first?.id ?? "") < ($1.first?.id ?? "") }
    }()

    private var question: TOEFLQuestion? {
        guard Self.sets.indices.contains(setIndex), Self.sets[setIndex].indices.contains(questionIndex) else { return nil }
        return Self.sets[setIndex][questionIndex]
    }

    var body: some View {
        if let question {
            VStack(alignment: .leading, spacing: 12) {
                Text("Question \(questionIndex + 1) of \(Self.sets[setIndex].count)").font(.subheadline.bold())
                if questionIndex == 0, let intro = question.intro {
                    Text(intro).font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
                Text("題目只用聽的（和正式考試一樣）。播完後開始 45 秒倒數，請用「直接回答 → 理由 → 例子 → 收尾」說滿時間。")
                    .font(.caption).foregroundStyle(.secondary)
                HStack(spacing: 10) {
                    Button(player.isPlaying ? "Playing…" : "Play question") { playAndCount(question) }
                        .buttonStyle(TOEFLPrimaryButtonStyle())
                        .disabled(player.isPlaying || remaining != nil)
                    Button(revealed ? "Hide question" : "Show question") { revealed.toggle() }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                    Button("Next question") { advance() }
                        .buttonStyle(TOEFLSecondaryButtonStyle())
                    Button("New set") { setIndex = Int.random(in: 0..<Self.sets.count); questionIndex = 0; reset() }
                        .buttonStyle(.borderless)
                    Spacer()
                    if let remaining {
                        Text("\(Int(ceil(remaining)))")
                            .font(.system(size: 34, weight: .bold, design: .rounded).monospacedDigit())
                            .foregroundStyle(remaining <= 10 ? .red : TOEFLTheme.navy)
                    }
                }
                if revealed, let text = question.audioScript {
                    Text(text)
                        .font(.body)
                        .padding(12)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(TOEFLTheme.input, in: RoundedRectangle(cornerRadius: 8))
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .onDisappear { reset() }
        } else {
            Text("No interview items in the bank.").foregroundStyle(.secondary)
        }
    }

    private func playAndCount(_ question: TOEFLQuestion) {
        player.play(key: question.audioKey, fallbackText: question.audioScript) {
            countdown?.cancel()
            countdown = Task { @MainActor in
                let begin = Date()
                remaining = 45
                while let left = remaining, left > 0, !Task.isCancelled {
                    try? await Task.sleep(for: .milliseconds(100))
                    remaining = max(0, 45 - Date().timeIntervalSince(begin))
                }
                if !Task.isCancelled {
                    player.playTone(TOEFLExamAudio.endTone) {}
                    remaining = nil
                }
            }
        }
    }

    private func reset() {
        countdown?.cancel()
        countdown = nil
        remaining = nil
        revealed = false
        player.stop()
    }

    private func advance() {
        reset()
        if questionIndex + 1 < Self.sets[setIndex].count {
            questionIndex += 1
        } else {
            setIndex = (setIndex + 1) % Self.sets.count
            questionIndex = 0
        }
    }
}
