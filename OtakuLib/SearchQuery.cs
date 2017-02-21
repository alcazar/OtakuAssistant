using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    public class SearchQuery
    {
        public readonly string SearchText;

        private string[] SearchWords = null;
        private string[] ChineseSearchWords = null;
        private string[] PinyinSearchWords = null;

        private int SearchScopeMin = 0;
        private int SearchScopeMax = 0;
        
        private static readonly char[] WordSeperators = {' '};
        private const CompareOptions PinyinCompareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreNonSpace;

        public SearchQuery(string searchText)
        {
            SearchText = searchText.Replace("'", "");
            SearchWords = SearchText.Split(WordSeperators, StringSplitOptions.RemoveEmptyEntries);
            
            List<string> searchWords = new List<string>();
            List<string> chineseSearchWords = new List<string>();
            List<string> pinyinSearchWords = new List<string>();
            foreach (string searchWord in SearchWords)
            {
                if (searchWord.IsChinese())
                {
                    chineseSearchWords.Add(searchWord);
                }
                else
                {
                    if (searchWord.IsPinyin())
                    {
                        pinyinSearchWords.Add(searchWord);
                    }
                    searchWords.Add(string.Join(" ", searchWord));
                }
            }
            SearchWords = searchWords.Count > 0 ? searchWords.ToArray() : null;
            ChineseSearchWords = chineseSearchWords.Count > 0 ? chineseSearchWords.ToArray() : null;
            PinyinSearchWords = pinyinSearchWords.Count > 0 ? pinyinSearchWords.ToArray() : null;

            if (SearchWords != null)
            {
                int totalCharCount = SearchWords.Aggregate(0, (int acc, string s) => { return acc + s.Length; });
                SearchScopeMin = totalCharCount / 2;
                SearchScopeMax = totalCharCount * 3;
            }

            CurrentCompareInfo = CultureInfo.CurrentCulture.CompareInfo;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRelevance(StringPointer str, int matchBegin, int matchLength)
        {
            int matchEnd = matchBegin + matchLength;

            bool wordStart = matchBegin == str.Start || WordDictionary.StringMemory[matchBegin - 1].IsBlank();
            bool wordEnd   = matchEnd == str.End || WordDictionary.StringMemory[matchEnd].IsBlank();

            float relevance = (float)matchLength/str.Length;
            relevance += wordStart ? 0.35f : 0f;
            relevance += wordEnd ? 0.2f : 0f;

            return relevance;
        }

        private static CompareInfo CurrentCompareInfo;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(StringPointer str, string substr, CompareOptions compareOptions)
        {
            return CurrentCompareInfo.IndexOf(WordDictionary.StringMemory, substr, str.Start, str.Length, compareOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Search(StringPointer str, string substr)
        {
            int index = Compare(str, substr, CompareOptions.IgnoreCase |  CompareOptions.IgnoreNonSpace);
            
            return index >= 0 ? GetRelevance(str, index, substr.Length) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SearchNoSpace(StringPointer str, string substr)
        {
            int index = Compare(str, substr, CompareOptions.IgnoreCase |  CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols);
            
            return index >= 0 ? (float)substr.Length/str.Length + 0.5f : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Search(StringPointer str, string[] searchItems)
        {
            float relevance = 0;
            float factor = 0;
            foreach (string substr in searchItems)
            {
                float wordRelevance = Search(str, substr);
                if (relevance == 0 && wordRelevance == 0)
                {
                    // quick return if the first search item is not a match
                    return 0;
                }
                relevance += wordRelevance;
                factor += wordRelevance > WordSearch.MinRelevance ? 1 : 0;
            }
            return relevance * factor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SearchNoSpace(StringPointer str, string[] items)
        {
            float relevance = 0;
            float factor = 0;
            foreach (string substr in items)
            {
                float wordRelevance = SearchNoSpace(str, substr);
                if (relevance == 0 && wordRelevance == 0)
                {
                    // quick return if the first search item is not a match
                    return 0;
                }
                relevance += wordRelevance;
                factor += wordRelevance > WordSearch.MinRelevance ? 1 : 0;
            }
            return relevance * factor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchHanzi(StringPointer hanzi)
        {
            float relevance = 0;
            foreach (string chineseSearchWord in ChineseSearchWords)
            {
                // check if the word contains our search
                relevance += Search(hanzi, chineseSearchWord) * 2f;
                // check if the word is part of the search
                int index = chineseSearchWord.IndexOf(hanzi);
                if (index >= 0)
                {
                    // in this case, order the results based on character order in the word
                    relevance += 4 - (float)index/chineseSearchWord.Length;
                }
            }
            return relevance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchHanzi(StringPointer hanzi, StringPointer traditional)
        {
            if (ChineseSearchWords == null)
            {
                return 0f;
            }

            if (traditional.Length == 0)
            {
                return SearchHanzi(hanzi);
            }
            else
            {
                return Math.Max(SearchHanzi(hanzi), SearchHanzi(traditional));
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchPinyin(StringPointer pinyin)
        {
            return SearchNoSpace(pinyin, PinyinSearchWords);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchPinyins(StringList pinyins)
        {
            if (PinyinSearchWords == null)
            {
                return 0;
            }

            float relevance = 0f;
            float factor = 1f;
            foreach (StringPointer pinyin in pinyins)
            {
                relevance = Math.Max(relevance, factor * SearchPinyin(pinyin));
                factor *= 0.95f;
            }
            return relevance;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchTranslation(StringPointer translation)
        {
            if (SearchScopeMin <= translation.Length && translation.Length <= SearchScopeMax)
            {
                return Search(translation, SearchWords);
            }
            else
            {
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchTranslations(StringList translations)
        {
            if (SearchWords == null)
            {
                return 0f;
            }


            float relevance = 0f;
            float factor = 1f;
            foreach (StringPointer translation in translations)
            {
                relevance = Math.Max(relevance, factor * SearchTranslation(translation));
                factor *= 0.95f;
            }
            return relevance;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchWord(Word word)
        {
            return Math.Max(SearchHanzi(word.Hanzi, word.Traditional), Math.Max(SearchPinyins(word.Pinyins), SearchTranslations(word.Translations)));
        }
    }
}
