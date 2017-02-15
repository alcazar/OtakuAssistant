using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace OtakuLib
{
    public struct SearchItem : IComparable<SearchItem>
    {
        public float Relevance;
        public Word Word;

        public string Hanzi             { get { return Word.Hanzi; } }
        public string Traditional
        {
            get
            {
                return (Word.Traditional != null && Word.Traditional.Length <= 4)
                    ? Word.Traditional
                    : string.Empty;
            }
        }
        public string ThumbPinyin       { get { return Word.ThumbPinyin; } }
        public string ThumbTranslation  { get { return Word.ThumbTranslation; } }

        public int CompareTo(SearchItem other)
        {
            float diff = Relevance - other.Relevance;
            if (diff == 0)
            {
                int _diff = Hanzi.CompareTo(other.Hanzi);
                if (_diff == 0)
                {
                    return (Traditional ?? string.Empty).CompareTo(other.Traditional ?? string.Empty);
                }
                return _diff;
            }
            return -(int)(diff * (1 << 16));
        }
    }

    // typedef hack
    public class SearchResult : List<SearchItem> { }

    public class WordSearch
    {
        const int MaxSearchResultCount = 100;
        const float MinRelevance = 0.5f;

        private string SearchText;

        public Task<SearchResult> SearchTask { get; private set; }
        public CancellationTokenSource SearchTaskCanceller { get; private set; }

        public WordSearch(string searchText)
        {
            SearchText = searchText;

            SearchTaskCanceller = new CancellationTokenSource();

            SearchTask = Task.Run((Func<SearchResult>)Search, SearchTaskCanceller.Token);
        }

        public SearchResult Search()
        {
            CancellationToken cancellationToken = SearchTaskCanceller.Token;

            Task<WordDictionary> dictionaryLoader = DictionaryLoader.Current.LoadTask;
            while (!dictionaryLoader.IsCompleted)
            {
                Task.Delay(50);
                cancellationToken.ThrowIfCancellationRequested();
            }

            WordDictionary dictionary = dictionaryLoader.Result;

            const int wordSliceSize = 20000;
            int wordSliceStart = 0;
            int wordSliceEnd = wordSliceSize;

            List<Task<SearchResult>> searchJobs = new List<Task<SearchResult>>();
            while (wordSliceStart < dictionary.Count)
            {
                SearchWorkJob searchJob = new SearchWorkJob(SearchText, dictionary, wordSliceStart, Math.Min(wordSliceEnd, dictionary.Count), SearchTaskCanceller.Token);
                
                searchJobs.Add(Task.Run((Func<SearchResult>)searchJob.Run, SearchTaskCanceller.Token));

                wordSliceStart = wordSliceEnd;
                wordSliceEnd += wordSliceSize;
            }

            Task.WaitAll(searchJobs.ToArray(), SearchTaskCanceller.Token);

            SearchResult searchResults = new SearchResult();
            foreach (Task<SearchResult> searchJob in searchJobs)
            {
                searchResults.AddRange(searchJob.Result);
                searchJob.Result.Clear();
                searchJob.Result.TrimExcess();
            }

            searchResults.Sort();

            if (searchResults.Count > MaxSearchResultCount)
            {
                searchResults.RemoveRange(MaxSearchResultCount, searchResults.Count - MaxSearchResultCount);
            }

            return searchResults;
        }

        private class SearchWorkJob
        {
            private string SearchText;
            private WordDictionary Dictionary;
            private int JobSliceStart;
            private int JobSliceEnd;

            private CancellationToken JobCancellationToken;

            public SearchWorkJob(string searchText, WordDictionary dictionary, int jobSliceStart, int jobSliceEnd, CancellationToken cancellationToken)
            {
                SearchText = searchText;
                Dictionary = dictionary;
                JobSliceStart = jobSliceStart;
                JobSliceEnd = jobSliceEnd;
                JobCancellationToken = cancellationToken;
            }
            
            public SearchResult Run()
            {
                bool searchTextIsChinese = SearchText.IsChinese();

                SearchResult searchResults = new SearchResult();
                SearchItem searchResult = new SearchItem();

                for (int i = JobSliceStart; i < JobSliceEnd; ++i)
                {
                    JobCancellationToken.ThrowIfCancellationRequested();

                    Word word = Dictionary[i];

                    searchResult.Word = word;

                    float nameRelevance = 0;
                    float pinyinRelevance = 0;
                    float translationRelevance = 0;
                
                    if (searchTextIsChinese)
                    {
                        nameRelevance = word.Hanzi.Search(SearchText);
                        if (nameRelevance == 0)
                        {
                            nameRelevance = SearchText.Search(word.Hanzi) * 0.85f;
                        }
                    }

                    foreach (Meaning meaning in word.Meanings)
                    {
                        float factor = 1;
                        foreach (string pinyin in meaning.Pinyins)
                        {
                            pinyinRelevance = Math.Max(pinyinRelevance, pinyin.Search(SearchText, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreNonSpace) * factor);
                            factor *= 0.95f;
                        }
                        factor = 1;
                        foreach (string translation in meaning.Translations)
                        {
                            translationRelevance = Math.Max(translationRelevance, translation.Search(SearchText) * factor);
                            factor *= 0.95f;
                        }
                    }

                    searchResult.Relevance = Math.Max(nameRelevance, Math.Max(pinyinRelevance, translationRelevance));
                    
                    if (searchResult.Relevance > MinRelevance)
                    {
                        searchResults.Add(searchResult);
                    }
                }

                return searchResults;
            }
        }
    }
}
