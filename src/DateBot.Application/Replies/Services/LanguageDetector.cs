using System.Collections.Frozen;
using CSharpFunctionalExtensions;
using DateBot.Application.Replies.Enums;

namespace DateBot.Application.Replies.Services;

/// <summary>Cheap heuristic for Russian / Uzbek (Latin or Cyrillic) / English.</summary>
public static class LanguageDetector
{
    private const int MinLetters = 3;

    private static readonly FrozenSet<char> UzbekCyrillicLetters = FrozenSet.ToFrozenSet(['ў', 'қ', 'ғ', 'ҳ', 'Ў', 'Қ', 'Ғ', 'Ҳ']);

    private static readonly string[] UzbekLatinMarkers = ["o‘", "g‘", "oʻ", "gʻ", "o'", "g'", "o`", "g`"];

    private static readonly FrozenSet<string> UzbekWords = FrozenSet.ToFrozenSet(
        ["va", "men", "sen", "salom", "bilan", "uchun", "yaxshi", "yoqadi", "sevaman", "emas", "ham", "juda",
         "nima", "qanday", "rahmat", "kitob", "musiqa", "sayohat", "yoshman", "qilaman", "o'qiyman", "ishlayman",
         "bo'lsa", "menga", "sizga", "qiz", "yigit", "do'st", "hayot", "bor", "yo'q"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> EnglishWords = FrozenSet.ToFrozenSet(
        ["the", "and", "i", "you", "love", "like", "my", "is", "to", "of", "in", "me", "a", "am", "with", "for",
         "looking", "music", "travel", "books", "hi", "hello", "your", "are", "it", "that"],
        StringComparer.OrdinalIgnoreCase);

    public static Maybe<ReplyLanguage> Detect(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length < MinLetters)
        {
            return Maybe<ReplyLanguage>.None;
        }

        var cyrillic = letters.Count(IsCyrillic);
        if (cyrillic * 2 > letters.Length)
        {
            return letters.Any(UzbekCyrillicLetters.Contains) ? ReplyLanguage.Uzbek : ReplyLanguage.Russian;
        }

        var words = text
            .Split([' ', '\n', '\t', ',', '.', '!', '?', ';', ':', '(', ')', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .ToArray();
        var uzbekScore = words.Count(UzbekWords.Contains)
            + UzbekLatinMarkers.Count(m => text.Contains(m, StringComparison.OrdinalIgnoreCase)) * 2;
        var englishScore = words.Count(EnglishWords.Contains);

        return uzbekScore == 0 && englishScore == 0 ? Maybe<ReplyLanguage>.None
            : uzbekScore > englishScore ? ReplyLanguage.Uzbek
            : ReplyLanguage.English;
    }

    private static bool IsCyrillic(char c) => c is >= 'Ѐ' and <= 'ӿ';
}
