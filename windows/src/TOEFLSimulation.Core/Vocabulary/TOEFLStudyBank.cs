using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TOEFLSimulation.Core.Audio;
using TOEFLSimulation.Core.Json;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>VocabularyEntry</c>: one vocabulary card (AuthorizedTOEFLVocabulary.json plus curated words).</summary>
public sealed record VocabularyEntry
{
    [JsonRequired] public string Word { get; init; } = "";
    [JsonRequired] public string Pronunciation { get; init; } = "";
    [JsonRequired] public string PartOfSpeech { get; init; } = "";
    [JsonRequired] public string Definition { get; init; } = "";
    /// <summary>Taiwan Traditional Chinese meaning.</summary>
    [JsonRequired] public string Chinese { get; init; } = "";
    [JsonRequired] public IReadOnlyList<string> Synonyms { get; init; } = [];
    [JsonRequired] public string Example { get; init; } = "";
    [JsonRequired] public IReadOnlyList<string> Sources { get; init; } = [];
    [JsonRequired] public bool IsHighFrequency { get; init; }

    /// <summary>The lower-cased word; the key for mastery progress.</summary>
    [JsonIgnore] public string Id => Word.ToLowerInvariant();
}

/// <summary>
/// Swift <c>TOEFLStudyBank</c>: the authorized vocabulary merged with the curated high-frequency words (curated text wins,
/// imported pronunciation and synonyms are kept, sources are merged), sorted case-insensitively.
/// </summary>
public sealed class TOEFLStudyBank
{
    public const string ResourceFileName = "AuthorizedTOEFLVocabulary.json";
    /// <summary>The source filter's "all" option.</summary>
    public const string AllSources = "All sources";

    public TOEFLStudyBank(IReadOnlyList<VocabularyEntry> bundledWords)
    {
        var merged = new Dictionary<string, VocabularyEntry>();
        foreach (var entry in bundledWords)
            if (!merged.TryAdd(entry.Id, entry)) throw new InvalidDataException($"Duplicate vocabulary entry: {entry.Id}");
        foreach (var curated in CuratedWords)
        {
            if (merged.TryGetValue(curated.Id, out var imported))
            {
                merged[curated.Id] = new VocabularyEntry
                {
                    Word = curated.Word,
                    Pronunciation = imported.Pronunciation,
                    PartOfSpeech = curated.PartOfSpeech,
                    Definition = curated.Definition,
                    Chinese = curated.Chinese,
                    Synonyms = imported.Synonyms,
                    Example = curated.Example,
                    Sources = imported.Sources.Concat(curated.Sources).Distinct().Order(StringComparer.Ordinal).ToList(),
                    IsHighFrequency = true,
                };
            }
            else
            {
                merged[curated.Id] = curated;
            }
        }
        var compare = CultureInfo.GetCultureInfo("en-US").CompareInfo;
        Words = merged.Values.OrderBy(w => w.Word, Comparer<string>.Create((a, b) => compare.Compare(a, b, CompareOptions.IgnoreCase))).ToList();
        SourceOptions = Words.SelectMany(w => w.Sources).Distinct().Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Loads <c>AuthorizedTOEFLVocabulary.json</c> from the Resources directory. Throws if missing or invalid.</summary>
    public static TOEFLStudyBank Load(string resourcesDirectory)
    {
        var path = Path.Combine(resourcesDirectory, ResourceFileName);
        try
        {
            return new TOEFLStudyBank(SwiftJson.Deserialize<List<VocabularyEntry>>(File.ReadAllBytes(path)) ?? []);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Could not decode {ResourceFileName}: {error.Message}", error);
        }
    }

    /// <summary>All entries, sorted by word (case-insensitive).</summary>
    public IReadOnlyList<VocabularyEntry> Words { get; }

    /// <summary>Every source tag, sorted (the source picker lists "All sources" first, then these).</summary>
    public IReadOnlyList<string> SourceOptions { get; }

    /// <summary>The Vocabulary page's Browse filter: source tag plus a query on word, definition, Chinese, or synonyms.</summary>
    public IReadOnlyList<VocabularyEntry> Filter(string searchText, string selectedSource = AllSources)
    {
        var query = searchText.Trim().ToLowerInvariant();
        return Words.Where(entry =>
        {
            var matchesSource = selectedSource == AllSources || entry.Sources.Contains(selectedSource);
            var matchesQuery = query.Length == 0
                || entry.Word.ToLowerInvariant().Contains(query, StringComparison.Ordinal)
                || entry.Definition.ToLowerInvariant().Contains(query, StringComparison.Ordinal)
                || entry.Chinese.Contains(query, StringComparison.Ordinal)
                || entry.Synonyms.Any(s => s.ToLowerInvariant().Contains(query, StringComparison.Ordinal));
            return matchesSource && matchesQuery;
        }).ToList();
    }

    /// <summary>Swift <c>validate()</c>: count, unique IDs, complete cards, reviewed definitions, and bundled pronunciations.</summary>
    public IReadOnlyList<string> Validate(AudioClipPack pronunciations)
    {
        var issues = new List<string>();
        if (Words.Count < 1_300) issues.Add("Vocabulary bank unexpectedly contains fewer than 1,300 entries.");
        if (Words.Select(w => w.Id).Distinct().Count() != Words.Count) issues.Add("Vocabulary bank has duplicate IDs.");
        if (Words.Any(w => w.Word.Length == 0 || w.Chinese.Length == 0 || w.Example.Length == 0))
            issues.Add("Vocabulary bank contains an incomplete card.");
        if (Words.Any(w => w.Definition.Length == 0 || w.Chinese.Contains('/') || w.Chinese.Contains(';')))
            issues.Add("Vocabulary bank contains an unreviewed definition or sense separator.");
        var missingAudio = Words.Where(w => !pronunciations.HasClip(w.Word)).ToList();
        if (missingAudio.Count > 0)
            issues.Add($"{missingAudio.Count} words have no bundled pronunciation, e.g. [{string.Join(", ", missingAudio.Take(3).Select(w => $"\"{w.Word}\""))}].");
        return issues;
    }

    private static VocabularyEntry Curated(string word, string partOfSpeech, string definition, string chinese, string example, string source) => new()
    {
        Word = word,
        Pronunciation = "",
        PartOfSpeech = partOfSpeech,
        Definition = definition,
        Chinese = chinese,
        Synonyms = [],
        Example = example,
        Sources = [source],
        IsHighFrequency = true,
    };

    /// <summary>The curated high-frequency words defined in the Mac app's VocabularyBank.swift.</summary>
    public static readonly IReadOnlyList<VocabularyEntry> CuratedWords =
    [
        Curated("adapt", "v.", "to change in response to conditions; to adjust; to modify a book, play, etc. for another form", "適應；調整；改編", "Coastal plants adapt to frequent exposure to salt.", "Essential Words for the TOEFL"),
        Curated("allocate", "v.", "to distribute or assign for a particular purpose", "分配；撥給；配置", "The committee allocated additional funds to laboratory safety.", "Official Guide vocabulary review"),
        Curated("ambiguous", "adj.", "open to more than one interpretation; unclear or uncertain", "模稜兩可的；語意不明的；含糊的；不明確的", "The ambiguous inscription supports several historical interpretations.", "Essential Words for the TOEFL"),
        Curated("approximate", "adj./v.", "close to the actual value but not exact; to estimate; to come close to in quality or amount", "adj. 大約的；近似的；v. 估算；接近；近似於", "Researchers used tree rings to approximate earlier rainfall levels.", "Official Guide vocabulary review"),
        Curated("coherent", "adj.", "logical, consistent, and easy to follow; forming a unified whole", "連貫的；條理清楚的；一致的", "A coherent response connects evidence to a clear claim.", "Writing rubric language"),
        Curated("compensate", "v.", "to make up for a weakness or loss; to pay someone for loss, injury, or work", "彌補；補償；賠償；抵銷", "Magnetic cues compensate when clouds hide visual landmarks.", "Official TOEFL reading themes"),
        Curated("consecutive", "adj.", "following one after another without interruption", "連續的；連貫的", "The team collected measurements on five consecutive days.", "Essential Words for the TOEFL"),
        Curated("constitute", "v.", "to form or make up a whole; to amount to; to establish", "構成；組成；被視為；設立", "Small businesses constitute most of the town's employers.", "Essential Words for the TOEFL"),
        Curated("corroborate", "v.", "to confirm or give support to a statement or theory with additional evidence", "證實；佐證；支持（說法）", "A second field survey corroborated the original observation.", "Academic reading vocabulary"),
        Curated("decline", "n./v.", "a gradual decrease or loss of strength; to become smaller or weaker; to politely refuse", "n. 下降；衰退；減少；v. 下降；衰退；婉拒", "The population began to decline after its habitat was fragmented.", "Official TOEFL reading themes"),
        Curated("derive", "v.", "to obtain or develop from a source; to originate from; to work out by reasoning", "源自；取得；獲得；推導出", "Many medicines are derived from compounds found in plants.", "Essential Words for the TOEFL"),
        Curated("distinct", "adj.", "clearly different or separate; easily noticed or clearly perceived", "明顯不同的；有區別的；清楚的；明確的", "The two dialects developed in distinct geographic regions.", "Official Guide vocabulary review"),
        Curated("elaborate", "adj./v.", "detailed and carefully planned; complicated; to explain or add more details", "adj. 精心設計的；精緻的；複雜的；v. 詳細說明；闡述", "The interviewer asked the student to elaborate on her example.", "Speaking rubric language"),
        Curated("empirical", "adj.", "based on observation, experiment, or experience rather than theory", "實證的；經驗主義的；以實驗為根據的", "The claim requires empirical evidence from controlled studies.", "Academic reading vocabulary"),
        Curated("facilitate", "v.", "to make an action or process easier or more likely", "促進；使便利；協助", "Shared notes can facilitate collaboration among group members.", "Essential Words for the TOEFL"),
        Curated("fluctuate", "v.", "to rise and fall irregularly in number or amount", "波動；起伏；變動", "Daily temperatures fluctuate more in dry environments.", "Essential Words for the TOEFL"),
        Curated("infer", "v.", "to reach a conclusion from evidence and reasoning; to imply", "推論；推斷；推測", "From the worn steps, historians infer that the entrance was heavily used.", "Official Guide vocabulary review"),
        Curated("inhibit", "v.", "to slow down or prevent a process; to make someone self-conscious and restrained", "抑制；阻礙；使拘束", "Low temperatures inhibit the growth of many microorganisms.", "Essential Words for the TOEFL"),
        Curated("integral", "adj.", "necessary to make a whole complete; essential; (mathematics) relating to integrals", "不可或缺的；構成整體的；完整的；（數學）積分的", "Seasonal flooding is integral to the wetland ecosystem.", "Academic reading vocabulary"),
        Curated("maintain", "v.", "to keep in an existing state; to keep in good condition; to state firmly that something is true; to support financially", "維持；保養；堅持主張；扶養", "The greenhouse maintains a stable temperature overnight.", "Essential Words for the TOEFL"),
        Curated("mechanism", "n.", "a process or system that produces a result; a set of moving parts in a machine", "機制；機械裝置；運作方式", "The model isolates the mechanism that drives ocean circulation.", "Official TOEFL reading themes"),
        Curated("mitigate", "v.", "to make something harmful or bad less severe", "減輕；緩和", "Urban trees can mitigate heat by shading pavement.", "Academic reading vocabulary"),
        Curated("negligible", "adj.", "too small or unimportant to be worth considering", "微不足道的；可忽略的", "The second treatment produced only a negligible change.", "Essential Words for the TOEFL"),
        Curated("persistent", "adj.", "continuing for a long time; continuing firmly despite opposition or difficulty", "持續的；堅持不懈的；頑固的", "Persistent drought altered the region's vegetation.", "Academic reading vocabulary"),
        Curated("plausible", "adj.", "seemingly reasonable or probable; believable; (of a person) skilled at producing persuasive but possibly deceptive arguments", "看似合理的；可信的；（人）花言巧語的", "The new evidence makes the alternative explanation plausible.", "Official Guide vocabulary review"),
        Curated("preliminary", "adj./n.", "coming before and preparing for something more important; a preliminary action or event; a preliminary round in a competition", "adj. 初步的；預備的；開端的；n. 初步行動；預賽", "Preliminary findings will be reviewed before publication.", "Essential Words for the TOEFL"),
        Curated("profound", "adj.", "very great or far-reaching; showing great knowledge or insight; demanding deep study; intensely felt", "深遠的；重大的；深刻的；深奧的；強烈的", "The printing press had a profound effect on access to knowledge.", "Essential Words for the TOEFL"),
        Curated("provisional", "adj.", "arranged for the present, possibly to be changed later; temporary", "暫時的；臨時的；暫定的", "The committee issued a provisional schedule for the field season.", "Academic reading vocabulary"),
        Curated("resilient", "adj.", "able to recover quickly from difficulties or change; adaptable; (of a material) able to spring back into shape", "有復原力的；有韌性的；適應力強的；（物體）有彈性的", "A diverse ecosystem may be more resilient after disturbance.", "Academic reading vocabulary"),
        Curated("retain", "v.", "to continue to have or keep; to keep in place; to remember; to hire a lawyer by paying a fee", "保留；保持；維持；記住；聘請（律師等）", "Dense materials retain heat after sunset.", "Official TOEFL reading themes"),
        Curated("subsequent", "adj.", "coming after something in time; following", "隨後的；後續的；之後的", "Subsequent experiments used a larger sample.", "Essential Words for the TOEFL"),
        Curated("subtle", "adj.", "not obvious; hard to notice; delicate in meaning or effect; clever", "細微的；不易察覺的；微妙的；巧妙的", "The two pigments show subtle differences under ultraviolet light.", "Official Guide vocabulary review"),
        Curated("sustain", "v.", "to keep something going over time; to support physically or mentally; to suffer an injury or loss", "維持；支撐；遭受（損失或傷害）", "The soil cannot sustain intensive farming without added nutrients.", "Essential Words for the TOEFL"),
        Curated("tentative", "adj.", "not certain or fixed; provisional; done without confidence", "暫定的；試探性的；猶豫的", "The researchers offered a tentative explanation for the anomaly.", "Academic reading vocabulary"),
        Curated("underlying", "adj.", "basic and fundamental but not immediately obvious; lying beneath", "根本的；潛在的；在下面的", "The discussion focused on the underlying cause of the conflict.", "Official Guide vocabulary review"),
        Curated("viable", "adj.", "capable of working successfully; feasible; able to survive or grow", "可行的；切實可行的；能存活的", "The pilot study showed that the method was viable.", "Essential Words for the TOEFL"),
    ];
}
