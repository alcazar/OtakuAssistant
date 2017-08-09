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

            if (MasterStartNotifier != null)
            {
                MasterStartNotifier.Set();
            }
            Debug.WriteLine("Search queued");
        }

        private static Mutex SearchQueueMutex = new Mutex();
        private static Queue<WordSearch> SearchQueue = new Queue<WordSearch>();
        
        private static SearchQuery CurrentQuery = null;
        private static List<SearchItem> InternalSearchResults = new List<SearchItem>();

        private static CancellationTokenSource SearchServiceStopTokenSource = null;
        private static ManualResetEventSlim MasterStartNotifier = null;
        private static SemaphoreSlim WorkerStartNotifier = null;
        private static SemaphoreSlim WorkerCompleteNotifier = null;
        private static SemaphoreSlim MasterCompleteNotifier = null;

        private static Task MasterJob = null;
        private static SearchWorkJob[] WorkerJobs = null;

        public static void StartSearchService(bool warmup = true, bool multithreaded = true)
        {
            if (MasterJob == null)
            {
                if (warmup && SearchQueue.Count == 0)
                {
                    new WordSearch("warmup", null, false);
                }

                WorkerJobs = new SearchWorkJob[4];

                SearchServiceStopTokenSource = new CancellationTokenSource();
                MasterStartNotifier = new ManualResetEventSlim(false, 1);
                WorkerStartNotifier = new SemaphoreSlim(0, WorkerJobs.Length);
                WorkerCompleteNotifier = new SemaphoreSlim(0, WorkerJobs.Length);
                MasterCompleteNotifier = new SemaphoreSlim(0, WorkerJobs.Length);

                if (multithreaded)
                {
                    MasterJob = Task.Factory.StartNew(SearchMasterJobMultiThread, TaskCreationOptions.LongRunning);
                }
                else
                {
                    MasterJob = Task.Factory.StartNew(SearchMasterJobSingleThread, TaskCreationOptions.LongRunning);
                }

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
                MasterStartNotifier.Dispose();
                MasterStartNotifier = null;
                WorkerStartNotifier.Dispose();
                WorkerStartNotifier = null;
                WorkerCompleteNotifier.Dispose();
                WorkerCompleteNotifier = null;
                MasterCompleteNotifier.Dispose();
                MasterCompleteNotifier = null;
            
                MasterJob = null;
                WorkerJobs = null;
            }
        }

        private static void SearchMasterJobSingleThread()
        {
            Queue<WordSearch> searchQueue = SearchQueue;
            
            List<SearchItem> internalSearchResults = InternalSearchResults;

            Stopwatch totalTime = new Stopwatch();
            Stopwatch dequeue = new Stopwatch();
            Stopwatch initQuery = new Stopwatch();
            Stopwatch jobSearch = new Stopwatch();
            Stopwatch generateResults = new Stopwatch();

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
                        while (!MasterStartNotifier.Wait(1, SearchServiceStopTokenSource.Token)) ;
                    }
                    else
                    {
                        // check if we have to quit now
                        SearchServiceStopTokenSource.Token.ThrowIfCancellationRequested();
                    }
                    MasterStartNotifier.Reset();

                    Debug.WriteLine("Search start");

                    totalTime.Restart();

                    dequeue.Restart();

                    WordSearch currentSearch;
                    lock (SearchQueueMutex)
                    {
                        currentSearch = searchQueue.Dequeue();
                    }

                    dequeue.Stop();

                    initQuery.Restart();

                    CurrentQuery = new SearchQuery(currentSearch.SearchText);

                    initQuery.Stop();

                    if (CurrentQuery.searchScope != SearchScope.NONE)
                    {
                        jobSearch.Restart();

                        internalSearchResults.Clear();

                        switch (CurrentQuery.searchScope)
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

                        jobSearch.Stop();

                        generateResults.Restart();

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
                    Debug.WriteLine("   Dequeue: 0.{0:fffffff}s", dequeue.Elapsed);
                    Debug.WriteLine("   InitQuery: 0.{0:fffffff}s", initQuery.Elapsed);
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Run(SearchScope searchScope)
        {
            WordDictionary dictionary = WordDictionary.Words;

            SearchQuery query = CurrentQuery;
            List<SearchItem> internalSearchResults = InternalSearchResults;

            int end = dictionary.Count;

            for (int i = 0; i < end; ++i)
            {
                Word word = dictionary[i];

                float relevance = query.SearchWord(word, i, searchScope);
                if (relevance > MinRelevance)
                {
                    internalSearchResults.Add(new SearchItem(word, relevance));
                }
            }
        }

        private static void SearchMasterJobMultiThread()
        {
            Queue<WordSearch> searchQueue = SearchQueue;

            List<SearchItem> internalSearchResults = new List<SearchItem>();

            Stopwatch totalTime = new Stopwatch();
            Stopwatch dequeue = new Stopwatch();
            Stopwatch initQuery = new Stopwatch();
            Stopwatch jobSearch = new Stopwatch();
            Stopwatch generateResults = new Stopwatch();

            int jobSliceSize = (WordDictionary.Words.Count + WorkerJobs.Length - 1)/WorkerJobs.Length;
            int jobSliceStart = 0;
            int jobSliceEnd = jobSliceSize;
            for (int i = 0; i < WorkerJobs.Length; ++i)
            {
                WorkerJobs[i] = new SearchWorkJob(jobSliceStart, Math.Min(jobSliceEnd, WordDictionary.Words.Count));
                WorkerJobs[i].task = Task.Factory.StartNew(WorkerJobs[i].Run, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning);

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
                        while (!MasterStartNotifier.Wait(1, SearchServiceStopTokenSource.Token));
                    }
                    else
                    {
                        // check if we have to quit now
                        SearchServiceStopTokenSource.Token.ThrowIfCancellationRequested();
                    }
                    MasterStartNotifier.Reset();

                    Debug.WriteLine("Search start");

                    totalTime.Restart();

                    dequeue.Restart();

                    WordSearch currentSearch;
                    lock (SearchQueueMutex)
                    {
                        currentSearch = searchQueue.Dequeue();
                    }

                    dequeue.Stop();

                    initQuery.Restart();

                    CurrentQuery = new SearchQuery(currentSearch.SearchText);

                    initQuery.Stop();

                    if (CurrentQuery.searchScope != SearchScope.NONE)
                    {
                        jobSearch.Restart();
                        
                        // kick in the jobs
                        WorkerStartNotifier.Release(WorkerJobs.Length);

                        // wait for jobs to do the work
                        for (int i = 0; i < WorkerJobs.Length; ++i)
                        {
                            while (!WorkerCompleteNotifier.Wait(1, SearchServiceStopTokenSource.Token));
                        }

                        // notify all the jobs that everything is complete
                        MasterCompleteNotifier.Release(WorkerJobs.Length);

                        jobSearch.Stop();

                        generateResults.Restart();

                        internalSearchResults.Clear();
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
                    Debug.WriteLine("   Dequeue: 0.{0:fffffff}s", dequeue.Elapsed);
                    Debug.WriteLine("   InitQuery: 0.{0:fffffff}s", initQuery.Elapsed);
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
                Stopwatch timer = new Stopwatch();

                try
                {
                    while (true)
                    {
                        // wait for the master to kick in the job
                        while (!WorkerStartNotifier.Wait(1, SearchServiceStopTokenSource.Token));

                        timer.Restart();

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

                        timer.Stop();
                        
                        // we finished our work, let the master know about it
                        WorkerCompleteNotifier.Release();

                        Debug.WriteLine("Job completed search in 0.{0:fffffff}s", timer.Elapsed);

                        // wait for the master to acknowledge that all jobs have finished
                        while (!MasterCompleteNotifier.Wait(1, SearchServiceStopTokenSource.Token));
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

                    float relevance = query.SearchWord(word, i, searchScope);
                    if (relevance > MinRelevance)
                    {
                        Results.Add(new SearchItem(word, relevance));
                    }
                }
            }
        }
    }
}
