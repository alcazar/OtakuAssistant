using System;
using System.Globalization;
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

        public StringSearch(string str, int start, int length)
        {
            SearchStr = str;
            SearchStart = start;
            SearchLength = length;

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

            SearchActualLength = str.ActualLength(start, length);
        }

        public StringSearch(string str)
            : this(str, 0, str.Length)
        {
        }

        public StringSearch(StringPointer str)
            : this(WordDictionary.StringMemory, str.Start, str.Length)
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
                            if (!str[result.Start].IsDiacritic())
                            {
                                --oldIndex;
                            }
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
        public static Result Search(string search, int searchStart, int searchLength, string str, int start, int length,
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

                if (search[searchStart + searchIndex] == c)
                {
                    ++searchIndex;
                    if (searchIndex == searchLength)
                    {
                        result.End = carret;
                        return result;
                    }
                }
                else
                {
                    searchIndex = 0;
                    result.Start = carret;
                }
            }

            result.End = result.Start;
            return result;
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
