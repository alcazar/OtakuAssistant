using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    [Flags]
    internal enum SearchScope
    {
        NONE = 0,
        HANZI = 1,
        PINYIN = 2,
        TRANSLATION = 4,
    }

    internal class SearchQuery
    {

        public readonly string SearchText;
        public readonly SearchScope searchScope;

        private string[] ChineseSearchWords = null;
        private StringSearch[] PinyinSearchWords = null;
        private StringSearch[] SearchWords = null;

        private int SearchScopeMin = 0;
        private int SearchScopeMax = 0;
        
        private static readonly char[] WordSeperators = {' '};

        private const SearchFlags HanziSearchFlags = SearchFlags.NONE;
        private const SearchFlags PinyinSearchFlags = SearchFlags.IGNORE_DIACRITICS | SearchFlags.IGNORE_NON_LETTER;
        private const SearchFlags TranslationSearchFlags = SearchFlags.IGNORE_CASE;

        public SearchQuery(string searchText)
        {
            SearchText = searchText.Replace("'", "");
            
            List<string> chineseSearchWords = new List<string>();
            List<StringSearch> pinyinSearchWords = new List<StringSearch>();
            List<StringSearch> searchWords = new List<StringSearch>();

            foreach (string searchWord in SearchText.Split(WordSeperators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (searchWord.IsChinese())
                {
                    chineseSearchWords.Add(searchWord);
                }
                else
                {
                    if (searchWord.IsPinyin())
                    {
                        pinyinSearchWords.Add(new StringSearch(searchWord, SearchFlags.IGNORE_CASE | PinyinSearchFlags));
                    }
                    searchWords.Add(new StringSearch(searchWord, TranslationSearchFlags));
                }
            }

            searchScope = SearchScope.NONE;
            if (chineseSearchWords.Count > 0)
            {
                searchScope |= SearchScope.HANZI;
                ChineseSearchWords = chineseSearchWords.ToArray();
            }
            if (pinyinSearchWords.Count > 0)
            {
                searchScope |= SearchScope.PINYIN;
                PinyinSearchWords = pinyinSearchWords.ToArray();
            }
            if (searchWords.Count > 0)
            {
                searchScope |= SearchScope.TRANSLATION;
                SearchWords = searchWords.ToArray();

                int totalCharCount = 0;
                foreach (StringSearch search in SearchWords)
                {
                    totalCharCount += search.SearchActualLength;
                }
                SearchScopeMin = totalCharCount / 2;
                SearchScopeMax = totalCharCount * 5;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRelevance(StringPointer str, float strActualLength, float matchActualLength, int matchBegin, int matchEnd)
        {
            bool wordStart = matchBegin == str.Start || WordDictionary.StringMemory[matchBegin - 1].IsBlank();
            bool wordEnd   = matchEnd == str.End || WordDictionary.StringMemory[matchEnd].IsBlank();

            float relevance = matchActualLength/strActualLength;
            relevance += wordStart ? 0.8f : 0f;
            relevance += wordEnd ? 0.6f : 0f;

            return relevance;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPinyinRelevance(StringPointer str, float strActualLength, StringSearch search)
        {
            StringSearch.Result result = search.SearchIn(str, PinyinSearchFlags);
            
            return result.Found ? GetRelevance(str, strActualLength, search.SearchActualLength, result.Start, result.End) : 0;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetTranslationRelevance(StringPointer str, float strActualLength, StringSearch search)
        {
            StringSearch.Result result = search.SearchIn(str);
            
            return result.Found ? GetRelevance(str, strActualLength, search.SearchActualLength, result.Start, result.End) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchHanzi(StringPointer hanzi)
        {
            float strActualLength = hanzi.ActualLength;

            float relevance = 0;
            foreach (string chineseSearchWord in ChineseSearchWords)
            {
                StringSearch.Result result;

                // check if the word (AB) is part of the search (ABC)
                result = StringSearch.Search(hanzi, chineseSearchWord, 0, chineseSearchWord.Length, HanziSearchFlags);

                if (result.Found)
                {
                    // in this case, order the results based on character order in the word
                    relevance += 4 - (float)result.Start/chineseSearchWord.Length;
                }

                // check if the word (AB) contains our search (A)
                result = StringSearch.Search(chineseSearchWord, 0, chineseSearchWord.Length, hanzi, HanziSearchFlags);

                if (result.Found)
                {
                    relevance += 2 * (float)chineseSearchWord.Length/hanzi.ActualLength;
                }
            }
            return relevance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchPinyin(StringPointer str)
        {
            float strActualLength = str.ActualLength;

            float relevance = 0;
            float factor = 0;
            foreach (StringSearch searchWord in PinyinSearchWords)
            {
                float wordRelevance = GetPinyinRelevance(str, strActualLength, searchWord);
                
                if (relevance == 0 && wordRelevance == 0)
                {
                    // quick return if the first search word is not a match
                    return 0;
                }

                relevance += wordRelevance;
                factor += wordRelevance > WordSearch.MinRelevance ? 1 : 0;
            }
            return relevance * factor;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchTranslation(StringPointer translation)
        {
            if (!(SearchScopeMin <= translation.Length && translation.Length <= SearchScopeMax))
            {
                return 0;
            }

            float strActualLength = translation.ActualLength;

            float relevance = 0;
            float factor = 0;
            foreach (StringSearch searchWord in SearchWords)
            {
                float wordRelevance = GetTranslationRelevance(translation, strActualLength, searchWord);

                if (relevance == 0 && wordRelevance == 0)
                {
                    // quick return if the first search word is not a match
                    return 0;
                }
                relevance += wordRelevance;
                factor += wordRelevance > WordSearch.MinRelevance ? 1 : 0;
            }
            return relevance * factor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchHanzis(StringPointer hanzi, StringPointer traditional)
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
                factor *= 0.995f;
            }
            return relevance;
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
                factor *= 0.995f;
            }
            return relevance;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchWord(Word word, SearchScope searchScope)
        {
            float relevance = 0;
            if ((searchScope & SearchScope.HANZI) != 0)
            {
                relevance = Math.Max(relevance, SearchHanzis(word.Hanzi, word.Traditional));
            }
            if ((searchScope & SearchScope.PINYIN) != 0)
            {
                relevance = Math.Max(relevance, SearchPinyins(word.Pinyins) * 0.9f);
            }
            if ((searchScope & SearchScope.TRANSLATION) != 0)
            {
                relevance = Math.Max(relevance, SearchTranslations(word.Translations));
            }
            return relevance;
        }
    }
}
