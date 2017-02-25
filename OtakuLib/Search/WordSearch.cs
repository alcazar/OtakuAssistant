using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
                    return Word.CompareTo(other.Word);
                }
                return -(int)(diff * (1 << 16));
            }
        }

        public const int MaxSearchResultCount = 50;
        public const float MinRelevance = 0.75f;

        public string SearchText;

        public Task<SearchResult> SearchTask { get; private set; }
        public CancellationTokenSource SearchTaskCanceller { get; private set; }

        private WordSearch WaitForComplete = null;
        private Task<List<SearchItem>>[] SearchJobs = null;

        public WordSearch(string searchText, WordSearch waitForComplete = null)
        {
            SearchText = searchText;
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
            Stopwatch initAndSpawnJobs = new Stopwatch();
            Stopwatch jobSearch = new Stopwatch();
            Stopwatch generateResults = new Stopwatch();

            stopWatch.Start();
            initAndSpawnJobs.Start();

            SearchQuery Query = new SearchQuery(SearchText);
            if (Query.searchScope == SearchScope.NONE)
            {
                // shortcut if we have a blank entry (like only spaces)
                return new SearchResult();
            }

            WordDictionary dictionary = dictionaryLoader.Result;

            const int wordSliceSize = 10000;
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

            initAndSpawnJobs.Stop();

            jobSearch.Start();
            Task.WaitAll(SearchJobs, cancellationToken);
            jobSearch.Stop();

            generateResults.Start();
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
            
            generateResults.Stop();
            stopWatch.Stop();
            
            Debug.WriteLine("Search time: {0}ms", stopWatch.ElapsedMilliseconds);
            Debug.WriteLine("   Init: {0}ms", initAndSpawnJobs.ElapsedMilliseconds);
            Debug.WriteLine("   Search: {0}ms", jobSearch.ElapsedMilliseconds);
            Debug.WriteLine("   Results: {0}ms", generateResults.ElapsedMilliseconds);

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
                switch(Query.searchScope)
                {
                    case SearchScope.NONE:
                        return null;
                    case SearchScope.HANZI:
                        return Run(SearchScope.HANZI);
                    case SearchScope.PINYIN:
                        return Run(SearchScope.PINYIN);
                    case SearchScope.TRANSLATION:
                        return Run(SearchScope.TRANSLATION);
                    case SearchScope.HANZI | SearchScope.PINYIN:
                        return Run(SearchScope.HANZI | SearchScope.PINYIN);
                    case SearchScope.HANZI | SearchScope.TRANSLATION:
                        return Run(SearchScope.HANZI | SearchScope.TRANSLATION);
                    case SearchScope.PINYIN | SearchScope.TRANSLATION:
                        return Run(SearchScope.PINYIN | SearchScope.TRANSLATION);
                    case SearchScope.HANZI | SearchScope.PINYIN | SearchScope.TRANSLATION:
                        return Run(SearchScope.HANZI | SearchScope.PINYIN | SearchScope.TRANSLATION);
                    default:
                        return null;
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public List<SearchItem> Run(SearchScope searchScope)
            {
                List<SearchItem> searchResults = new List<SearchItem>();

                for (int i = JobSliceStart; i < JobSliceEnd; ++i)
                {
                    if ((i & 1023) == 0)
                    {
                        JobCancellationToken.ThrowIfCancellationRequested();
                    }

                    Word word = Dictionary[i];
                    
                    float relevance = Query.SearchWord(word, searchScope);
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
