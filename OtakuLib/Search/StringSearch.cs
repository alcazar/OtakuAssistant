using System;
using System.Text;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    [Flags]
    public enum SearchFlags
    {
        NONE = 0,
        IGNORE_CASE = 1,
        IGNORE_DIACRITICS = 2,
        IGNORE_NON_LETTER = 4,
    }

    public class StringSearch
    {
        public struct Result : IEquatable<Result>
        {
            public int Start;
            public int End;

            public int Length { get { return End - Start; } }
            public bool Found { get { return End != Start; } }

            public Result(int start, int end)
            {
                Start = start;
                End = end;
            }

            public override bool Equals(object obj)
            {
                return (obj as Result?)?.Equals(this) ?? false;
            }

            public override int GetHashCode()
            {
                return Start | End;
            }

            public static bool operator ==(Result a, Result b)
            {
                return a.Equals(b);
            }

            public static bool operator !=(Result a, Result b)
            {
                return !a.Equals(b);
            }

            public bool Equals(Result other)
            {
                return Start == other.Start && End == other.End;
            }

            public override string ToString()
            {
                return string.Format("[{0}:{1}[", Start, End);
            }
        }

        public string SearchStr { get; private set; }
        public int SearchStart { get; private set; }
        public int SearchLength { get; private set; }
        public int SearchActualLength { get; private set; }

        private ushort[] Backtrack;

        public static string PreprocessStr(string str, int start, int length, SearchFlags searchFlags, out int actualLength)
        {
            StringBuilder stringBuilder = new StringBuilder();

            actualLength = 0;
            for (int i = 0; i < length; ++i)
            {
                char c = str[i + start];
                if (c.IsHighSurrogate())
                {
                    stringBuilder.Append(c);

                    // low surrogate
                    ++i;
                    stringBuilder.Append(str[i + start]);

                    // two characters marks as one
                    ++actualLength;
                }
                else if (c.IsDiacritic())
                {
                    if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) == 0)
                    {
                        stringBuilder.Append(c);
                    }
                }
                else
                {
                    // normal character
                    ++actualLength;

                    if (c.IsBlank())
                    {
                        if ((searchFlags & SearchFlags.IGNORE_NON_LETTER) == 0)
                        {
                            stringBuilder.Append(c);
                        }
                    }
                    else
                    {
                        // precomposed characters
                        if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                        {
                            if (c < RemoveDiacritic.Length)
                            {
                                c = RemoveDiacritic[c];
                            }
                        }
                        if ((searchFlags & SearchFlags.IGNORE_CASE) != 0 && 'A' <= c && c <= 'Z')
                        {
                            c = (char)(c - 'A' + 'a');
                        }
                        stringBuilder.Append(c);
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public StringSearch(string str, int start, int length, int actualLength, SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            if (searchFlags != SearchFlags.NONE)
            {
                SearchStr = PreprocessStr(str, start, length, searchFlags, out actualLength);
                SearchStart = 0;
                SearchLength = SearchStr.Length;
            }
            else
            {
                SearchStr = str;
                SearchStart = start;
                SearchLength = length;

                if (actualLength < 0)
                {
                    actualLength = str.ActualLength(start, length);
                }
            }
            SearchActualLength = actualLength;

            Backtrack = new ushort[length];
            for (int i = 0; i < length; ++i)
            {
                Backtrack[i] = 0;
                for (int j = i - 1; j > 0; --j)
                {
                    if (string.Compare(str, 0, str, i - j, j) == 0)
                    {
                        Backtrack[i] = (ushort)j;
                        break;
                    }
                }
            }
        }

        public StringSearch(string str, SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
            : this(str, 0, str.Length, -1, searchFlags)
        {
        }

        public StringSearch(StringPointer str, SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
            : this(WordDictionary.StringMemory, str.Start, str.Length, str.ActualLength, searchFlags)
        {
        }

        private static char[] RemoveDiacritic;
        static StringSearch()
        {
            RemoveDiacritic = new char[0x200];

            for (int i = 0; i < RemoveDiacritic.Length; ++i)
            {
                string s = new string(new char[]{ (char)i }).RemoveAccents();
                RemoveDiacritic[i] = s != null && s.Length > 0 ? s[0] : '\0';
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result SearchIn(string str, int start, int length,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            Result result = new Result();
            result.Start = start;

            int searchIndex = 0;

            int carret = start;
            int end = carret + length;

            while (carret < end)
            {
                char c = str[carret];
                ++carret;

                if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                {
                    if (c.IsDiacritic())
                    {
                        continue;
                    }
                    // check if precombined
                    if (c < 0x200)
                    {
                        c = RemoveDiacritic[c];
                    }
                }
                if ((searchFlags & SearchFlags.IGNORE_NON_LETTER) != 0 && c.IsBlank())
                {
                    continue;
                }
                if ((searchFlags & SearchFlags.IGNORE_CASE) != 0 && 'A' <= c && c <= 'Z')
                {
                    c = (char)(c - 'A' + 'a');
                }

                if (SearchStr[SearchStart + searchIndex] == c)
                {
                    ++searchIndex;
                    if (searchIndex == SearchLength)
                    {
                        if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                        {
                            while (carret < end && str[carret].IsDiacritic())
                            {
                                ++carret;
                            }
                        }
                        result.End = carret;
                        return result;
                    }
                }
                else if (searchIndex > 0)
                {
                    int oldIndex = searchIndex;
                    do
                    {
                        searchIndex = Backtrack[searchIndex];
                    }
                    while (searchIndex > 0 && SearchStr[SearchStart + searchIndex] != c);
                        
                    --carret;
                    if (searchIndex > 0)
                    {
                        do
                        {
                            ++result.Start;
                            if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0 && c.IsDiacritic())
                            {
                                continue;
                            }
                            
                            if ((searchFlags & SearchFlags.IGNORE_NON_LETTER) != 0 && c.IsBlank())
                            {
                                continue;
                            }

                            --oldIndex;
                        }
                        while (oldIndex > searchIndex);
                    }
                    else
                    {
                        result.Start = carret;
                    }
                }
                else
                {
                    result.Start = carret;
                }
            }

            result.End = result.Start;
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result SearchIn(string str, SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return SearchIn(str, 0, str.Length, searchFlags);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result SearchIn(StringPointer str, SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return SearchIn(WordDictionary.StringMemory, str.Start, str.Length, searchFlags);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char GetNextSearchIndex(string search, int searchStart, ref int searchIndex, int searchLength, SearchFlags searchFlags)
        {
            for (;searchIndex < searchLength; ++searchIndex)
            {
                char c = search[searchStart + searchIndex];

                if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                {
                    if (c.IsDiacritic())
                    {
                        continue;
                    }
                    // check if precombined
                    if (c < 0x200)
                    {
                        c = RemoveDiacritic[c];
                    }
                }
                if ((searchFlags & SearchFlags.IGNORE_NON_LETTER) != 0 && c.IsBlank())
                {
                    continue;
                }
                if ((searchFlags & SearchFlags.IGNORE_CASE) != 0 && 'A' <= c && c <= 'Z')
                {
                    c = (char)(c - 'A' + 'a');
                }

                return c;
            }
            return '\0';
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result Search(string search, int searchStart, int searchLength, string str, int start, int length,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            Result result = new Result();
            result.Start = start;

            int searchIndex = 0;

            int carret = start;
            int end = carret + length;
            
            char c2 = GetNextSearchIndex(search, searchStart, ref searchIndex, searchLength, searchFlags);

            while (carret < end)
            {
                char c = str[carret];
                ++carret;
                
                if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                {
                    if (c.IsDiacritic())
                    {
                        continue;
                    }
                    // check if precombined
                    if (c < 0x200)
                    {
                        c = RemoveDiacritic[c];
                    }
                }
                if ((searchFlags & SearchFlags.IGNORE_NON_LETTER) != 0 && c.IsBlank())
                {
                    continue;
                }
                if ((searchFlags & SearchFlags.IGNORE_CASE) != 0 && 'A' <= c && c <= 'Z')
                {
                    c = (char)(c - 'A' + 'a');
                }

                if (c == c2)
                {
                    ++searchIndex;

                    c2 = GetNextSearchIndex(search, searchStart, ref searchIndex, searchLength, searchFlags);

                    if (searchIndex == searchLength)
                    {
                        if ((searchFlags & SearchFlags.IGNORE_DIACRITICS) != 0)
                        {
                            while (carret < end && str[carret].IsDiacritic())
                            {
                                ++carret;
                            }
                        }
                        result.End = carret;
                        return result;
                    }
                }
                else
                {
                    ++result.Start;
                    carret = result.Start;
                    if (searchIndex > 0)
                    {
                        searchIndex = 0;
                        c2 = GetNextSearchIndex(search, searchStart, ref searchIndex, searchLength, searchFlags);
                    }
                }
            }

            result.End = result.Start;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result Search(string search, string str,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return Search(search, 0, search.Length, str, 0, str.Length, searchFlags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result Search(StringPointer search, string str, int start, int length,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return Search(WordDictionary.StringMemory, search.Start, search.Length, str, start, length, searchFlags);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result Search(string search, int searchStart, int searchLength, StringPointer str,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return Search(search, searchStart, searchLength, WordDictionary.StringMemory, str.Start, str.Length, searchFlags);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result Search(StringPointer search, StringPointer str,
            SearchFlags searchFlags = SearchFlags.IGNORE_CASE)
        {
            return Search(WordDictionary.StringMemory, search.Start, search.Length, WordDictionary.StringMemory, str.Start, str.Length, searchFlags);
        }
    }
}
