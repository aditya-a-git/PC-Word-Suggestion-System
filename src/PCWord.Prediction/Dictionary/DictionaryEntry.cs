namespace PCWord.Prediction.Dictionary;

[Flags]
public enum DictionaryDialect
{
    None = 0,
    American = 1,
    BritishIse = 2,
    BritishIze = 4,
    All = American | BritishIse | BritishIze
}

public sealed record DictionaryEntry(
    string Word,
    DictionaryDialect Dialects = DictionaryDialect.All,
    string VariantGroup = "",
    int? CommonnessSize = null,
    string PartOfSpeech = "",
    int? VariantLevel = null,
    string Lemmas = "",
    long? LegacyWeight = null)
{
    // Keeps small hand-authored fixtures concise while making clear that the value
    // is SCOWL commonness size, not an observed corpus frequency.
    public DictionaryEntry(string word, int commonnessSize)
        : this(word, DictionaryDialect.All, CommonnessSize: commonnessSize)
    {
    }
}
