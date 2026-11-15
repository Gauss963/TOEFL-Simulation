using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.App.Exam;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>
/// A study guide for the 2026 Speaking section (SpeakingGuideView), combining ETS's task descriptions and scoring criteria
/// with test-prep advice, plus practice widgets that play real prompts from the bank. Explanations are in Traditional
/// Chinese; phrases to use in answers are in English.
/// </summary>
public sealed partial class SpeakingGuidePage : Page
{
    private readonly StackPanel topicPanel = new() { Spacing = 22 };
    private SpeakingGuideTopic topic = SpeakingGuideTopic.RepeatTask;

    public SpeakingGuidePage()
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
        ShowTopic();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Removing the trainers stops their audio and countdowns (their Unloaded handlers), like onDisappear.
        topicPanel.Children.Clear();
    }

    private UIElement Header()
    {
        var bar = new SelectorBar();
        foreach (var item in new[] { SpeakingGuideTopic.RepeatTask, SpeakingGuideTopic.Interview, SpeakingGuideTopic.Delivery })
            bar.Items.Add(new SelectorBarItem { Text = item.Title(), Tag = item, IsSelected = item == topic });
        bar.SelectionChanged += (sender, _) =>
        {
            if (sender.SelectedItem?.Tag is SpeakingGuideTopic selected && selected != topic)
            {
                topic = selected;
                ShowTopic();
            }
        };
        var practice = new Button { Content = "Practice the Speaking section", Style = AppStyle("AccentButtonStyle") };
        practice.Click += (_, _) => ExamLauncher.Start(ExamMode.SpeakingPractice);
        return GuideTheme.Card(VStack(14,
            Text("Speaking guide", PageTitle, FontWeights.Bold, "GuideNavyText"),
            Text("新制 Speaking 共 11 題、約 8 分鐘：Listen and Repeat 7 題 + Take an Interview 4 題，全部沒有準備時間。ETS 說明這一科評量你能否清楚、有效地用英文溝通：複述是否準確、聽起來是否清楚易懂、說話節奏是否自然、用字與文法是否恰當。題目取材自校園與學術情境，可能聽到北美、英國、紐西蘭或澳洲口音。",
                style: "GuideSecondaryText"),
            SpacedRow(bar, practice)));
    }

    private void ShowTopic()
    {
        topicPanel.Children.Clear();
        var cards = topic switch
        {
            SpeakingGuideTopic.RepeatTask => RepeatGuide(),
            SpeakingGuideTopic.Interview => InterviewGuide(),
            _ => DeliveryGuide(),
        };
        foreach (var card in cards) topicPanel.Children.Add(card);
    }

    private static GuideCard Card(string title, string icon, UIElement[] content) => new(title, icon, content);

    // MARK: - Listen and Repeat

    private static UIElement[] RepeatGuide() =>
    [
        Card("題型重點", "repeat", [
            new GuideFacts([
                ("題數", "7 句，同一個情境（例如在動物園當志工，聽主管說明）"),
                ("作答", "每句播完後立刻複述一次；回答時間約 8–12 秒，句子越後面越長、越複雜"),
                ("畫面", "會出現情境示意圖，只是背景；任務只有「照原句說一遍」"),
                ("評分", "每句 0–5 分：完全照原句說出且清楚易懂得 5 分；意思正確但有小改動（漏冠詞、時態或單複數、語序微調）約 4 分；句子大致完整但意思跑掉約 3 分；漏掉大段約 2 分；只說出一點點約 1 分；沒說或聽不懂 0 分"),
            ]),
            new GuideNote("這題不考你的意見或組織能力，而是聽辨、短期記憶、發音與語調。目標是一字不差，不是改寫。"),
        ]),

        Card("三步驟作答法", "list.number", [
            new GuideStructure([
                ("1", "邊聽邊切意群", "不要一個字一個字記，而是把句子切成 2–3 個「意群」（有意義的片段）。", "If a visitor feels sick / or gets hurt, / bring them to the first aid booth right away."),
                ("2", "先抓骨架", "先記住主詞、動詞和句尾的關鍵資訊（地點、時間、數字），再補細節。句尾資訊最容易忘。", "visitor sick / hurt → bring → first aid booth → right away"),
                ("3", "提示音後立刻說", "照原句的停頓與語調說出來，說完就停。忘了一小段時，保持句子文法完整、意思不變，不要整句放棄或重新開始。", ""),
            ]),
        ]),

        ListenRepeatTrainerCard(),

        Card("最常被扣分的地方", "exclamationmark.triangle", [
            new GuideMistakes([
                ("The penguin are fed every morning.", "The penguins are fed every morning.", "漏掉字尾 -s、-ed：這類小字最容易被吞掉，會從 5 分掉到 4 分。"),
                ("Volunteers get small discount at gift shop.", "Volunteers get a small discount at the gift shop.", "漏掉冠詞 a、the。中文沒有冠詞，要刻意聽、刻意說。"),
                ("You can get a map here.", "You can grab a map right here.", "換成自己的說法（paraphrase）：意思對也只有 4 分，原句用字要照說。"),
                ("…bring them to the… uh… sorry, if a visitor…", "停頓半秒後接著說完", "重新開始或道歉會浪費時間，也讓評分聽起來不流暢。"),
                ("每句都用同一個平調唸完", "模仿原句的重音和語調", "評分包含語調是否自然；把重音放在關鍵字（名詞、動詞、數字）。"),
            ]),
        ]),

        Card("練習方法", "figure.run", [
            new GuidePhraseGroup("每天 10–15 分鐘 Shadowing（影子跟讀）", [
                "選校園對話、Podcast 或本 app 的 Listening 錄音，以意群為單位，比原音晚半拍跟著說。",
                "先看文字跟讀，熟了以後不看文字，最後只聽一次就複述。",
            ]),
            new GuidePhraseGroup("錄音比對", [
                "做完 Speaking 練習後，在成績報告的答案檢討中看「辨識出的文字」，和原句逐字比對。",
                "辨識結果和原句不同的地方，通常就是發音不清或漏字的地方。",
            ]),
            new GuidePhraseGroup("逐步拉長", [
                "先練 6–8 個字的短句，穩定 5 分後再練 12–16 個字、含子句的長句。",
                "長句練「意群記憶」：聽完先在心裡默念骨架，再完整說出。",
            ]),
        ]),
    ];

    // MARK: - Interview

    private static UIElement[] InterviewGuide() =>
    [
        Card("題型重點", "person.crop.rectangle", [
            new GuideFacts([
                ("題數", "4 題，同一個主題的模擬訪談（影片中的訪談者提問）"),
                ("作答", "每題 45 秒，沒有準備時間；題目只用聽的，不會顯示在螢幕上"),
                ("難度", "逐題提高：描述自己的習慣 → 講一次經驗 → 比較或討論挑戰 → 表達立場"),
                ("評分", "每題 0–5 分：是否切題、內容是否充分展開、說話節奏是否穩定自然、發音是否清楚（重音與語調能傳達意思）、文法與用字是否正確多樣"),
                ("長度", "以正常語速說滿約 40–45 秒，大約 90–120 字"),
            ]),
        ]),

        Card("四題的漸進邏輯", "stairs", [
            new GuidePhraseTable(["題目", "常見問法", "重點"], [
                ["第 1 題 暖身", "Where do you usually…? What does a typical… look like?", "用現在式描述習慣；直接回答，再補 2–3 個細節"],
                ["第 2 題 經驗", "Think about a time when… How did it turn out?", "用過去式講一個具體事件：背景 → 你做了什麼 → 結果"],
                ["第 3 題 比較", "Some people…, while others… Which do you prefer?", "選一邊，說主要理由 + 例子，可以承認另一邊的優點"],
                ["第 4 題 立場", "Do you think schools should…? Why or why not?", "清楚表態，一個理由說完整 + 現實例子，比三個理由都說一半好"],
            ]),
        ]),

        Card("45 秒回答結構", "list.number", [
            new GuideStructure([
                ("1", "直接回答（約 5–8 秒）", "第一句就回答問題，不要先鋪陳背景（中文習慣先講背景，英文要先講重點）。", "Honestly, I'd say I prefer studying alone."),
                ("2", "主要理由（約 15 秒）", "說明為什麼，加一點細節。", "The main reason is that I can control my own pace."),
                ("3", "具體例子（約 15 秒）", "親身經驗、具體的人事時地物——這是拉開分數的關鍵。", "For example, last semester my study group met for three hours but only covered one problem set."),
                ("4", "收尾（約 5 秒，可省略）", "一句話回扣答案；如果還有時間，也可以繼續補充例子直到時間到。", "So overall, I study alone and join a group only for final review."),
            ]),
        ]),

        InterviewTrainerCard(),

        Card("範例回答（第 1 組：大學生的讀書習慣）", "text.bubble", [
            Text("以下四題來自題庫的同一組訪談，示範難度逐題提高時回答怎麼調整。每段約 95–110 字，以正常語速約 40–45 秒。",
                Subheadline, style: "GuideSecondaryText"),
            .. SpeakingGuide.ModelAnswers.Select(ModelAnswerView),
        ]),

        Card("常用句型", "text.alignleft", [
            new GuidePhraseGroup("立刻開口回答", [
                "Honestly, I'd say…",
                "For me, it's definitely…",
                "I usually… / Most of the time, I…",
                "That's an interesting question. I think…（只在需要一秒思考時用一次）",
            ]),
            new GuidePhraseGroup("說明理由", [
                "The main reason is that…",
                "This is mainly because…",
                "What I like most about it is that…",
            ]),
            new GuidePhraseGroup("舉例與講經驗（用過去式）", [
                "For example, last semester I…",
                "I remember one time when…",
                "At first, I… Then I… In the end, it turned out…",
            ]),
            new GuidePhraseGroup("比較、讓步與收尾", [
                "That said, group study is useful right before an exam.",
                "On the other hand, …",
                "So overall, I think…",
                "That's why I…",
            ]),
        ]),

        Card("常見失分原因", "exclamationmark.triangle", [
            new GuideMistakes([
                ("先講一大段背景，最後才回答", "第一句直接回答", "英文溝通習慣先講主旨；先鋪陳會讓評分者抓不到重點。"),
                ("第 1 題講太長、後面才發現時間不夠", "每題都用同樣的四步結構", "每題獨立計時 45 秒，但第 1 題說太多會影響整體節奏與專注。"),
                ("I think it's good because it's good.", "One clear reason + a real example", "循環論證沒有內容；具體的例子才是「充分展開」。"),
                ("背好的長模板硬套", "記住幾個開頭句型，內容即時發揮", "新制沒有準備時間，模板聽起來不自然，也常答非所問。"),
                ("講經驗時用現在式", "Last year, I had to…", "第 2 題講過去的事件要用過去式，時態錯誤很容易被扣分。"),
                ("um… uh… like…", "短暫停頓或 Well, …", "贅詞過多會影響「流暢度」；寧可停半秒。"),
                ("說到 30 秒就停", "用例子或補充說滿 40–45 秒", "太短通常無法「充分展開」，也會被扣分。"),
            ]),
        ]),

        Card("建立個人素材庫", "tray.full", [
            Text("訪談主題多半是校園與日常：讀書方法、科技使用、交通、飲食、運動、打工、社團、旅行、環保。先為每個主題準備 1–2 個真實經驗（時間、地點、發生什麼、結果），臨場就能快速套用，比背模板更自然。", Subheadline),
            new GuideChecklist([
                "一次準備周全的考試（讀書方法、團體 vs 個人）",
                "一次用科技解決問題（App、線上課程、AI 工具）",
                "一次社團或打工的挑戰（時間管理、團隊合作）",
                "一次旅行或搬家的經驗（適應新環境）",
                "一個改變你習慣的決定（運動、飲食、作息）",
            ]),
        ]),
    ];

    // MARK: - Pronunciation & fluency

    private static UIElement[] DeliveryGuide() =>
    [
        Card("評分看的「表達」是什麼", "waveform", [
            new GuideFacts([
                ("清楚易懂", "評分者不需要費力就聽得懂；不要求美式或英式口音"),
                ("節奏與語速", "穩定的對話語速，約每分鐘 130–150 字；不要為了塞內容而說太快"),
                ("重音與語調", "重要的字加重、句尾語調正確，能幫助傳達意思"),
                ("流暢度", "停頓自然，贅詞與重新開始少"),
            ]),
        ]),

        Card("台灣考生常見發音問題", "mouth", [
            new GuidePhraseTable(["問題", "例子", "怎麼練"], [
                ["字尾子音被吞掉", "need、hard、book、twelve", "刻意把最後的 /d/ /t/ /k/ 收乾淨，錄音聽看看有沒有"],
                ["-s、-ed 字尾漏掉", "students、helped、decided", "Listen and Repeat 最常因此扣分；名詞複數和過去式特別注意"],
                ["th 發成 s 或 d", "think、three、this、other", "舌尖輕放在上下齒之間送氣"],
                ["v 和 w、b 混淆", "very、video、vote", "上齒輕碰下唇再發聲"],
                ["l 和 n、r 混淆", "light / night、right / light", "l 舌尖頂上齒齦；r 舌頭不碰任何地方"],
                ["長短母音不分", "ship / sheep、full / fool", "短母音短而放鬆，長母音拉長"],
                ["每個音節都一樣重", "comPUter、deVElop、PHOtograph", "英文是重音節奏，非重音音節要輕、短"],
                ["字與字斷開唸", "pick it up → pi-ki-tup", "子音接母音時連音，句子會更自然"],
            ]),
        ]),

        Card("用這個 app 找出自己的問題", "magnifyingglass", [
            new GuideChecklist([
                "做 Speaking 練習後，打開成績報告的答案檢討，看每題「辨識出的文字」",
                "Listen and Repeat：辨識結果和原句不同的字，多半是發音不清或漏字",
                "Interview：檢查是否有很多 um、uh，回答是否說滿 40 秒以上",
                "把常錯的字記下來，用上表的方法單獨練，再回到整句",
            ]),
        ]),

        Card("四週練習計畫", "calendar", [
            new GuidePhraseTable(["週", "Listen and Repeat", "Take an Interview"], [
                ["第 1 週", "每天 Shadowing 10 分鐘；熟悉意群切分", "背熟每個步驟的 2–3 個開頭句；每天答 2 題，不計時"],
                ["第 2 週", "每天練 1 組（7 句），錄音比對", "每天 1 組（4 題）計時 45 秒，錄音聽自己的結構"],
                ["第 3 週", "專練長句與常漏的字尾", "建立個人素材庫；練第 2、4 題的經驗與立場"],
                ["第 4 週", "完整 Speaking 模擬，連續作答", "完整模擬並檢討贅詞、時態與長度；考前降低強度"],
            ]),
        ]),
    ];

    // MARK: - Practice widgets

    private static GuideCard ListenRepeatTrainerCard()
    {
        var player = AppServices.Core.CreateExamAudioPlayer(AppServices.Audio);
        return Card("練習：聽一句、複述一句", "speaker.wave.2",
            [new ListenRepeatTrainerView(AppServices.Core.CreateListenRepeatTrainer(player), player)]);
    }

    private static GuideCard InterviewTrainerCard()
    {
        var player = AppServices.Core.CreateExamAudioPlayer(AppServices.Audio);
        return Card("練習：聽題目、45 秒作答", "timer",
            [new InterviewTrainerView(AppServices.Core.CreateInterviewTrainer(player), player)]);
    }

    // MARK: - Model answers

    private static Border ModelAnswerView(SpeakingGuide.ModelAnswer answer, int index)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Text($"Q{index + 1}. {answer.Question}", Subheadline, FontWeights.Bold));
        foreach (var (label, text) in answer.Parts)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(Text(label, Caption, FontWeights.Bold, "GuideTealText"));
            var body = Text(text, Body, selectable: true);
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            stack.Children.Add(row);
        }
        var words = TextUtil.WordCount(string.Join(" ", answer.Parts.Select(part => part.Text)));
        stack.Children.Add(Secondary($"{words} words · {answer.Note}"));
        return new Border { Style = Style("GuideInsetBorder"), Padding = new Thickness(12), Child = stack };
    }
}
