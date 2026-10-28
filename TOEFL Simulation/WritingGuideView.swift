import SwiftUI
#if os(macOS)
import AppKit
#endif

/// A study guide for the 2026 Writing section: structures, templates, phrase banks, checklists, and
/// annotated model answers from the question bank. Explanations are in Traditional Chinese; phrases
/// to use in answers are in English.
struct WritingGuideView: View {
    enum Topic: String, CaseIterable, Identifiable {
        case email = "Write an Email"
        case discussion = "Academic Discussion"
        case sentence = "Build a Sentence"
        var id: String { rawValue }
    }

    let start: (ExamMode) -> Void
    @State private var topic: Topic = .email
    @State private var emailIndex = 0
    @State private var discussionIndex = 0

    var body: some View {
        ScrollView { content }
            .navigationTitle("Writing Guide")
    }

    var content: some View {
        VStack(alignment: .leading, spacing: 22) {
            header
            switch topic {
            case .email: emailGuide
            case .discussion: discussionGuide
            case .sentence: sentenceGuide
            }
        }
        .frame(maxWidth: 1000, alignment: .leading)
        .padding(30)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Writing guide")
                .font(.system(size: 30, weight: .bold, design: .rounded))
                .foregroundStyle(TOEFLTheme.navy)
            Text("新制 Writing 共三題、約 23 分鐘，每題獨立計時、時間不能互相挪用。Email 與學術討論由 AI 與人工共同評分，重點是「把溝通任務完成得清楚、得體」，而不是堆砌難字。")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack {
                Picker("Topic", selection: $topic) {
                    ForEach(Topic.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)
                .frame(maxWidth: 520)
                Spacer()
                Button("Practice the Writing section") { start(.writingPractice) }
                    .buttonStyle(TOEFLPrimaryButtonStyle())
            }
        }
        .toeflCard()
    }

    // MARK: - Email

    private var emailGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("題型重點", icon: "envelope") {
                Guide.facts([
                    ("時間", "7 分鐘"),
                    ("題目", "約 90 字的情境 + 必須完成的 3 個要點；收件人與主旨已填好，只寫內文"),
                    ("字數", "建議 150–200 字；少於 100 字通常無法完整回應所有要點"),
                    ("評分", "0–5：是否完成任務、細節是否充分、語言是否精確多樣、語氣與書信格式是否得體"),
                ])
            }

            Guide.card("四段式結構", icon: "list.number") {
                Guide.structure([
                    ("1", "稱呼 Greeting", "依關係選擇正式程度，後面加逗號。", "Dear Professor Lin,"),
                    ("2", "開門見山說目的 Purpose", "第一句就讓對方知道為什麼寫信；不要寒暄太久。", "I am writing to ask whether I could make up the lab I missed last Thursday."),
                    ("3", "主體：依序完成三個要點 Body", "照題目要點的順序寫，每點 2–3 句：原因 → 影響 → 請求／建議，並加入具體細節（時間、地點、數字）。", "Because I had a high fever, I went to the health center and could not attend. Since the lab counts toward my grade, I would like to complete it before the exam. I am free on Monday or Wednesday afternoon."),
                    ("4", "禮貌收尾 Closing + 署名", "簡短重申請求、感謝對方，再署名。", "Thank you for considering my request. I look forward to hearing from you.\n\nBest regards,\nAmy"),
                ])
            }

            Guide.card("萬用模板（依情境替換括號）", icon: "doc.text") {
                Guide.template("""
                Dear [Professor / Ms. / Mr. + 姓氏],

                I am writing to [ask about / request / let you know about] [主題].

                [要點 1：說明背景或原因，加一個具體細節。]
                [要點 2：說明影響，或提出問題／解釋。]
                [要點 3：提出請求、建議或替代方案，盡量具體（時間、方式）。]

                Thank you for your time and understanding. I look forward to hearing from you.

                Best regards,
                [名字]
                """)
            }

            Guide.card("時間分配", icon: "timer") {
                Guide.facts([
                    ("0:00–1:00", "讀情境，圈出三個要點與收件人身分，決定語氣"),
                    ("1:00–6:00", "依四段式寫作；每個要點至少一個具體細節"),
                    ("6:00–7:00", "檢查三點是否都完成、語氣是否一致、拼字與標點"),
                ])
            }

            Guide.card("稱呼與結尾：依關係選擇", icon: "person.2") {
                Guide.phraseTable(["對象", "稱呼", "結尾", "署名"], [
                    ["教授、主管", "Dear Professor Chen, / Dear Dr. Patel,", "Thank you for your time and consideration.", "Sincerely, / Best regards,"],
                    ["行政單位、不知姓名", "Dear Housing Office, / Dear Hiring Manager,", "I would appreciate your help with this matter.", "Sincerely,"],
                    ["房東、客服、店家", "Dear Mr. Lopez, / Dear Customer Service Team,", "I look forward to your reply.", "Best regards,"],
                    ["同學、同事", "Hi Jordan,", "Let me know what you think.", "Best, / Thanks,"],
                    ["朋友、家人", "Hi Aunt Mei, / Hey Sam,", "Can't wait to hear from you!", "Take care, / Love,"],
                ])
                Guide.note("不知道對方姓名時，用單位名稱比 Dear Sir or Madam 自然；稱呼教授時用 Professor + 姓氏，不要用名字。")
            }

            Guide.card("開頭第一句：依目的", icon: "text.quote") {
                Guide.phrases([
                    ("請求", "I am writing to request an extension on my research paper."),
                    ("詢問", "I am writing to ask about the application process for the exchange program."),
                    ("反映問題", "I am writing to let you know about a problem with the laundry machines on the third floor."),
                    ("道歉", "I am writing to apologize for missing our meeting on Tuesday."),
                    ("建議", "I would like to suggest a small change to our weekly meeting time."),
                    ("感謝", "Thank you so much for helping me prepare for my interview last week."),
                    ("回覆邀請", "Thank you for inviting me to your graduation party."),
                    ("追蹤", "I am following up on the email I sent last Monday about my registration."),
                ])
            }

            Guide.card("主體常用句型", icon: "text.alignleft") {
                Guide.phraseGroup("禮貌提出請求（由普通到最客氣）", [
                    "Could you send me the updated schedule?",
                    "Would it be possible to move my presentation to Friday?",
                    "I was wondering if you might be able to write me a recommendation letter.",
                    "I would really appreciate it if you could look into this issue.",
                ])
                Guide.phraseGroup("說明原因與影響", [
                    "The reason is that my shift was changed at the last minute.",
                    "Because the heater has been broken since Monday, my room is too cold to study in.",
                    "As a result, I was unable to submit the assignment on time.",
                    "This has made it difficult for me to prepare for my exams.",
                ])
                Guide.phraseGroup("提出建議或替代方案", [
                    "One option would be to meet online instead.",
                    "Perhaps we could split the work so that each person writes one section.",
                    "If that is not possible, I would be happy to come in on Saturday morning.",
                    "I am available on Monday after 2 p.m. or any time on Wednesday.",
                ])
                Guide.phraseGroup("得體地拒絕", [
                    "Unfortunately, I won't be able to attend because I have a family commitment that weekend.",
                    "I'm afraid I can't take on another project right now, but I hope we can work together in the future.",
                ])
                Guide.phraseGroup("道歉與負責", [
                    "I am very sorry for the inconvenience this caused.",
                    "It was my mistake, and I take full responsibility.",
                    "To make up for it, I will replace the book by Friday.",
                ])
                Guide.phraseGroup("禮貌地抱怨", [
                    "I wanted to bring a problem to your attention.",
                    "I understand that repairs can take time; however, the issue has continued for two weeks.",
                ])
            }

            Guide.card("語氣階梯：同一件事，越往下越客氣", icon: "slider.horizontal.3") {
                Guide.phraseTable(["直接（避免）", "禮貌", "更委婉（給教授、主管）"], [
                    ["Send me the slides.", "Could you send me the slides?", "Would it be possible for you to share the slides from Monday's lecture?"],
                    ["I want a refund.", "I would like to request a refund.", "I would appreciate it if you could process a refund for this order."],
                    ["Change my room.", "Can I change my room?", "I was wondering whether it might be possible to move to a different room."],
                    ["You are wrong about my grade.", "I think there may be a mistake in my grade.", "I wanted to ask whether you might be willing to take another look at my essay grade."],
                ])
            }

            Guide.card("台灣考生常見錯誤", icon: "exclamationmark.triangle") {
                Guide.mistakes([
                    ("Please reply me.", "Please reply to me. / I look forward to your reply.", "reply 是不及物動詞，要用 reply to。"),
                    ("I want you to give me an extension.", "Would it be possible to have a short extension?", "want 對教授太直接，用 would / could 降低強度。"),
                    ("Hope you can help me.", "I hope you can help me.", "正式信件不要省略主詞。"),
                    ("Dear Professor Daniel,", "Dear Professor Lin,", "Professor 後面接姓氏，不是名字。"),
                    ("I'm so sad that I can't come!!!", "Unfortunately, I won't be able to attend.", "避免情緒化字眼與多個驚嘆號，保持語氣穩定。"),
                    ("只寫兩個要點", "三個要點各寫 2–3 句", "漏掉任何一個要點都會直接影響「任務完成度」。"),
                ])
            }

            Guide.card("交卷前檢查清單", icon: "checklist") {
                Guide.checklist([
                    "第一句就說明寫信目的",
                    "三個要點都有回應，且依題目順序",
                    "每個要點至少有一個具體細節（日期、時間、地點、原因）",
                    "稱呼、結尾、署名與收件人的關係一致",
                    "請求用 could / would / I was wondering if 等客氣說法",
                    "150 字以上，沒有口語縮寫（gonna、u、thx）",
                ])
            }

            annotatedEmail
        }
    }

    // MARK: - Academic Discussion

    private var discussionGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("題型重點", icon: "bubble.left.and.bubble.right") {
                Guide.facts([
                    ("時間", "10 分鐘"),
                    ("題目", "教授在線上課程討論區提問，兩位同學各自表達不同看法"),
                    ("字數", "至少 100 字；建議 120–170 字"),
                    ("評分", "0–5：是否切題並對討論有貢獻、論點是否有具體說明、語言是否精確多樣"),
                ])
            }

            Guide.card("高分結構", icon: "list.number") {
                Guide.structure([
                    ("1", "表明立場 Position", "一句話直接回答教授的問題。", "In my view, cities should invest in protected bike lanes rather than more buses."),
                    ("2", "回應同學 Engage", "點名一位同學，說明你同意或不同意哪一點以及原因，展現你在「討論」。", "I agree with Maya that bike lanes are cheaper to build, but I think Leo underestimates how many short trips could switch to bikes."),
                    ("3", "提出新理由 New reason", "加入兩位同學都沒提到的理由——這是拉開分數的關鍵。", "What neither of them mentioned is that bike lanes also make streets safer for pedestrians."),
                    ("4", "具體例子 Example", "用一個具體的例子、經驗或數據支持新理由。", "For example, after my hometown added protected lanes near schools, the number of students biking doubled within a year."),
                    ("5", "收束 Wrap-up", "一句話回扣立場（可選，時間不夠可省略）。", "For these reasons, bike lanes are the more effective long-term investment."),
                ])
            }

            Guide.card("萬用模板", icon: "doc.text") {
                Guide.template("""
                In my view, [立場：直接回答教授的問題].

                I agree with [同學 A] that [他的論點], but [補充或修正]. / Although [同學 B] makes a good point about [他的論點], I believe [你的看法] because [原因].

                Another important reason is that [兩位同學都沒提到的新理由].
                For example, [具體例子：經驗、案例或數據].

                [可選] That said, [承認對方論點的合理之處]. Overall, [回扣立場].
                """)
            }

            Guide.card("時間分配", icon: "timer") {
                Guide.facts([
                    ("0:00–2:00", "讀教授問題與兩位同學的論點；決定立場和一個「新理由」"),
                    ("2:00–9:00", "依結構寫作，先寫立場與回應同學，再展開新理由和例子"),
                    ("9:00–10:00", "檢查是否點名同學、例子是否具體、文法與拼字"),
                ])
            }

            Guide.card("常用句型", icon: "text.alignleft") {
                Guide.phraseGroup("表明立場", [
                    "In my view, schools should require at least one course in personal finance.",
                    "I strongly believe that the benefits of remote work outweigh the drawbacks.",
                    "While both options have merit, I would choose to invest in public transportation.",
                ])
                Guide.phraseGroup("回應同學", [
                    "I agree with Claire that families shape our basic values, but...",
                    "Paul raises a good point about peer pressure; however, ...",
                    "Unlike Kevin, I don't think cost is the most important factor, because...",
                    "Building on Sofia's idea, I would add that...",
                ])
                Guide.phraseGroup("加入新理由與例子", [
                    "Another factor that neither of my classmates mentioned is...",
                    "What is often overlooked is that...",
                    "For example, when my university switched to online exams, ...",
                    "A clear example of this is...",
                ])
                Guide.phraseGroup("讓步與收束", [
                    "Admittedly, this approach would be expensive at first.",
                    "That said, the long-term benefits are worth the initial cost.",
                    "Overall, I believe that...",
                ])
            }

            Guide.card("常見失分原因", icon: "exclamationmark.triangle") {
                Guide.mistakes([
                    ("只表態，沒有理由", "立場 + 原因 + 例子", "評分要的是「為什麼」，不是只有「是什麼」。"),
                    ("完全沒提到同學", "點名至少一位同學並回應他的論點", "沒有回應會被視為自說自話，而不是參與討論。"),
                    ("重複同學已說過的理由", "提出一個新的理由或角度", "「貢獻」來自新的想法。"),
                    ("This will help society.", "This would cut commuting time for thousands of students.", "籠統的句子沒有說服力，要具體到人、事、數字。"),
                    ("套用舊制長篇開頭", "第一句直接表態", "這不是論說文；10 分鐘內不需要長篇引言。"),
                ])
            }

            Guide.card("交卷前檢查清單", icon: "checklist") {
                Guide.checklist([
                    "第一句直接回答教授的問題",
                    "至少點名一位同學並回應他的論點",
                    "有一個兩位同學都沒提到的新理由",
                    "有具體例子（經驗、案例、數據）",
                    "120 字以上，句型有變化（because、although、which、if 子句）",
                ])
            }

            annotatedDiscussion
        }
    }

    // MARK: - Build a Sentence

    private var sentenceGuide: some View {
        VStack(alignment: .leading, spacing: 22) {
            Guide.card("題型重點", icon: "square.stack.3d.up") {
                Guide.facts([
                    ("題數", "10 題，與 Email、學術討論共用 Writing 時間"),
                    ("題目", "先看一句話（問題或陳述），再把打散的字塊排成合理的回應；部分字已固定在句中，另有一個多餘的干擾字塊"),
                    ("重點", "語序、助動詞與疑問句結構、子句、時態、固定句型"),
                ])
            }
            Guide.card("解題步驟", icon: "list.number") {
                Guide.structure([
                    ("1", "讀對話", "先判斷回應要表達什麼：回答問題、解釋原因、提出建議？", ""),
                    ("2", "找主詞與動詞", "先排出「主詞 + 動詞」，再接受詞與修飾語。", ""),
                    ("3", "看固定字", "句中已固定的字和句尾標點會限制可能的語序。", ""),
                    ("4", "排除干擾字", "多出來的字塊通常是錯誤形式：to／-ing、does／did、which／where、more／most。", ""),
                ])
            }
            Guide.card("最常考的五個陷阱", icon: "exclamationmark.triangle") {
                Guide.mistakes([
                    ("Do you know where is the library?", "Do you know where the library is?", "間接問句用直述句語序（疑問詞 + 主詞 + 動詞），不倒裝，也不加 do／does。"),
                    ("She let me to borrow her notes.", "She let me borrow her notes.", "let／make + 受詞 + 原形動詞，不加 to。"),
                    ("I avoid to eat late.", "I avoid eating late.", "avoid、mind、suggest、finish 後面接 V-ing。"),
                    ("If I would have known, I would come.", "If I had known, I would have come.", "與過去事實相反：If + had p.p., would have p.p.。"),
                    ("I had my laptop fix.", "I had my laptop fixed.", "have／get + 物 + 過去分詞，表示請別人做。"),
                ])
            }
            Guide.card("練習建議", icon: "lightbulb") {
                Guide.checklist([
                    "每天做 5–10 題，限時完成，錯題看中文文法說明",
                    "做完 Writing 練習後，在答案檢討中查看每題的文法重點",
                    "不要在這題花太多時間，保留時間給 Email 和學術討論",
                ])
            }
        }
    }

    // MARK: - Annotated samples

    private var emailSamples: [TOEFLQuestion] {
        TOEFLQuestionBank.allQuestions.filter { $0.taskType == .writeEmail && !($0.sampleAnswer ?? "").isEmpty }
    }

    private var discussionSamples: [TOEFLQuestion] {
        TOEFLQuestionBank.allQuestions.filter { $0.taskType == .academicDiscussion && !($0.sampleAnswer ?? "").isEmpty }
    }

    private var annotatedEmail: some View {
        let samples = emailSamples
        return Guide.card("範例 Email（附結構標示）", icon: "envelope.open") {
            if samples.isEmpty {
                Text("No sample emails in the bank.").foregroundStyle(.secondary)
            } else {
                let question = samples[emailIndex % samples.count]
                sampleNavigator(count: samples.count, index: $emailIndex)
                Text(question.prompt).font(.subheadline).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                if let passage = question.passage {
                    Text(passage).font(.subheadline.monospaced()).fixedSize(horizontal: false, vertical: true)
                }
                Divider()
                ForEach(Array(Self.labeledEmailParts(question.sampleAnswer ?? "", bullets: Self.bullets(in: question.prompt)).enumerated()), id: \.offset) { _, part in
                    labeledPart(part.label, part.text, color: part.color)
                }
                Text("\(Self.wordCount(question.sampleAnswer ?? "")) words")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
    }

    private var annotatedDiscussion: some View {
        let samples = discussionSamples
        return Guide.card("範例回答（標示：點名同學、轉折與舉例）", icon: "text.bubble") {
            if samples.isEmpty {
                Text("No sample responses in the bank.").foregroundStyle(.secondary)
            } else {
                let question = samples[discussionIndex % samples.count]
                sampleNavigator(count: samples.count, index: $discussionIndex)
                Text(question.prompt).font(.subheadline).fixedSize(horizontal: false, vertical: true)
                if let posts = question.passage {
                    Text(posts).font(.subheadline).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
                Divider()
                Text(Self.highlightedDiscussion(question.sampleAnswer ?? "", classmates: Self.classmateNames(question.passage ?? "")))
                    .font(.body)
                    .lineSpacing(4)
                    .fixedSize(horizontal: false, vertical: true)
                HStack(spacing: 14) {
                    Guide.legend("同學名字", TOEFLTheme.action)
                    Guide.legend("轉折、舉例與立場用語", TOEFLTheme.teal)
                    Spacer()
                    Text("\(Self.wordCount(question.sampleAnswer ?? "")) words").font(.caption).foregroundStyle(.secondary)
                }
            }
        }
    }

    private func sampleNavigator(count: Int, index: Binding<Int>) -> some View {
        HStack {
            Text("Sample \(index.wrappedValue % count + 1) of \(count)").font(.caption).foregroundStyle(.secondary)
            Spacer()
            Button { index.wrappedValue = (index.wrappedValue - 1 + count) % count } label: { Image(systemName: "chevron.left") }
            Button("Random") { index.wrappedValue = Int.random(in: 0..<count) }
            Button { index.wrappedValue = (index.wrappedValue + 1) % count } label: { Image(systemName: "chevron.right") }
        }
        .buttonStyle(.bordered)
    }

    struct LabeledPart { let label: String; let text: String; let color: Color }

    /// The task's required points ("• …" lines in the prompt).
    static func bullets(in prompt: String) -> [String] {
        prompt.components(separatedBy: "\n").filter { $0.hasPrefix("• ") }.map { String($0.dropFirst(2)) }
    }

    /// Splits a model email into greeting, purpose, body, closing, and sign-off. When the body has one
    /// paragraph per required point, each paragraph is labeled with the point it answers.
    static func labeledEmailParts(_ email: String, bullets: [String] = []) -> [LabeledPart] {
        let paragraphs = email.components(separatedBy: "\n\n").map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }.filter { !$0.isEmpty }
        guard paragraphs.count >= 3 else { return [LabeledPart(label: "Email", text: email, color: .secondary)] }
        var parts: [LabeledPart] = []
        var body = paragraphs
        let greeting = body.removeFirst()
        parts.append(LabeledPart(label: "① 稱呼", text: greeting, color: TOEFLTheme.action))
        let signOff = body.removeLast()
        // The purpose is the first sentence of the first paragraph; the rest of it belongs to the body.
        let first = body.removeFirst()
        let purposeEnd = first.firstIndex(where: { ".!?".contains($0) }).map { first.index(after: $0) } ?? first.endIndex
        parts.append(LabeledPart(label: "② 開門見山說目的", text: String(first[..<purposeEnd]).trimmingCharacters(in: .whitespaces), color: TOEFLTheme.teal))
        let restOfFirst = String(first[purposeEnd...]).trimmingCharacters(in: .whitespaces)
        var bodyParagraphs = (restOfFirst.isEmpty ? [] : [restOfFirst]) + body
        let closing = bodyParagraphs.count > 1 ? bodyParagraphs.removeLast() : nil
        let matchesPoints = !bullets.isEmpty && bodyParagraphs.count == bullets.count
        for (index, paragraph) in bodyParagraphs.enumerated() {
            let label = matchesPoints ? "③ 要點 \(index + 1)：\(bullets[index])" : "③ 主體：回應要點"
            parts.append(LabeledPart(label: label, text: paragraph, color: TOEFLTheme.navy))
        }
        if let closing { parts.append(LabeledPart(label: "④ 禮貌收尾", text: closing, color: TOEFLTheme.gold)) }
        parts.append(LabeledPart(label: "⑤ 結尾敬語與署名", text: signOff, color: TOEFLTheme.action))
        return parts
    }

    static func classmateNames(_ posts: String) -> [String] {
        posts.components(separatedBy: "\n\n").compactMap { post in
            post.split(separator: ":", maxSplits: 1).first.map { String($0).trimmingCharacters(in: .whitespaces) }
        }
    }

    private static let signalPhrases = [
        "In my view", "In my opinion", "I believe", "I agree", "I disagree", "I would argue", "I think",
        "However", "although", "Although", "While", "That said", "Admittedly", "On the other hand",
        "For example", "For instance", "Another", "In addition", "Moreover", "Furthermore", "Overall",
        "For these reasons", "neither", "Neither", "because", "Because", "As a result", "In contrast",
    ]

    static func highlightedDiscussion(_ text: String, classmates: [String]) -> AttributedString {
        var attributed = AttributedString(text)
        func mark(_ phrase: String, color: Color) {
            var searchRange = attributed.startIndex..<attributed.endIndex
            while let range = attributed[searchRange].range(of: phrase) {
                attributed[range].foregroundColor = color
                attributed[range].font = .body.bold()
                searchRange = range.upperBound..<attributed.endIndex
            }
        }
        for name in classmates where !name.isEmpty { mark(name, color: TOEFLTheme.action) }
        for phrase in signalPhrases { mark(phrase, color: TOEFLTheme.teal) }
        return attributed
    }

    static func wordCount(_ text: String) -> Int {
        text.split(whereSeparator: { $0.isWhitespace }).count
    }

    private func labeledPart(_ label: String, _ text: String, color: Color) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Text(label)
                .font(.caption.bold())
                .foregroundStyle(color)
                .frame(width: 170, alignment: .leading)
                .fixedSize(horizontal: false, vertical: true)
            Text(text)
                .font(.body)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(10)
        .background(color.opacity(0.07), in: RoundedRectangle(cornerRadius: 8))
        .overlay(alignment: .leading) { Rectangle().fill(color).frame(width: 3) }
    }

    #if DEBUG
    /// Renders each topic, for `-renderWritingGuide <directory>`.
    static func renderSnapshots(to directory: URL) {
        for topic in Topic.allCases {
            var view = WritingGuideView(start: { _ in })
            view._topic = State(initialValue: topic)
            let renderer = ImageRenderer(content: view.content.frame(width: 1000).background(TOEFLTheme.canvas))
            renderer.scale = 1
            #if os(macOS)
            if let tiff = renderer.nsImage?.tiffRepresentation, let png = NSBitmapImageRep(data: tiff)?.representation(using: .png, properties: [:]) {
                try? png.write(to: directory.appendingPathComponent("guide-\(topic.rawValue.replacingOccurrences(of: " ", with: "-")).png"))
            }
            #endif
        }
    }
    #endif
}
