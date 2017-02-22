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

        private string[] ChineseSearchWords = null;
        private StringSearch[] PinyinSearchWords = null;
        private StringSearch[] SearchWords = null;

        private int SearchScopeMin = 0;
        private int SearchScopeMax = 0;
        
        private static readonly char[] WordSeperators = {' '};
        private const CompareOptions PinyinCompareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreNonSpace;

        public SearchQuery(string searchText)
        {
            SearchText = searchText.Replace("'", "");
            
            List<string> chineseSearchWords = new List<string>();
            List<StringSearch> pinyinSearchWords = new List<StringSearch>();
            List<StringSearch> searchWords = new List<StringSearch>();

            foreach (string searchWord in SearchText.Split(WordSeperators, StringSplitOptions.RemoveEmptyEntries))
            {
                StringSearch stringSearch = new StringSearch(searchWord); 
                if (searchWord.IsChinese())
                {
                    chineseSearchWords.Add(searchWord);
                }
                else
                {
                    if (searchWord.IsPinyin())
                    {
                        pinyinSearchWords.Add(stringSearch);
                    }
                    searchWords.Add(stringSearch);
                }
            }
            SearchWords = searchWords.Count > 0 ? searchWords.ToArray() : null;
            ChineseSearchWords = chineseSearchWords.Count > 0 ? chineseSearchWords.ToArray() : null;
            PinyinSearchWords = pinyinSearchWords.Count > 0 ? pinyinSearchWords.ToArray() : null;

            if (SearchWords != null)
            {
                int totalCharCount = SearchWords.Aggregate(0, (int acc, StringSearch s) => { return acc + s.SearchLength; });
                SearchScopeMin = totalCharCount / 2;
                SearchScopeMax = totalCharCount * 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRelevance(StringPointer str, float strActualLength, float matchActualLength, int matchBegin, int matchEnd)
        {
            bool wordStart = matchBegin == str.Start || WordDictionary.StringMemory[matchBegin - 1].IsBlank();
            bool wordEnd   = matchEnd == str.End || WordDictionary.StringMemory[matchEnd].IsBlank();

            float relevance = matchActualLength/strActualLength;
            relevance += wordStart ? 0.35f : 0f;
            relevance += wordEnd ? 0.2f : 0f;

            return relevance;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPinyinRelevance(StringPointer str, float strActualLength, StringSearch search)
        {
            StringSearch.Result result = search.SearchIn(str, SearchFlags.IGNORE_DIACRITICS | SearchFlags.IGNORE_NON_LETTER);
            
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
                result = StringSearch.Search(hanzi, chineseSearchWord, 0, chineseSearchWord.Length, SearchFlags.NONE);

                if (result.Found)
                {
                    // in this case, order the results based on character order in the word
                    relevance += 4 - (float)result.Start/chineseSearchWord.Length;
                }

                // check if the word (AB) contains our search (A)
                result = StringSearch.Search(chineseSearchWord, 0, chineseSearchWord.Length, hanzi);

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
                factor *= 0.95f;
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
                factor *= 0.95f;
            }
            return relevance;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SearchWord(Word word)
        {
            return Math.Max(SearchHanzis(word.Hanzi, word.Traditional), Math.Max(SearchPinyins(word.Pinyins), SearchTranslations(word.Translations)));
        }
    }
}
