using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.App.Exam;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>
/// A study guide for the 2026 Writing section (WritingGuideView): structures, templates, phrase banks, checklists, and
/// annotated model answers from the question bank. Explanations are in Traditional Chinese; phrases to use in answers are
/// in English.
/// </summary>
public sealed partial class WritingGuidePage : Page
{
    private readonly StackPanel topicPanel = new() { Spacing = 22 };
    private WritingGuideTopic topic = WritingGuideTopic.Email;
    private int emailIndex;
    private int discussionIndex;
    private IReadOnlyList<TOEFLQuestion> emailSamples = [];
    private IReadOnlyList<TOEFLQuestion> discussionSamples = [];

    public WritingGuidePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Root.Children.Clear();
        Root.Children.Add(Header());
        Root.Children.Add(topicPanel);
        topicPanel.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Left });
        try
        {
            await AppServices.Ready;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return;
        }
        var bank = AppServices.Core.QuestionBank;
        emailSamples = WritingGuide.EmailSamples(bank);
        discussionSamples = WritingGuide.DiscussionSamples(bank);
        ShowTopic();
    }

    private UIElement Header()
    {
        var bar = new SelectorBar();
        foreach (var item in new[] { WritingGuideTopic.Email, WritingGuideTopic.Discussion, WritingGuideTopic.Sentence })
            bar.Items.Add(new SelectorBarItem { Text = item.Title(), Tag = item, IsSelected = item == topic });
        bar.SelectionChanged += (sender, _) =>
        {
            if (sender.SelectedItem?.Tag is WritingGuideTopic selected && selected != topic)
            {
                topic = selected;
                ShowTopic();
            }
        };
        var practice = new Button { Content = "Practice the Writing section", Style = AppStyle("AccentButtonStyle") };
        practice.Click += (_, _) => ExamLauncher.Start(ExamMode.WritingPractice);
        return GuideTheme.Card(VStack(14,
            Text("Writing guide", PageTitle, FontWeights.Bold, "GuideNavyText"),
            Text("新制 Writing 共三題、約 23 分鐘，每題獨立計時、時間不能互相挪用。Email 與學術討論由 AI 與人工共同評分，重點是「把溝通任務完成得清楚、得體」，而不是堆砌難字。",
                style: "GuideSecondaryText"),
            SpacedRow(bar, practice)));
    }

    private void ShowTopic()
    {
        topicPanel.Children.Clear();
        var cards = topic switch
        {
            WritingGuideTopic.Email => EmailGuide(),
            WritingGuideTopic.Discussion => DiscussionGuide(),
            _ => SentenceGuide(),
        };
        foreach (var card in cards) topicPanel.Children.Add(card);
    }

    private static GuideCard Card(string title, string icon, UIElement[] content) => new(title, icon, content);

    // MARK: - Email

    private UIElement[] EmailGuide() =>
    [
        Card("題型重點", "envelope", [
            new GuideFacts([
                ("時間", "7 分鐘"),
                ("題目", "約 90 字的情境 + 必須完成的 3 個要點；收件人與主旨已填好，只寫內文"),
                ("字數", "建議 150–200 字；少於 100 字通常無法完整回應所有要點"),
                ("評分", "0–5：是否完成任務、細節是否充分、語言是否精確多樣、語氣與書信格式是否得體"),
            ]),
        ]),

        Card("四段式結構", "list.number", [
            new GuideStructure([
                ("1", "稱呼 Greeting", "依關係選擇正式程度，後面加逗號。", "Dear Professor Lin,"),
                ("2", "開門見山說目的 Purpose", "第一句就讓對方知道為什麼寫信；不要寒暄太久。", "I am writing to ask whether I could make up the lab I missed last Thursday."),
                ("3", "主體：依序完成三個要點 Body", "照題目要點的順序寫，每點 2–3 句：原因 → 影響 → 請求／建議，並加入具體細節（時間、地點、數字）。", "Because I had a high fever, I went to the health center and could not attend. Since the lab counts toward my grade, I would like to complete it before the exam. I am free on Monday or Wednesday afternoon."),
                ("4", "禮貌收尾 Closing + 署名", "簡短重申請求、感謝對方，再署名。", "Thank you for considering my request. I look forward to hearing from you.\n\nBest regards,\nAmy"),
            ]),
        ]),

        Card("萬用模板（依情境替換括號）", "doc.text", [
            new GuideTemplate("""
            Dear [Professor / Ms. / Mr. + 姓氏],

            I am writing to [ask about / request / let you know about] [主題].

            [要點 1：說明背景或原因，加一個具體細節。]
            [要點 2：說明影響，或提出問題／解釋。]
            [要點 3：提出請求、建議或替代方案，盡量具體（時間、方式）。]

            Thank you for your time and understanding. I look forward to hearing from you.

            Best regards,
            [名字]
            """),
        ]),

        Card("時間分配", "timer", [
            new GuideFacts([
                ("0:00–1:00", "讀情境，圈出三個要點與收件人身分，決定語氣"),
                ("1:00–6:00", "依四段式寫作；每個要點至少一個具體細節"),
                ("6:00–7:00", "檢查三點是否都完成、語氣是否一致、拼字與標點"),
            ]),
        ]),

        Card("稱呼與結尾：依關係選擇", "person.2", [
            new GuidePhraseTable(["對象", "稱呼", "結尾", "署名"], [
                ["教授、主管", "Dear Professor Chen, / Dear Dr. Patel,", "Thank you for your time and consideration.", "Sincerely, / Best regards,"],
                ["行政單位、不知姓名", "Dear Housing Office, / Dear Hiring Manager,", "I would appreciate your help with this matter.", "Sincerely,"],
                ["房東、客服、店家", "Dear Mr. Lopez, / Dear Customer Service Team,", "I look forward to your reply.", "Best regards,"],
                ["同學、同事", "Hi Jordan,", "Let me know what you think.", "Best, / Thanks,"],
                ["朋友、家人", "Hi Aunt Mei, / Hey Sam,", "Can't wait to hear from you!", "Take care, / Love,"],
            ]),
            new GuideNote("不知道對方姓名時，用單位名稱比 Dear Sir or Madam 自然；稱呼教授時用 Professor + 姓氏，不要用名字。"),
        ]),

        Card("開頭第一句：依目的", "text.quote", [
            new GuidePhrases([
                ("請求", "I am writing to request an extension on my research paper."),
                ("詢問", "I am writing to ask about the application process for the exchange program."),
                ("反映問題", "I am writing to let you know about a problem with the laundry machines on the third floor."),
                ("道歉", "I am writing to apologize for missing our meeting on Tuesday."),
                ("建議", "I would like to suggest a small change to our weekly meeting time."),
                ("感謝", "Thank you so much for helping me prepare for my interview last week."),
                ("回覆邀請", "Thank you for inviting me to your graduation party."),
                ("追蹤", "I am following up on the email I sent last Monday about my registration."),
            ]),
        ]),

        Card("主體常用句型", "text.alignleft", [
            new GuidePhraseGroup("禮貌提出請求（由普通到最客氣）", [
                "Could you send me the updated schedule?",
                "Would it be possible to move my presentation to Friday?",
                "I was wondering if you might be able to write me a recommendation letter.",
                "I would really appreciate it if you could look into this issue.",
            ]),
            new GuidePhraseGroup("說明原因與影響", [
                "The reason is that my shift was changed at the last minute.",
                "Because the heater has been broken since Monday, my room is too cold to study in.",
                "As a result, I was unable to submit the assignment on time.",
                "This has made it difficult for me to prepare for my exams.",
            ]),
            new GuidePhraseGroup("提出建議或替代方案", [
                "One option would be to meet online instead.",
                "Perhaps we could split the work so that each person writes one section.",
                "If that is not possible, I would be happy to come in on Saturday morning.",
                "I am available on Monday after 2 p.m. or any time on Wednesday.",
            ]),
            new GuidePhraseGroup("得體地拒絕", [
                "Unfortunately, I won't be able to attend because I have a family commitment that weekend.",
                "I'm afraid I can't take on another project right now, but I hope we can work together in the future.",
            ]),
            new GuidePhraseGroup("道歉與負責", [
                "I am very sorry for the inconvenience this caused.",
                "It was my mistake, and I take full responsibility.",
                "To make up for it, I will replace the book by Friday.",
            ]),
            new GuidePhraseGroup("禮貌地抱怨", [
                "I wanted to bring a problem to your attention.",
                "I understand that repairs can take time; however, the issue has continued for two weeks.",
            ]),
        ]),

        Card("語氣階梯：同一件事，越往下越客氣", "slider.horizontal.3", [
            new GuidePhraseTable(["直接（避免）", "禮貌", "更委婉（給教授、主管）"], [
                ["Send me the slides.", "Could you send me the slides?", "Would it be possible for you to share the slides from Monday's lecture?"],
                ["I want a refund.", "I would like to request a refund.", "I would appreciate it if you could process a refund for this order."],
                ["Change my room.", "Can I change my room?", "I was wondering whether it might be possible to move to a different room."],
                ["You are wrong about my grade.", "I think there may be a mistake in my grade.", "I wanted to ask whether you might be willing to take another look at my essay grade."],
            ]),
        ]),

        Card("台灣考生常見錯誤", "exclamationmark.triangle", [
            new GuideMistakes([
                ("Please reply me.", "Please reply to me. / I look forward to your reply.", "reply 是不及物動詞，要用 reply to。"),
                ("I want you to give me an extension.", "Would it be possible to have a short extension?", "want 對教授太直接，用 would / could 降低強度。"),
                ("Hope you can help me.", "I hope you can help me.", "正式信件不要省略主詞。"),
                ("Dear Professor Daniel,", "Dear Professor Lin,", "Professor 後面接姓氏，不是名字。"),
                ("I'm so sad that I can't come!!!", "Unfortunately, I won't be able to attend.", "避免情緒化字眼與多個驚嘆號，保持語氣穩定。"),
                ("只寫兩個要點", "三個要點各寫 2–3 句", "漏掉任何一個要點都會直接影響「任務完成度」。"),
            ]),
        ]),

        Card("交卷前檢查清單", "checklist", [
            new GuideChecklist([
                "第一句就說明寫信目的",
                "三個要點都有回應，且依題目順序",
                "每個要點至少有一個具體細節（日期、時間、地點、原因）",
                "稱呼、結尾、署名與收件人的關係一致",
                "請求用 could / would / I was wondering if 等客氣說法",
                "150 字以上，沒有口語縮寫（gonna、u、thx）",
            ]),
        ]),

        AnnotatedEmail(),
    ];

    // MARK: - Academic Discussion

    private UIElement[] DiscussionGuide() =>
    [
        Card("題型重點", "bubble.left.and.bubble.right", [
            new GuideFacts([
                ("時間", "10 分鐘"),
                ("題目", "教授在線上課程討論區提問，兩位同學各自表達不同看法"),
                ("字數", "至少 100 字；建議 120–170 字"),
                ("評分", "0–5：是否切題並對討論有貢獻、論點是否有具體說明、語言是否精確多樣"),
            ]),
        ]),

        Card("高分結構", "list.number", [
            new GuideStructure([
                ("1", "表明立場 Position", "一句話直接回答教授的問題。", "In my view, cities should invest in protected bike lanes rather than more buses."),
                ("2", "回應同學 Engage", "點名一位同學，說明你同意或不同意哪一點以及原因，展現你在「討論」。", "I agree with Maya that bike lanes are cheaper to build, but I think Leo underestimates how many short trips could switch to bikes."),
                ("3", "提出新理由 New reason", "加入兩位同學都沒提到的理由——這是拉開分數的關鍵。", "What neither of them mentioned is that bike lanes also make streets safer for pedestrians."),
                ("4", "具體例子 Example", "用一個具體的例子、經驗或數據支持新理由。", "For example, after my hometown added protected lanes near schools, the number of students biking doubled within a year."),
                ("5", "收束 Wrap-up", "一句話回扣立場（可選，時間不夠可省略）。", "For these reasons, bike lanes are the more effective long-term investment."),
            ]),
        ]),

        Card("萬用模板", "doc.text", [
            new GuideTemplate("""
            In my view, [立場：直接回答教授的問題].

            I agree with [同學 A] that [他的論點], but [補充或修正]. / Although [同學 B] makes a good point about [他的論點], I believe [你的看法] because [原因].

            Another important reason is that [兩位同學都沒提到的新理由].
            For example, [具體例子：經驗、案例或數據].

            [可選] That said, [承認對方論點的合理之處]. Overall, [回扣立場].
            """),
        ]),

        Card("時間分配", "timer", [
            new GuideFacts([
                ("0:00–2:00", "讀教授問題與兩位同學的論點；決定立場和一個「新理由」"),
                ("2:00–9:00", "依結構寫作，先寫立場與回應同學，再展開新理由和例子"),
                ("9:00–10:00", "檢查是否點名同學、例子是否具體、文法與拼字"),
            ]),
        ]),

        Card("常用句型", "text.alignleft", [
            new GuidePhraseGroup("表明立場", [
                "In my view, schools should require at least one course in personal finance.",
                "I strongly believe that the benefits of remote work outweigh the drawbacks.",
                "While both options have merit, I would choose to invest in public transportation.",
            ]),
            new GuidePhraseGroup("回應同學", [
                "I agree with Claire that families shape our basic values, but...",
                "Paul raises a good point about peer pressure; however, ...",
                "Unlike Kevin, I don't think cost is the most important factor, because...",
                "Building on Sofia's idea, I would add that...",
            ]),
            new GuidePhraseGroup("加入新理由與例子", [
                "Another factor that neither of my classmates mentioned is...",
                "What is often overlooked is that...",
                "For example, when my university switched to online exams, ...",
                "A clear example of this is...",
            ]),
            new GuidePhraseGroup("讓步與收束", [
                "Admittedly, this approach would be expensive at first.",
                "That said, the long-term benefits are worth the initial cost.",
                "Overall, I believe that...",
            ]),
        ]),

        Card("常見失分原因", "exclamationmark.triangle", [
            new GuideMistakes([
                ("只表態，沒有理由", "立場 + 原因 + 例子", "評分要的是「為什麼」，不是只有「是什麼」。"),
                ("完全沒提到同學", "點名至少一位同學並回應他的論點", "沒有回應會被視為自說自話，而不是參與討論。"),
                ("重複同學已說過的理由", "提出一個新的理由或角度", "「貢獻」來自新的想法。"),
                ("This will help society.", "This would cut commuting time for thousands of students.", "籠統的句子沒有說服力，要具體到人、事、數字。"),
                ("套用舊制長篇開頭", "第一句直接表態", "這不是論說文；10 分鐘內不需要長篇引言。"),
            ]),
        ]),

        Card("交卷前檢查清單", "checklist", [
            new GuideChecklist([
                "第一句直接回答教授的問題",
                "至少點名一位同學並回應他的論點",
                "有一個兩位同學都沒提到的新理由",
                "有具體例子（經驗、案例、數據）",
                "120 字以上，句型有變化（because、although、which、if 子句）",
            ]),
        ]),

        AnnotatedDiscussion(),
    ];

    // MARK: - Build a Sentence

    private static UIElement[] SentenceGuide() =>
    [
        Card("題型重點", "square.stack.3d.up", [
            new GuideFacts([
                ("題數", "10 題，與 Email、學術討論共用 Writing 時間"),
                ("題目", "先看一句話（問題或陳述），再把打散的字塊排成合理的回應；另有一個多餘的干擾字塊"),
                ("提示字", "實際考試通常不給開頭：整句都要自己排，最多只在句尾固定一個字，或完全沒有提示字。開頭那個字塊通常是小寫，不能靠大寫判斷誰排第一"),
                ("重點", "語序、助動詞與疑問句結構、子句、時態、固定句型"),
            ]),
        ]),
        Card("解題步驟", "list.number", [
            new GuideStructure([
                ("1", "讀對話", "先判斷回應要表達什麼：回答問題、解釋原因、提出建議？", ""),
                ("2", "決定句型與開頭", "沒有提示字時，先看句尾標點：問號 → 疑問詞或助動詞開頭；句號 → 主詞開頭（I、we、the + 名詞、it、there）。回應問句時，常以 I／it／we 開頭。", ""),
                ("3", "找主詞與動詞", "先排出「主詞 + 動詞」這個核心，再接受詞、補語，最後放時間、地點等修飾語。句尾有固定字時，它會限制最後一個字塊。", ""),
                ("4", "排除干擾字", "多出來的字塊通常是錯誤形式：to／-ing、does／did、which／where、more／most。排完後唸一遍，確認每個字塊都用上、沒有多一個助動詞。", ""),
            ]),
        ]),
        Card("最常考的五個陷阱", "exclamationmark.triangle", [
            new GuideMistakes([
                ("Do you know where is the library?", "Do you know where the library is?", "間接問句用直述句語序（疑問詞 + 主詞 + 動詞），不倒裝，也不加 do／does。"),
                ("She let me to borrow her notes.", "She let me borrow her notes.", "let／make + 受詞 + 原形動詞，不加 to。"),
                ("I avoid to eat late.", "I avoid eating late.", "avoid、mind、suggest、finish 後面接 V-ing。"),
                ("If I would have known, I would come.", "If I had known, I would have come.", "與過去事實相反：If + had p.p., would have p.p.。"),
                ("I had my laptop fix.", "I had my laptop fixed.", "have／get + 物 + 過去分詞，表示請別人做。"),
            ]),
        ]),
        Card("練習建議", "lightbulb", [
            new GuideChecklist([
                "每天做 5–10 題，限時完成，錯題看中文文法說明",
                "做完 Writing 練習後，在答案檢討中查看每題的文法重點",
                "不要在這題花太多時間，保留時間給 Email 和學術討論",
            ]),
        ]),
    ];

    // MARK: - Annotated samples

    private GuideCard AnnotatedEmail()
    {
        var card = Card("範例 Email（附結構標示）", "envelope.open", []);
        var body = new StackPanel { Spacing = 12 };
        card.Body.Children.Add(body);
        void Fill()
        {
            body.Children.Clear();
            if (emailSamples.Count == 0)
            {
                body.Children.Add(Text("No sample emails in the bank.", style: "GuideSecondaryText"));
                return;
            }
            var question = emailSamples[emailIndex % emailSamples.Count];
            body.Children.Add(SampleNavigator(emailSamples.Count, emailIndex, index => { emailIndex = index; Fill(); }));
            body.Children.Add(Text(question.Prompt, Subheadline, style: "GuideSecondaryText"));
            if (question.Passage is { } passage) body.Children.Add(Text(passage, Subheadline, font: Monospaced));
            body.Children.Add(Divider());
            foreach (var part in WritingGuide.LabeledEmailParts(question.SampleAnswer ?? "", WritingGuide.Bullets(question.Prompt)))
                body.Children.Add(LabeledPart(part));
            body.Children.Add(Secondary($"{WritingGuide.WordCount(question.SampleAnswer ?? "")} words"));
        }
        Fill();
        return card;
    }

    private GuideCard AnnotatedDiscussion()
    {
        var card = Card("範例回答（標示：點名同學、轉折與舉例）", "text.bubble", []);
        var body = new StackPanel { Spacing = 12 };
        card.Body.Children.Add(body);
        void Fill()
        {
            body.Children.Clear();
            if (discussionSamples.Count == 0)
            {
                body.Children.Add(Text("No sample responses in the bank.", style: "GuideSecondaryText"));
                return;
            }
            var question = discussionSamples[discussionIndex % discussionSamples.Count];
            body.Children.Add(SampleNavigator(discussionSamples.Count, discussionIndex, index => { discussionIndex = index; Fill(); }));
            body.Children.Add(Text(question.Prompt, Subheadline));
            if (question.Passage is { } posts) body.Children.Add(Text(posts, Subheadline, style: "GuideSecondaryText"));
            body.Children.Add(Divider());
            var spans = WritingGuide.HighlightedDiscussion(question.SampleAnswer ?? "", WritingGuide.ClassmateNames(question.Passage ?? ""))
                .Select(span => new TextSpan(span.Text, span.Color, span.Color is not null)).ToList();
            body.Children.Add(new SpanText(spans, Body, lineSpacing: 4));
            body.Children.Add(SpacedRow(
                HStack(14, new GuideLegend("同學名字", "Action"), new GuideLegend("轉折、舉例與立場用語", "Teal")),
                Secondary($"{WritingGuide.WordCount(question.SampleAnswer ?? "")} words")));
        }
        Fill();
        return card;
    }

    /// <summary>"Sample 3 of 100" with previous, Random, and next buttons (wrapping around).</summary>
    private static Grid SampleNavigator(int count, int index, Action<int> select)
    {
        var previous = new Button { Content = Icon(GuideIcons.ChevronLeft, 12) };
        AutomationProperties.SetName(previous, "Previous sample");
        previous.Click += (_, _) => select(WritingGuide.PreviousIndex(index, count));
        var random = new Button { Content = "Random" };
        random.Click += (_, _) => select(Random.Shared.Next(count));
        var next = new Button { Content = Icon(GuideIcons.ChevronRight, 12) };
        AutomationProperties.SetName(next, "Next sample");
        next.Click += (_, _) => select(WritingGuide.NextIndex(index, count));
        return SpacedRow(Secondary(WritingGuide.SampleCaption(index, count)), HStack(8, previous, random, next));
    }

    /// <summary>One annotated part of the model email: a colored label beside the text, on a tinted background with a
    /// colored bar on the leading edge.</summary>
    private static Grid LabeledPart(WritingGuide.LabeledPart part)
    {
        var color = ColorName(part.Color);
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(Text(part.Label, Caption, FontWeights.Bold, $"Guide{color}Text"));
        var text = Text(part.Text, Body, selectable: true);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var host = new Grid();
        host.Children.Add(new Border { Style = Style($"GuidePart{color}"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = grid });
        host.Children.Add(new Border { Style = Style($"GuideBar{color}"), Width = 3, HorizontalAlignment = HorizontalAlignment.Left });
        return host;
    }
}
