using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace OtakuLib
{
    public class SearchItem
    {
        public float Relevance;
        public Word Word;

        public SearchItem(Word word, float relevance)
        {
            Word = word;
            Relevance = relevance;
        }

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
        public string ThumbPinyin
        {
            get
            {
#if DEBUG
                return string.Format("{1:0.000} {0}", Word.ThumbPinyin, Relevance);
#else
                return Word.ThumbPinyin;
#endif
            }
        }
        public string ThumbTranslation  { get { return Word.ThumbTranslation; } }
    }

    // typedef hack
    [System.Runtime.InteropServices.ComVisible(false)]
    public class SearchResult : List<SearchItem> { }

    public class WordSearch
    {
        private struct SearchItem : IComparable<SearchItem>
        {
            public float Relevance;
            public Word Word;

            public SearchItem(Word word, float relevance)
            {
                Word = word;
                Relevance = relevance;
            }

            public int CompareTo(SearchItem other)
            {
                float diff = Relevance - other.Relevance;
                if (diff == 0)
                {
                    int _diff = Word.Hanzi.CompareTo(other.Word.Hanzi);
                    if (_diff == 0)
                    {
                        return Word.Traditional.CompareTo(other.Word.Traditional);
                    }
                    return _diff;
                }
                return -(int)(diff * (1 << 16));
            }
        }

        public const int MaxSearchResultCount = 25;
        public const float MinRelevance = 0.75f;

        public SearchQuery Query;

        public Task<SearchResult> SearchTask { get; private set; }
        public CancellationTokenSource SearchTaskCanceller { get; private set; }

        private WordSearch WaitForComplete = null;
        private Task<List<SearchItem>>[] SearchJobs = null;

        public WordSearch(string searchText, WordSearch waitForComplete = null)
        {
            Query = new SearchQuery(searchText);

            WaitForComplete = waitForComplete;
            SearchTaskCanceller = new CancellationTokenSource();
            SearchTask = Task.Run((Func<SearchResult>)Search, SearchTaskCanceller.Token);
        }

        public SearchResult Search()
        {
            CancellationToken cancellationToken = SearchTaskCanceller.Token;

            if (WaitForComplete != null)
            {
                try
                {
                    WaitForComplete.SearchTask.Wait(cancellationToken);
                }
                catch (AggregateException) { }

                if (WaitForComplete.SearchJobs != null)
                {
                    try
                    {
                        Task.WaitAll(WaitForComplete.SearchJobs, cancellationToken);
                    }
                    catch (AggregateException) { }
                }
                WaitForComplete = null;
            }

            Task<WordDictionary> dictionaryLoader = DictionaryLoader.Current.LoadTask;
            if (!dictionaryLoader.IsCompleted)
            {
                dictionaryLoader.Wait(cancellationToken);
            }

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            WordDictionary dictionary = dictionaryLoader.Result;

            const int wordSliceSize = 20000;
            int wordSliceStart = 0;
            int wordSliceEnd = wordSliceSize;
            
            SearchJobs = new Task<List<SearchItem>>[(dictionary.Count + wordSliceSize - 1)/wordSliceSize];
            for (int i = 0; i < SearchJobs.Length; ++i)
            {
                SearchWorkJob searchJob = new SearchWorkJob(Query, dictionary, wordSliceStart, Math.Min(wordSliceEnd, dictionary.Count), cancellationToken);
                
                SearchJobs[i] = Task.Run((Func<List<SearchItem>>)searchJob.Run, cancellationToken);

                wordSliceStart = wordSliceEnd;
                wordSliceEnd += wordSliceSize;
            }
            Task.WaitAll(SearchJobs, cancellationToken);

            List<SearchItem> internalSearchResults = new List<SearchItem>();
            foreach (Task<List<SearchItem>> searchJob in SearchJobs)
            {
                internalSearchResults.AddRange(searchJob.Result);
                searchJob.Result.Clear();
                searchJob.Result.TrimExcess();
            }

            internalSearchResults.Sort();
            
            SearchResult searchResults = new SearchResult();
            int len = Math.Min(internalSearchResults.Count, MaxSearchResultCount);
            for (int i = 0; i < len; ++i)
            {
                searchResults.Add(new OtakuLib.SearchItem(internalSearchResults[i].Word, internalSearchResults[i].Relevance));
            }

            internalSearchResults.Clear();
            internalSearchResults.TrimExcess();

            stopWatch.Stop();
            
            Debug.WriteLine("Search time: {0}ms", stopWatch.ElapsedMilliseconds);

            return searchResults;
        }

        private class SearchWorkJob
        {
            private SearchQuery Query;
            private WordDictionary Dictionary;
            private int JobSliceStart;
            private int JobSliceEnd;

            private CancellationToken JobCancellationToken;

            public SearchWorkJob(SearchQuery query, WordDictionary dictionary, int jobSliceStart, int jobSliceEnd, CancellationToken cancellationToken)
            {
                Dictionary = dictionary;
                JobSliceStart = jobSliceStart;
                JobSliceEnd = jobSliceEnd;
                JobCancellationToken = cancellationToken;
                Query = query;
            }
            
            public List<SearchItem> Run()
            {
                List<SearchItem> searchResults = new List<SearchItem>();

                for (int i = JobSliceStart; i < JobSliceEnd; ++i)
                {
                    if ((i & 255) == 0)
                    {
                        JobCancellationToken.ThrowIfCancellationRequested();
                    }

                    Word word = Dictionary[i];
                    
                    float relevance = Query.SearchWord(word);
                    if (relevance > MinRelevance)
                    {
                        searchResults.Add(new SearchItem(word, relevance));
                    }
                }

                return searchResults;
            }
        }
    }
}
