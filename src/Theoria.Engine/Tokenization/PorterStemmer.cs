namespace Theoria.Engine.Tokenization;

/// <summary>
/// Implementation of the Porter stemming algorithm for English.
/// Reduces inflected words to their stem so that "theology", "theological",
/// "theologians" all map to the same root, dramatically improving recall.
///
/// Based on Martin Porter's original 1980 algorithm.
/// Reference: https://tartarus.org/martin/PorterStemmer/
/// </summary>
public static class PorterStemmer
{
    /// <summary>
    /// Returns the stemmed form of the given word.
    /// If the word is 2 characters or fewer, it is returned unchanged.
    /// </summary>
    public static string Stem(string word)
    {
        if (word.Length <= 2)
            return word;

        var stem = word.ToCharArray();
        int len = stem.Length;

        len = Step1a(stem, len);
        len = Step1b(stem, len);
        len = Step1c(stem, len);
        len = Step2(stem, len);
        len = Step3(stem, len);
        len = Step4(stem, len);
        len = Step5a(stem, len);
        len = Step5b(stem, len);

        return new string(stem, 0, len);
    }

    // --- Step 1a: plurals ---
    private static int Step1a(char[] s, int len)
    {
        if (EndsWith(s, len, "sses")) return len - 2;     // caresses → caress
        if (EndsWith(s, len, "ies")) return len - 2;       // ponies → poni
        if (EndsWith(s, len, "ss")) return len;            // caress → caress
        if (len > 1 && s[len - 1] == 's') return len - 1;  // cats → cat
        return len;
    }

    // --- Step 1b: -ed, -ing ---
    private static int Step1b(char[] s, int len)
    {
        if (EndsWith(s, len, "eed"))
        {
            if (MeasureBeforeSuffix(s, len, 3) > 0) return len - 1; // agreed → agree
            return len;
        }

        bool found = false;
        int trimmed = len;

        if (EndsWith(s, len, "ed"))
        {
            if (ContainsVowelBefore(s, len - 2))
            {
                trimmed = len - 2;
                found = true;
            }
        }
        else if (EndsWith(s, len, "ing"))
        {
            if (ContainsVowelBefore(s, len - 3))
            {
                trimmed = len - 3;
                found = true;
            }
        }

        if (found)
        {
            if (EndsWith(s, trimmed, "at") || EndsWith(s, trimmed, "bl") || EndsWith(s, trimmed, "iz"))
            {
                s[trimmed] = 'e';
                return trimmed + 1;
            }
            if (trimmed > 1 && s[trimmed - 1] == s[trimmed - 2] && !IsVowel(s, trimmed - 1))
            {
                char c = s[trimmed - 1];
                if (c != 'l' && c != 's' && c != 'z')
                    return trimmed - 1;
            }
            if (MeasureBeforeSuffix(s, trimmed, 0) == 1 && EndsWithCvc(s, trimmed))
            {
                s[trimmed] = 'e';
                return trimmed + 1;
            }
        }
        return found ? trimmed : len;
    }

    // --- Step 1c: y → i if stem has vowel ---
    private static int Step1c(char[] s, int len)
    {
        if (len > 1 && s[len - 1] == 'y' && ContainsVowelBefore(s, len - 1))
        {
            s[len - 1] = 'i';
        }
        return len;
    }

    // --- Step 2: map double suffixes ---
    private static int Step2(char[] s, int len)
    {
        if (len < 3) return len;

        switch (s[len - 2])
        {
            case 'a':
                if (EndsWith(s, len, "ational")) return ReplaceSuffix(s, len, 7, "ate");
                if (EndsWith(s, len, "tional")) return ReplaceSuffix(s, len, 6, "tion");
                break;
            case 'c':
                if (EndsWith(s, len, "enci")) return ReplaceSuffix(s, len, 4, "ence");
                if (EndsWith(s, len, "anci")) return ReplaceSuffix(s, len, 4, "ance");
                break;
            case 'e':
                if (EndsWith(s, len, "izer")) return ReplaceSuffix(s, len, 4, "ize");
                break;
            case 'l':
                if (EndsWith(s, len, "abli")) return ReplaceSuffix(s, len, 4, "able");
                if (EndsWith(s, len, "alli")) return ReplaceSuffix(s, len, 4, "al");
                if (EndsWith(s, len, "entli")) return ReplaceSuffix(s, len, 5, "ent");
                if (EndsWith(s, len, "eli")) return ReplaceSuffix(s, len, 3, "e");
                if (EndsWith(s, len, "ousli")) return ReplaceSuffix(s, len, 5, "ous");
                break;
            case 'o':
                if (EndsWith(s, len, "ization")) return ReplaceSuffix(s, len, 7, "ize");
                if (EndsWith(s, len, "ation")) return ReplaceSuffix(s, len, 5, "ate");
                if (EndsWith(s, len, "ator")) return ReplaceSuffix(s, len, 4, "ate");
                break;
            case 's':
                if (EndsWith(s, len, "alism")) return ReplaceSuffix(s, len, 5, "al");
                if (EndsWith(s, len, "iveness")) return ReplaceSuffix(s, len, 7, "ive");
                if (EndsWith(s, len, "fulness")) return ReplaceSuffix(s, len, 7, "ful");
                if (EndsWith(s, len, "ousness")) return ReplaceSuffix(s, len, 7, "ous");
                break;
            case 't':
                if (EndsWith(s, len, "aliti")) return ReplaceSuffix(s, len, 5, "al");
                if (EndsWith(s, len, "iviti")) return ReplaceSuffix(s, len, 5, "ive");
                if (EndsWith(s, len, "biliti")) return ReplaceSuffix(s, len, 6, "ble");
                break;
        }
        return len;
    }

    // --- Step 3: more suffixes ---
    private static int Step3(char[] s, int len)
    {
        if (len < 3) return len;

        switch (s[len - 1])
        {
            case 'e':
                if (EndsWith(s, len, "icate")) return ReplaceSuffix(s, len, 5, "ic");
                if (EndsWith(s, len, "ative")) return ReplaceSuffix(s, len, 5, "");
                if (EndsWith(s, len, "alize")) return ReplaceSuffix(s, len, 5, "al");
                break;
            case 'i':
                if (EndsWith(s, len, "iciti")) return ReplaceSuffix(s, len, 5, "ic");
                break;
            case 'l':
                if (EndsWith(s, len, "ical")) return ReplaceSuffix(s, len, 4, "ic");
                if (EndsWith(s, len, "ful")) return ReplaceSuffix(s, len, 3, "");
                break;
            case 's':
                if (EndsWith(s, len, "ness")) return ReplaceSuffix(s, len, 4, "");
                break;
        }
        return len;
    }

    // --- Step 4: remove suffixes if m > 1 ---
    private static int Step4(char[] s, int len)
    {
        if (len < 3) return len;

        switch (s[len - 2])
        {
            case 'a':
                if (EndsWith(s, len, "al") && MeasureBeforeSuffix(s, len, 2) > 1) return len - 2;
                break;
            case 'c':
                if (EndsWith(s, len, "ance") && MeasureBeforeSuffix(s, len, 4) > 1) return len - 4;
                if (EndsWith(s, len, "ence") && MeasureBeforeSuffix(s, len, 4) > 1) return len - 4;
                break;
            case 'e':
                if (EndsWith(s, len, "er") && MeasureBeforeSuffix(s, len, 2) > 1) return len - 2;
                break;
            case 'i':
                if (EndsWith(s, len, "ic") && MeasureBeforeSuffix(s, len, 2) > 1) return len - 2;
                break;
            case 'l':
                if (EndsWith(s, len, "able") && MeasureBeforeSuffix(s, len, 4) > 1) return len - 4;
                if (EndsWith(s, len, "ible") && MeasureBeforeSuffix(s, len, 4) > 1) return len - 4;
                break;
            case 'n':
                if (EndsWith(s, len, "ant") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                if (EndsWith(s, len, "ement") && MeasureBeforeSuffix(s, len, 5) > 1) return len - 5;
                if (EndsWith(s, len, "ment") && MeasureBeforeSuffix(s, len, 4) > 1) return len - 4;
                if (EndsWith(s, len, "ent") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
            case 'o':
                if (EndsWith(s, len, "ion") && len >= 4 && MeasureBeforeSuffix(s, len, 3) > 1 &&
                    (s[len - 4] == 's' || s[len - 4] == 't')) return len - 3;
                if (EndsWith(s, len, "ou") && MeasureBeforeSuffix(s, len, 2) > 1) return len - 2;
                break;
            case 's':
                if (EndsWith(s, len, "ism") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
            case 't':
                if (EndsWith(s, len, "ate") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                if (EndsWith(s, len, "iti") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
            case 'u':
                if (EndsWith(s, len, "ous") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
            case 'v':
                if (EndsWith(s, len, "ive") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
            case 'z':
                if (EndsWith(s, len, "ize") && MeasureBeforeSuffix(s, len, 3) > 1) return len - 3;
                break;
        }
        return len;
    }

    // --- Step 5a: remove trailing 'e' ---
    private static int Step5a(char[] s, int len)
    {
        if (s[len - 1] != 'e') return len;
        int m = MeasureBeforeSuffix(s, len, 1);
        if (m > 1) return len - 1;
        if (m == 1 && !EndsWithCvc(s, len - 1)) return len - 1;
        return len;
    }

    // --- Step 5b: -ll → -l if m > 1 ---
    private static int Step5b(char[] s, int len)
    {
        if (len > 1 && s[len - 1] == 'l' && s[len - 2] == 'l' && MeasureBeforeSuffix(s, len, 1) > 1)
            return len - 1;
        return len;
    }

    // --- Helper: replace suffix if m > 0 for the stem ---
    private static int ReplaceSuffix(char[] s, int len, int suffixLen, string replacement)
    {
        if (MeasureBeforeSuffix(s, len, suffixLen) > 0)
        {
            int stemEnd = len - suffixLen;
            for (int i = 0; i < replacement.Length; i++)
                s[stemEnd + i] = replacement[i];
            return stemEnd + replacement.Length;
        }
        return len;
    }

    // --- Helper: compute the "measure" m of the portion s[0..len-suffixLen) ---
    private static int MeasureBeforeSuffix(char[] s, int len, int suffixLen)
    {
        int end = len - suffixLen;
        if (end <= 0) return 0;

        int m = 0;
        int i = 0;

        // Skip leading vowels
        while (i < end && IsVowelAt(s, i, end)) i++;

        while (i < end)
        {
            // Skip consonants
            while (i < end && !IsVowelAt(s, i, end)) i++;
            if (i >= end) break;
            m++;
            // Skip vowels
            while (i < end && IsVowelAt(s, i, end)) i++;
        }
        return m;
    }

    // --- Helper: does the stem end with consonant-vowel-consonant (where last C != w/x/y)? ---
    private static bool EndsWithCvc(char[] s, int len)
    {
        if (len < 3) return false;
        char c = s[len - 1];
        if (c == 'w' || c == 'x' || c == 'y') return false;
        return !IsVowelAt(s, len - 1, len) && IsVowelAt(s, len - 2, len) && !IsVowelAt(s, len - 3, len);
    }

    // --- Helper: is char at position i a vowel? ---
    private static bool IsVowelAt(char[] s, int i, int len)
    {
        if (i < 0 || i >= len) return false;
        char c = s[i];
        if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') return true;
        if (c == 'y' && i > 0 && !IsVowelAt(s, i - 1, len)) return true;
        return false;
    }

    private static bool IsVowel(char[] s, int i) => IsVowelAt(s, i, s.Length);

    // --- Helper: does s[0..len) end with the given suffix? ---
    private static bool EndsWith(char[] s, int len, string suffix)
    {
        if (suffix.Length > len) return false;
        int offset = len - suffix.Length;
        for (int i = 0; i < suffix.Length; i++)
        {
            if (s[offset + i] != suffix[i]) return false;
        }
        return true;
    }

    // --- Helper: does s[0..end) contain at least one vowel? ---
    private static bool ContainsVowelBefore(char[] s, int end)
    {
        for (int i = 0; i < end; i++)
            if (IsVowelAt(s, i, end)) return true;
        return false;
    }
}
