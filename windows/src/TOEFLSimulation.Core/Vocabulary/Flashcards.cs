using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLFlashcardProgressStore</c>: mastered word IDs, saved sorted under <see cref="StorageKeys.FlashcardsMastered"/>.</summary>
public sealed class FlashcardProgressStore : ObservableObject
{
    private readonly KeyValueStore store;
    private readonly HashSet<string> mastered;

    public FlashcardProgressStore(KeyValueStore store)
    {
        this.store = store;
        mastered = (store.GetStringArray(StorageKeys.FlashcardsMastered) ?? []).ToHashSet();
    }

    public IReadOnlySet<string> Mastered => mastered;

    public void SetMastered(string wordID, bool isMastered)
    {
        if (isMastered) mastered.Add(wordID);
        else mastered.Remove(wordID);
        store.SetStringArray(StorageKeys.FlashcardsMastered, mastered.Order(StringComparer.Ordinal));
        OnPropertyChanged(nameof(Mastered));
    }

    public void Reset()
    {
        mastered.Clear();
        store.Remove(StorageKeys.FlashcardsMastered);
        OnPropertyChanged(nameof(Mastered));
    }
}

/// <summary>Swift <c>TOEFLFlashcardDeckSize</c> (raw value = card count; 0 = all).</summary>
public enum FlashcardDeckSize { Twenty = 20, Fifty = 50, OneHundred = 100, All = 0 }

/// <summary>Swift <c>TOEFLFlashcardMode</c>; raw values "deck" and "infinite" are persisted.</summary>
public enum FlashcardMode { Deck, Infinite }

/// <summary>What the flash-card area shows.</summary>
public enum FlashcardScreen
{
    /// <summary>Before the first deck (nothing shown).</summary>
    NotStarted,
    /// <summary>A card (front or back per <see cref="FlashcardSession.IsRevealed"/>), progress, and rating buttons.</summary>
    Card,
    /// <summary>"Deck complete" summary.</summary>
    DeckComplete,
    /// <summary>"Every card mastered" (infinite loop finished).</summary>
    InfiniteComplete,
    /// <summary>"No matching cards".</summary>
    Empty,
}

/// <summary>Keyboard shortcuts of the flash-card view.</summary>
public enum FlashcardKey { Space, V, LeftArrow, RightArrow }

/// <summary>
/// The flash-card study state (TOEFLFlashcardStudyView): deck building, reveal, Know it / Study again, the infinite loop's
/// requeue rule, and mastery progress. Call <see cref="Appear"/> when shown and <see cref="Disappear"/> when hidden.
/// </summary>
public sealed class FlashcardSession : ObservableObject
{
    public const string ModeKey = StorageKeys.FlashcardsMode;

    private readonly IReadOnlyList<VocabularyEntry> words;
    private readonly KeyValueStore store;
    private readonly Random random;
    private readonly Audio.WordPronunciationPlayer? pronunciation;
    private string selectedSource = TOEFLStudyBank.AllSources;
    private FlashcardDeckSize deckSize = FlashcardDeckSize.Twenty;
    private bool unmasteredOnly = true;
    private FlashcardMode mode;
    private List<VocabularyEntry> deck = [];
    private readonly HashSet<string> masteredThisRound = [];
    private readonly HashSet<string> missedThisRound = [];

    public FlashcardSession(IReadOnlyList<VocabularyEntry> words, KeyValueStore store, Audio.WordPronunciationPlayer? pronunciation = null, Random? random = null)
    {
        this.words = words;
        this.store = store;
        this.pronunciation = pronunciation;
        this.random = random ?? Random.Shared;
        Progress = new FlashcardProgressStore(store);
        mode = store.GetString(ModeKey) == "infinite" ? FlashcardMode.Infinite : FlashcardMode.Deck;
    }

    public FlashcardProgressStore Progress { get; }

    public string SelectedSource
    {
        get => selectedSource;
        set { if (SetProperty(ref selectedSource, value)) NotifyAll(); }
    }

    public FlashcardDeckSize DeckSize
    {
        get => deckSize;
        set => SetProperty(ref deckSize, value);
    }

    public bool UnmasteredOnly
    {
        get => unmasteredOnly;
        set { if (SetProperty(ref unmasteredOnly, value)) NotifyAll(); }
    }

    /// <summary>Persisted (@AppStorage). Changing it starts a new deck.</summary>
    public FlashcardMode Mode
    {
        get => mode;
        set
        {
            if (!SetProperty(ref mode, value)) return;
            store.SetString(ModeKey, value == FlashcardMode.Infinite ? "infinite" : "deck");
            StartNewDeck();
        }
    }

    public IReadOnlyList<VocabularyEntry> Deck => deck;
    public int CurrentIndex { get; private set; }
    public bool IsRevealed { get; private set; }
    public IReadOnlySet<string> MasteredThisRound => masteredThisRound;
    public IReadOnlySet<string> MissedThisRound => missedThisRound;
    public bool HasStarted { get; private set; }
    public int InfiniteTotal { get; private set; }

    public static string DeckSizeTitle(FlashcardDeckSize size) => size == FlashcardDeckSize.All ? "All" : $"{(int)size} cards";
    public static string ModeTitle(FlashcardMode mode) => mode == FlashcardMode.Deck ? "Fixed deck" : "Infinite loop";

    public IReadOnlyList<VocabularyEntry> EligibleWords => words.Where(item =>
    {
        var matchesSource = SelectedSource == TOEFLStudyBank.AllSources || item.Sources.Contains(SelectedSource);
        var skipsMastered = UnmasteredOnly || Mode == FlashcardMode.Infinite;
        var matchesProgress = !skipsMastered || !Progress.Mastered.Contains(item.Id);
        return matchesSource && matchesProgress;
    }).ToList();

    public VocabularyEntry? CurrentCard => CurrentIndex < deck.Count ? deck[CurrentIndex] : null;

    public int OverallMasteredCount => words.Count(w => Progress.Mastered.Contains(w.Id));

    public FlashcardScreen Screen
    {
        get
        {
            if (CurrentCard is not null) return FlashcardScreen.Card;
            if (HasStarted && Mode == FlashcardMode.Infinite && InfiniteTotal > 0) return FlashcardScreen.InfiniteComplete;
            if (HasStarted && deck.Count > 0) return FlashcardScreen.DeckComplete;
            return HasStarted ? FlashcardScreen.Empty : FlashcardScreen.NotStarted;
        }
    }

    // Text shown by the view.
    public string AvailableText => $"{EligibleWords.Count} available";
    public string MasteredText => $"{OverallMasteredCount} mastered";
    public string NewDeckButtonTitle => Mode == FlashcardMode.Infinite ? "Restart loop" : "New shuffled deck";
    public string DeckProgressTitle => $"Card {CurrentIndex + 1} of {deck.Count}";
    public string DeckProgressDetail => $"{masteredThisRound.Count} known · {missedThisRound.Count} review";
    /// <summary>Deck progress bar value (CurrentIndex of max(deck, 1)).</summary>
    public double DeckProgressFraction => (double)CurrentIndex / Math.Max(deck.Count, 1);
    public string InfiniteProgressDetail => $"{masteredThisRound.Count} of {InfiniteTotal} mastered · {deck.Count} left";
    public double InfiniteProgressFraction => (double)masteredThisRound.Count / Math.Max(InfiniteTotal, 1);
    public const string InfiniteNote = "Cards marked Study again return after about 15–40 other cards, so you can check real recall.";
    public string CompletionText => $"You knew {masteredThisRound.Count} cards and marked {missedThisRound.Count} for another review.";
    public string InfiniteCompletionText => $"You worked through all {InfiniteTotal} cards in this loop until each one was marked Know it.";
    public string EmptyText => UnmasteredOnly || Mode == FlashcardMode.Infinite
        ? "Every matching card is mastered. Switch to a fixed deck with Unmastered only off, or reset mastery progress."
        : "Choose a different source to build a deck.";

    private void NotifyAll() => OnPropertyChanged(string.Empty);

    /// <summary>onAppear: builds the first deck.</summary>
    public void Appear()
    {
        if (!HasStarted) StartNewDeck();
    }

    /// <summary>onDisappear: stops pronunciation.</summary>
    public void Disappear() => pronunciation?.Stop();

    public void Reveal()
    {
        IsRevealed = true;
        NotifyAll();
    }

    /// <summary>"Know it" (true) or "Study again" (false).</summary>
    public void Rate(bool asMastered)
    {
        if (CurrentCard is not { } card) return;
        pronunciation?.Stop();
        Progress.SetMastered(card.Id, asMastered);
        if (asMastered)
        {
            masteredThisRound.Add(card.Id);
            missedThisRound.Remove(card.Id);
        }
        else
        {
            missedThisRound.Add(card.Id);
            masteredThisRound.Remove(card.Id);
        }
        if (Mode == FlashcardMode.Infinite) AdvanceInfiniteLoop(asMastered ? null : card);
        else CurrentIndex++;
        IsRevealed = false;
        NotifyAll();
    }

    /// <summary>Removes the current card; a missed card returns after enough other cards to test real recall.</summary>
    private void AdvanceInfiniteLoop(VocabularyEntry? missed)
    {
        var queue = deck.ToList();
        queue.RemoveAt(CurrentIndex);
        if (missed is not null) queue.Insert(RequeueGap(queue.Count), missed);
        deck = queue;
        CurrentIndex = 0;
    }

    /// <summary>About a quarter of the remaining queue, kept between 15 and 40 cards with ±20% jitter; a short queue sends the
    /// card to the end so every other card is seen first.</summary>
    public int RequeueGap(int remaining)
    {
        var target = Math.Min(Math.Max(remaining / 4, 15), 40);
        var jittered = random.Next(target * 4 / 5, target * 6 / 5 + 1);
        return Math.Min(remaining, jittered);
    }

    /// <summary>"New shuffled deck" / "Restart loop" / "Start another deck", and after a mode change or reset.</summary>
    public void StartNewDeck()
    {
        pronunciation?.Stop();
        var shuffled = EligibleWords.ToList();
        Shuffle(shuffled);
        var limit = DeckSize == FlashcardDeckSize.All || Mode == FlashcardMode.Infinite ? shuffled.Count : Math.Min((int)DeckSize, shuffled.Count);
        deck = shuffled.Take(limit).ToList();
        InfiniteTotal = Mode == FlashcardMode.Infinite ? deck.Count : 0;
        CurrentIndex = 0;
        IsRevealed = false;
        masteredThisRound.Clear();
        missedThisRound.Clear();
        HasStarted = true;
        NotifyAll();
    }

    /// <summary>"Review missed cards".</summary>
    public void ReviewMissedCards()
    {
        pronunciation?.Stop();
        deck = words.Where(w => missedThisRound.Contains(w.Id)).ToList();
        Shuffle(deck);
        InfiniteTotal = 0;
        CurrentIndex = 0;
        IsRevealed = false;
        masteredThisRound.Clear();
        missedThisRound.Clear();
        HasStarted = true;
        NotifyAll();
    }

    /// <summary>"Reset mastery progress" (after confirming "Reset all flash-card progress?").</summary>
    public void ResetProgress()
    {
        Progress.Reset();
        StartNewDeck();
    }

    /// <summary>"Start a fixed deck" on the infinite-loop completion screen.</summary>
    public void StartFixedDeck() => Mode = FlashcardMode.Deck;

    /// <summary>Keyboard: Space reveals, V toggles pronunciation, ←/→ rate a revealed card. Returns true if handled.</summary>
    public bool HandleKey(FlashcardKey key)
    {
        switch (key)
        {
            case FlashcardKey.Space when CurrentCard is not null && !IsRevealed:
                Reveal();
                return true;
            case FlashcardKey.V when CurrentCard is { } card:
                _ = pronunciation?.ToggleAsync(card.Word);
                return true;
            case FlashcardKey.LeftArrow when CurrentCard is not null && IsRevealed:
                Rate(false);
                return true;
            case FlashcardKey.RightArrow when CurrentCard is not null && IsRevealed:
                Rate(true);
                return true;
            default:
                return false;
        }
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
