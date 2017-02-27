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

        public string SearchText { get; private set; }
        public SearchResult Results { get; private set; }

        public delegate void SearchCompleteHandlerDelegate(WordSearch search);
        public SearchCompleteHandlerDelegate SearchCompleteHandler;

        public WordSearch(string searchText, SearchCompleteHandlerDelegate handler, bool clearQueue = true)
        {
            SearchText = searchText;
            SearchCompleteHandler = handler;

            lock (SearchQueueMutex)
            {
                if (clearQueue)
                {
                    SearchQueue.Clear();
                }
                SearchQueue.Enqueue(this);
            }
                
            if (MasterJobStartNotifier != null)
            {
                MasterJobStartNotifier.Set();
            }
            Debug.WriteLine("Search queued");
        }

        private static Mutex SearchQueueMutex = new Mutex();
        private static Queue<WordSearch> SearchQueue = new Queue<WordSearch>();
        
        private static SearchQuery CurrentQuery = null;

        private static CancellationTokenSource SearchServiceStopTokenSource = null;
        private static ManualResetEventSlim MasterJobStartNotifier = null;
        private static SemaphoreSlim WorkerStartNotifier = null;
        private static Barrier WorkerCompleteBarrier = null;

        private static Task MasterJob = null;
        private static SearchWorkJob[] WorkerJobs = null;

        public static void StartSearchService(bool warmup = true)
        {
            if (MasterJob == null)
            {
                if (warmup && SearchQueue.Count == 0)
                {
                    new WordSearch("warmup", null, false);
                }

                WorkerJobs = new SearchWorkJob[4];

                SearchServiceStopTokenSource = new CancellationTokenSource();
                MasterJobStartNotifier = new ManualResetEventSlim(false, 1);
                WorkerStartNotifier = new SemaphoreSlim(0, WorkerJobs.Length);
                WorkerCompleteBarrier = new Barrier(WorkerJobs.Length + 1);

                MasterJob = Task.Factory.StartNew(SearchMasterJob, TaskCreationOptions.None);

                Debug.WriteLine("Started search service");
            }
        }

        public static async Task WaitForSearchesToComplete()
        {
            int count;
            do
            {
                count = SearchQueue.Count;
                await Task.Delay(10);
            } while (count > 0);
        }

        public static async Task StopSearchService()
        {
            if (MasterJob != null)
            {
                lock (SearchQueueMutex)
                {
                    SearchQueue.Clear();
                }

                // new searches will be pushed to a new queue
                // and will be handled when restarting the search service
                SearchQueue = new Queue<WordSearch>();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                
                SearchServiceStopTokenSource.Cancel();
                await MasterJob.ConfigureAwait(false);
            
                stopwatch.Stop();
                Debug.WriteLine("Stopped search service in 0.{0:fffffff}s", stopwatch.Elapsed);

                SearchServiceStopTokenSource.Dispose();
                SearchServiceStopTokenSource = null;
                MasterJobStartNotifier.Dispose();
                MasterJobStartNotifier = null;
                WorkerStartNotifier.Dispose();
                WorkerStartNotifier = null;
                WorkerCompleteBarrier.Dispose();
                WorkerCompleteBarrier = null;
            
                MasterJob = null;
                WorkerJobs = null;
            }
        }

        private static void SearchMasterJob()
        {
            Queue<WordSearch> searchQueue = SearchQueue;

            int jobSliceSize = (WordDictionary.Words.Count + WorkerJobs.Length - 1)/WorkerJobs.Length;
            int jobSliceStart = 0;
            int jobSliceEnd = jobSliceSize;
            for (int i = 0; i < WorkerJobs.Length; ++i)
            {
                WorkerJobs[i] = new SearchWorkJob(jobSliceStart, Math.Min(jobSliceEnd, WordDictionary.Words.Count));
                WorkerJobs[i].task = Task.Factory.StartNew(WorkerJobs[i].Run, TaskCreationOptions.AttachedToParent);

                jobSliceStart = jobSliceEnd;
                jobSliceEnd += jobSliceSize;
            }

            try
            {
                while (true)
                {
                    int count = 0;
                    lock (SearchQueueMutex)
                    {
                        count = searchQueue.Count;
                    }

                    if (count == 0)
                    {
                        // no job
                        while (!MasterJobStartNotifier.Wait(1, SearchServiceStopTokenSource.Token));
                    }
                    else
                    {
                        // check if we have to quit now
                        SearchServiceStopTokenSource.Token.ThrowIfCancellationRequested();
                    }
                    MasterJobStartNotifier.Reset();

                    Debug.WriteLine("Search start");

                    Stopwatch totalTime = new Stopwatch();
                    Stopwatch initQuery = new Stopwatch();
                    Stopwatch initJobs = new Stopwatch();
                    Stopwatch jobSearch = new Stopwatch();
                    Stopwatch generateResults = new Stopwatch();

                    totalTime.Start();
                
                    initQuery.Start();

                    WordSearch currentSearch;
                    lock (SearchQueueMutex)
                    {
                        currentSearch = searchQueue.Dequeue();
                    }

                    CurrentQuery = new SearchQuery(currentSearch.SearchText);

                    initQuery.Stop();

                    if (CurrentQuery.searchScope != SearchScope.NONE)
                    {
                        initJobs.Start();
                        
                        WorkerStartNotifier.Release(WorkerJobs.Length);

                        initJobs.Stop();

                        jobSearch.Start();
                        // wait for jobs to do the work
                        WorkerCompleteBarrier.SignalAndWait(SearchServiceStopTokenSource.Token);
                        jobSearch.Stop();

                        generateResults.Start();

                        List<SearchItem> internalSearchResults = new List<SearchItem>();
                        foreach (SearchWorkJob workJob in WorkerJobs)
                        {
                            internalSearchResults.AddRange(workJob.Results);
                        }

                        internalSearchResults.Sort();
            
                        currentSearch.Results = new SearchResult();
                        int len = Math.Min(internalSearchResults.Count, MaxSearchResultCount);
                        for (int i = 0; i < len; ++i)
                        {
                            currentSearch.Results.Add(new OtakuLib.SearchItem(internalSearchResults[i].Word, internalSearchResults[i].Relevance));
                        }
            
                        generateResults.Stop();
                    }

                    totalTime.Stop();
            
                    Debug.WriteLine("Search time for {1}: 0.{0:fffffff}s", totalTime.Elapsed, currentSearch.SearchText);
                    Debug.WriteLine("   InitQuery: 0.{0:fffffff}s", initQuery.Elapsed);
                    Debug.WriteLine("   InitJobs: 0.{0:fffffff}s", initJobs.Elapsed);
                    Debug.WriteLine("   Search: 0.{0:fffffff}s", jobSearch.Elapsed);
                    Debug.WriteLine("   Results: 0.{0:fffffff}s", generateResults.Elapsed);
                    
                    currentSearch.SearchCompleteHandler?.Invoke(currentSearch);

                    CurrentQuery = null;
                }
            }
            catch (OperationCanceledException)
            {
                // return nicely
                Debug.WriteLine("Stopped master search thread");
            }
        }

        private class SearchWorkJob
        {
            public Task task;
            public int JobSliceStart;
            public int JobSliceEnd;
            public List<SearchItem> Results = new List<SearchItem>();

            public SearchWorkJob(int jobSliceStart, int jobSliceEnd)
            {
                JobSliceStart = jobSliceStart;
                JobSliceEnd = jobSliceEnd;
            }
            
            public void Run()
            {
                try
                {
                    while (true)
                    {
                        while (!WorkerStartNotifier.Wait(1, SearchServiceStopTokenSource.Token));

                        Results.Clear();

                        switch(CurrentQuery.searchScope)
                        {
                            case SearchScope.HANZI:
                                Run(SearchScope.HANZI);
                                break;
                            case SearchScope.PINYIN:
                                Run(SearchScope.PINYIN);
                                break;
                            case SearchScope.TRANSLATION:
                                Run(SearchScope.TRANSLATION);
                                break;
                            case SearchScope.HANZI | SearchScope.PINYIN:
                                Run(SearchScope.HANZI | SearchScope.PINYIN);
                                break;
                            case SearchScope.HANZI | SearchScope.TRANSLATION:
                                Run(SearchScope.HANZI | SearchScope.TRANSLATION);
                                break;
                            case SearchScope.PINYIN | SearchScope.TRANSLATION:
                                Run(SearchScope.PINYIN | SearchScope.TRANSLATION);
                                break;
                            case SearchScope.HANZI | SearchScope.PINYIN | SearchScope.TRANSLATION:
                                Run(SearchScope.HANZI | SearchScope.PINYIN | SearchScope.TRANSLATION);
                                break;
                            case SearchScope.NONE:
                            default:
                                break;
                        }

                        WorkerCompleteBarrier.SignalAndWait(SearchServiceStopTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // return nicely
                    Debug.WriteLine("Stopped search thread {0} - {1}", JobSliceStart, JobSliceEnd);
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Run(SearchScope searchScope)
            {
                WordDictionary dictionary = WordDictionary.Words;

                SearchQuery query = CurrentQuery;

                int start = JobSliceStart;
                int end = JobSliceEnd;

                for (int i = start; i < end; ++i)
                {
                    Word word = dictionary[i];

                    float relevance = query.SearchWord(word, searchScope);
                    if (relevance > MinRelevance)
                    {
                        Results.Add(new SearchItem(word, relevance));
                    }
                }
            }
        }
    }
}
