namespace TOEFLSimulation.Core;

/// <summary>Swift <c>ReadingQuestionType</c>: Reading question types, with the labels and tips shown in practice.</summary>
public enum ReadingQuestionType
{
    Vocabulary, Factual, NegativeFactual, Detail, Inference, Application, Purpose, Relationship, Paraphrase, MainIdea, Attitude,
}

public static class ReadingQuestionTypes
{
    /// <summary>All cases in the Swift declaration order.</summary>
    public static readonly IReadOnlyList<ReadingQuestionType> AllCases = Enum.GetValues<ReadingQuestionType>();

    /// <summary>The raw value used in the bank's <c>questionType</c> field (e.g. <c>negative_factual</c>).</summary>
    public static string RawValue(this ReadingQuestionType type) => type switch
    {
        ReadingQuestionType.Vocabulary => "vocabulary",
        ReadingQuestionType.Factual => "factual",
        ReadingQuestionType.NegativeFactual => "negative_factual",
        ReadingQuestionType.Detail => "detail",
        ReadingQuestionType.Inference => "inference",
        ReadingQuestionType.Application => "application",
        ReadingQuestionType.Purpose => "purpose",
        ReadingQuestionType.Relationship => "relationship",
        ReadingQuestionType.Paraphrase => "paraphrase",
        ReadingQuestionType.MainIdea => "main_idea",
        _ => "attitude",
    };

    public static ReadingQuestionType? FromRawValue(string? raw)
    {
        foreach (var type in AllCases)
            if (type.RawValue() == raw) return type;
        return null;
    }

    public static string Title(this ReadingQuestionType type) => type switch
    {
        ReadingQuestionType.Vocabulary => "Vocabulary 詞彙",
        ReadingQuestionType.Factual => "Factual 細節",
        ReadingQuestionType.NegativeFactual => "NOT/EXCEPT 否定細節",
        ReadingQuestionType.Detail => "Detail 細節",
        ReadingQuestionType.Inference => "Inference 推論",
        ReadingQuestionType.Application => "Application 規則應用",
        ReadingQuestionType.Purpose => "Purpose 目的",
        ReadingQuestionType.Relationship => "Relationship 段落關係",
        ReadingQuestionType.Paraphrase => "Paraphrase 句意改寫",
        ReadingQuestionType.MainIdea => "Main idea 主旨",
        _ => "Attitude 作者態度",
    };

    public static string Tip(this ReadingQuestionType type) => type switch
    {
        ReadingQuestionType.Vocabulary => "用整句意思判斷，答案是「在這句話裡」的意思，常不是最常見的字義；把選項代回句子檢查。",
        ReadingQuestionType.Factual or ReadingQuestionType.Detail => "先在文中找到對應句，再比對選項；正確答案通常是改寫，照抄原文字詞的選項要小心。",
        ReadingQuestionType.NegativeFactual => "NOT/EXCEPT：逐一在文中確認三個「有提到」的選項，剩下的才是答案。",
        ReadingQuestionType.Inference => "答案不會直接寫在文中，但必須能由兩句以上的資訊推出；過度延伸或太絕對的選項通常錯。",
        ReadingQuestionType.Application => "把規則套到題目的人物或情境，注意例外條件、日期與金額，必要時一步步計算。",
        ReadingQuestionType.Purpose => "問作者「為什麼提到」某內容：看它前後支持哪個論點，而不是它本身的內容。",
        ReadingQuestionType.Relationship => "判斷段落之間的關係：舉例、反駁、修正前面的觀點、提出原因或結果。",
        ReadingQuestionType.Paraphrase => "找保留原句主要資訊與邏輯關係（因果、對比、讓步）的選項；改變關係或漏掉重點的都錯。",
        ReadingQuestionType.MainIdea => "主旨要涵蓋全文，不能只講某一段的細節。",
        _ => "注意作者用的評價字與保留語氣（may, appears, however），選擇程度相符的選項。",
    };

    /// <summary>The question's type, or null if its <c>questionType</c> is missing or unknown.</summary>
    public static ReadingQuestionType? ReadingType(this TOEFLQuestion question) => FromRawValue(question.QuestionType);
}
