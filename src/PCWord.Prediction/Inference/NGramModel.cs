using System.Text;
using System.Text.Json;

namespace PCWord.Prediction.Inference;

/// <summary>Deterministic bounded word n-gram model. Counts and requests stay local.</summary>
public sealed class NGramModel
{
    public const int MaximumVocabulary = 50_000;
    public const int MaximumContextEntries = 250_000;
    private const int MaximumCandidatesPerContext = 32;
    private readonly Dictionary<string, long> _unigrams;
    private readonly Dictionary<string, ContextCounts> _bigrams;
    private readonly Dictionary<string, ContextCounts> _trigrams;
    private readonly KeyValuePair<string, long>[] _rankedUnigrams;
    private readonly double _unigramTotal;
    public double RecommendedBackoff { get; private set; } = 0.8;
    /// <summary>Weight given to the highest available n-gram order during interpolation.</summary>
    public double RecommendedInterpolationWeight => RecommendedBackoff;
    public int RecommendedOrder { get; private set; } = 3;

    private NGramModel(Dictionary<string, long> unigrams,
        Dictionary<string, Dictionary<string, long>> bigrams,
        Dictionary<string, Dictionary<string, long>> trigrams)
    {
        _unigrams = unigrams;
        _rankedUnigrams = unigrams.OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        _unigramTotal = unigrams.Values.Sum(value => (double)value);
        var wordPool = unigrams.Keys.ToDictionary(word => word, word => word, StringComparer.Ordinal);
        _bigrams = CompactContexts(bigrams, wordPool);
        _trigrams = CompactContexts(trigrams, wordPool);
    }

    public int VocabularySize => _unigrams.Count;
    public int ContextCount => _bigrams.Count + _trigrams.Count;
    public bool ContainsWord(string word) => _unigrams.ContainsKey(word);

    public static NGramModel Train(IEnumerable<string> lines, int maximumVocabulary = MaximumVocabulary,
        int maximumContextEntries = MaximumContextEntries)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (maximumVocabulary < 1 || maximumContextEntries < 1) throw new ArgumentOutOfRangeException();
        var uni = new Dictionary<string, long>(StringComparer.Ordinal);
        var bi = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        var tri = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            foreach (string[] words in WordTokenizer.TokenizeSegments(line))
            for (int i = 0; i < words.Length; i++)
            {
                Increment(uni, words[i]);
                if (i > 0) IncrementNested(bi, words[i - 1], words[i]);
                if (i > 1) IncrementNested(tri, Pair(words[i - 2], words[i - 1]), words[i]);
            }
        }
        var vocab = uni.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(maximumVocabulary).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return new NGramModel(vocab, Bound(bi, vocab, maximumContextEntries), Bound(tri, vocab, maximumContextEntries));
    }

    public IReadOnlyList<string> Predict(string precedingText, int maximum = 3, double? backoff = null, int? maximumOrder = null)
    {
        ArgumentNullException.ThrowIfNull(precedingText);
        double backoffValue = backoff ?? RecommendedBackoff;
        int orderValue = maximumOrder ?? RecommendedOrder;
        // The editor requests three candidates; the offline evaluator requests five
        // so top-5 recall can be measured through the exact same ranking path.
        if (maximum is < 1 or > 5 || backoffValue is <= 0 or > 1 || orderValue is < 1 or > 3) throw new ArgumentOutOfRangeException();
        string[] history = WordTokenizer.TokenizeSegments(precedingText).LastOrDefault() ?? Array.Empty<string>();
        history = history.TakeLast(2).ToArray();
        if (_unigrams.Count == 0) return Array.Empty<string>();
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        ContextCounts? trigram = orderValue >= 3 && history.Length == 2 &&
            _trigrams.TryGetValue(Pair(history[0], history[1]), out var tri) ? tri : null;
        ContextCounts? bigram = orderValue >= 2 && history.Length > 0 &&
            _bigrams.TryGetValue(history[^1], out var bi) ? bi : null;
        AddTop(candidates, trigram, MaximumCandidatesPerContext);
        AddTop(candidates, bigram, MaximumCandidatesPerContext);
        AddTop(candidates, _rankedUnigrams, MaximumCandidatesPerContext * 2);
        var scored = candidates.Select(word =>
        {
            double unigramProbability = _unigrams.TryGetValue(word, out long unigramCount)
                ? unigramCount / _unigramTotal
                : 0;
            double bigramProbability = bigram is not null && bigram.Value.TryGetValue(word, out long bigramCount)
                ? bigramCount / bigram.Value.Total
                : 0;
            double trigramProbability = trigram is not null && trigram.Value.TryGetValue(word, out long trigramCount)
                ? trigramCount / trigram.Value.Total
                : 0;

            // Interpolate adjacent orders so sparse contexts retain a useful lower-order
            // signal instead of dropping to it only when a candidate is unseen.
            double score = orderValue switch
            {
                3 when trigram is not null => backoffValue * trigramProbability +
                    (1 - backoffValue) * (bigram is not null ? bigramProbability : unigramProbability),
                >= 2 when bigram is not null => backoffValue * bigramProbability +
                    (1 - backoffValue) * unigramProbability,
                _ => unigramProbability
            };
            long count = _unigrams.GetValueOrDefault(word);
            return (word, score, count);
        }).OrderByDescending(x => x.score).ThenByDescending(x => x.count).ThenBy(x => x.word, StringComparer.Ordinal)
          .Take(maximum).Select(x => x.word).ToArray();
        return scored;
    }

    public IReadOnlyList<string> PredictUnigram(int maximum = 3) =>
        _rankedUnigrams.Take(Math.Max(0, maximum)).Select(pair => pair.Key).ToArray();

    public void ConfigurePrediction(double backoff, int maximumOrder)
    {
        if (backoff is <= 0 or > 1 || maximumOrder is < 1 or > 3) throw new ArgumentOutOfRangeException();
        RecommendedBackoff = backoff; RecommendedOrder = maximumOrder;
    }
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(new ModelData(
        _unigrams, ExpandContexts(_bigrams), ExpandContexts(_trigrams), RecommendedBackoff, RecommendedOrder)));
    public static NGramModel Load(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 128L * 1024 * 1024) throw new InvalidDataException("Model file is missing or exceeds 128 MiB.");
        var data = JsonSerializer.Deserialize<ModelData>(File.ReadAllText(path)) ?? throw new InvalidDataException("Model is empty.");
        if (data.Unigrams is null || data.Bigrams is null || data.Trigrams is null)
            throw new InvalidDataException("Model is missing required count tables.");
        if (data.Unigrams.Count > MaximumVocabulary || data.Bigrams.Count + data.Trigrams.Count > MaximumContextEntries * 2)
            throw new InvalidDataException("Model exceeds configured bounds.");
        if (data.Unigrams.Any(pair => string.IsNullOrEmpty(pair.Key) || pair.Value <= 0) ||
            data.Bigrams.Values.Concat(data.Trigrams.Values).Any(counts => counts is null || counts.Count > MaximumCandidatesPerContext ||
                counts.Any(pair => string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)) ||
            data.RecommendedBackoff is <= 0 or > 1 || data.RecommendedOrder is < 1 or > 3)
            throw new InvalidDataException("Model configuration or context exceeds configured bounds.");
        var model = new NGramModel(data.Unigrams, data.Bigrams, data.Trigrams);
        model.ConfigurePrediction(data.RecommendedBackoff, data.RecommendedOrder);
        return model;
    }

    private static Dictionary<string, Dictionary<string, long>> Bound(Dictionary<string, Dictionary<string, long>> source,
        Dictionary<string, long> vocab, int maximum) => source
        .Where(p => p.Value.Keys.Any(vocab.ContainsKey))
        .OrderByDescending(p => p.Value.Values.Sum(v => (double)v)).ThenBy(p => p.Key, StringComparer.Ordinal)
        .Take(maximum).ToDictionary(p => p.Key, p => p.Value.Where(q => vocab.ContainsKey(q.Key))
            .OrderByDescending(q => q.Value).ThenBy(q => q.Key, StringComparer.Ordinal).Take(MaximumCandidatesPerContext)
            .ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal), StringComparer.Ordinal);
    private static Dictionary<string, ContextCounts> CompactContexts(
        Dictionary<string, Dictionary<string, long>> source,
        Dictionary<string, string> wordPool) => source.ToDictionary(
            pair => pair.Key,
            pair => new ContextCounts(pair.Value, wordPool),
            StringComparer.Ordinal);
    private static Dictionary<string, Dictionary<string, long>> ExpandContexts(
        Dictionary<string, ContextCounts> source) => source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(),
            StringComparer.Ordinal);
    private static void AddTop(HashSet<string> result, ContextCounts? values, int count)
    { if (values is null) return; values.Value.AddTop(result, count); }
    private static void AddTop(HashSet<string> result, KeyValuePair<string, long>[] values, int count)
    { foreach (var item in values.Take(count)) result.Add(item.Key); }
    private static string Pair(string a, string b) => a + "\u001f" + b;
    private static void Increment(Dictionary<string, long> values, string key) => values[key] = values.GetValueOrDefault(key) + 1;
    private static void IncrementNested(Dictionary<string, Dictionary<string, long>> values, string key, string word)
    { if (!values.TryGetValue(key, out var counts)) values[key] = counts = new(StringComparer.Ordinal); Increment(counts, word); }
    private sealed record ModelData(Dictionary<string, long> Unigrams,
        Dictionary<string, Dictionary<string, long>> Bigrams, Dictionary<string, Dictionary<string, long>> Trigrams,
        double RecommendedBackoff = 0.8, int RecommendedOrder = 3);
    private readonly struct ContextCounts
    {
        private readonly KeyValuePair<string, long>[] _ranked;
        public double Total { get; }

        public ContextCounts(Dictionary<string, long> counts, Dictionary<string, string> wordPool)
        {
            _ranked = counts.Select(pair =>
                new KeyValuePair<string, long>(wordPool.GetValueOrDefault(pair.Key, pair.Key), pair.Value))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            Total = _ranked.Sum(pair => (double)pair.Value);
        }

        public bool TryGetValue(string word, out long count)
        {
            foreach (KeyValuePair<string, long> pair in _ranked)
            {
                if (StringComparer.Ordinal.Equals(pair.Key, word))
                {
                    count = pair.Value;
                    return true;
                }
            }
            count = 0;
            return false;
        }

        public void AddTop(HashSet<string> result, int count)
        {
            foreach (KeyValuePair<string, long> pair in _ranked.Take(count))
                result.Add(pair.Key);
        }

        public Dictionary<string, long> ToDictionary()
        {
            var result = new Dictionary<string, long>(_ranked.Length, StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> pair in _ranked) result.Add(pair.Key, pair.Value);
            return result;
        }
    }
}

/// <summary>Invariant lowercase Unicode letter/mark/digit tokens; punctuation separates tokens.</summary>
public static class WordTokenizer
{
    public static IEnumerable<string[]> TokenizeSegments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var segment = new StringBuilder();
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.Value is '.' or '!' or '?' or '\n' or '\r')
            {
                string[] tokens = Tokenize(segment.ToString()).ToArray();
                if (tokens.Length > 0) yield return tokens;
                segment.Clear();
            }
            else segment.Append(rune.ToString());
        }
        string[] final = Tokenize(segment.ToString()).ToArray();
        if (final.Length > 0) yield return final;
    }

    public static IEnumerable<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var token = new StringBuilder();
        foreach (Rune rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (Rune.IsLetterOrDigit(rune) || category is System.Globalization.UnicodeCategory.NonSpacingMark or
                System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark)
                token.Append(rune.ToString().ToLowerInvariant());
            else if (token.Length > 0) { yield return token.ToString(); token.Clear(); }
        }
        if (token.Length > 0) yield return token.ToString();
    }
}
