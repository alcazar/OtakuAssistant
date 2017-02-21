using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OtakuLib
{
    public abstract class DictionaryLoader
    {
        public static DictionaryLoader Current { get; private set; }

        public string DictionaryName { get; private set; }

        public Task<WordDictionary> LoadTask { get; private set; }
        public CancellationTokenSource LoadTaskCanceller { get; private set; }

        protected virtual bool AlreadySorted { get { return false; } }

        protected PortableFS FS;
        protected bool BuildThumbs;

        public DictionaryLoader(string dictionaryName, PortableFS portableFS, bool buildThumbs)
        {
            DictionaryName = dictionaryName;
            FS = portableFS;
            BuildThumbs = buildThumbs;

            if (LoadTaskCanceller != null)
            {
                LoadTaskCanceller.Cancel();
            }
            
            LoadTaskCanceller = new CancellationTokenSource();
            LoadTask = Task.Run(LoadDictionaryTask, LoadTaskCanceller.Token);

            Current = this;
        }

        protected abstract Task<List<DictionaryLoadJob>> GetDictionaryLoadJobs();

        private async Task<WordDictionary> LoadDictionaryTask()
        {
            // spawn jobs
            List<DictionaryLoadJob> dictionaryLoadJobs = await GetDictionaryLoadJobs();
            List<Task> jobTasks = new List<Task>();
            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                jobTasks.Add(Task.Run((Action)dictionaryLoadJob.LoadDictionaryPart, LoadTaskCanceller.Token));
            }

            // wait for all jobs to complete
            Task.WaitAll(jobTasks.ToArray(), LoadTaskCanceller.Token);
            
            StringBuilder StringMemoryBuilder = new StringBuilder();
            List<ushort> StringLengthMemoryBuilder = new List<ushort>();
            List<MeaningMemory> MeaningMemoryBuilder = new List<MeaningMemory>();

            List<Word> words = new List<Word>();

            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                foreach (Word word in dictionaryLoadJob.Words)
                {
                    word.StringStart += StringMemoryBuilder.Length;
                    word.ListStart += StringLengthMemoryBuilder.Count;
                    word.MeaningsMemory.MeaningStart += MeaningMemoryBuilder.Count;
                }
                words.AddRange(dictionaryLoadJob.Words);

                StringMemoryBuilder.Append(dictionaryLoadJob.StringMemoryBuilder);
                StringLengthMemoryBuilder.AddRange(dictionaryLoadJob.StringLengthMemoryBuilder);
                MeaningMemoryBuilder.AddRange(dictionaryLoadJob.MeaningMemoryBuilder.MeaningMemory);
            }

            WordDictionary.StringMemory = StringMemoryBuilder.ToString();
            WordDictionary.StringLengthMemory = StringLengthMemoryBuilder.ToArray();
            WordDictionary.MeaningMemory = MeaningMemoryBuilder.ToArray();

            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                dictionaryLoadJob.Dispose();
            }

            if (!AlreadySorted)
            {
                // sort
                words.Sort();
            
                // repack everything...
                StringMemoryBuilder.Clear();
                StringLengthMemoryBuilder.Clear();
                MeaningMemoryBuilder.Clear();

                foreach (Word word in words)
                {
                    StringMemoryBuilder.Append(WordDictionary.StringMemory.Substring(word.StringStart, word.TotalStringLength));
                    for (int i = 0; i < word.TotalListLength; ++i)
                    {
                        StringLengthMemoryBuilder.Add(WordDictionary.StringLengthMemory[word.ListStart + i]);
                    }
                    for (int i = 0; i < word.MeaningsMemory.MeaningLength; ++i)
                    {
                        MeaningMemoryBuilder.Add(WordDictionary.MeaningMemory[word.MeaningsMemory.MeaningStart + i]);
                    }
                    word.StringStart = StringMemoryBuilder.Length - word.TotalStringLength;
                    word.ListStart = StringLengthMemoryBuilder.Count - word.TotalListLength;
                    word.MeaningsMemory.MeaningStart = MeaningMemoryBuilder.Count - word.MeaningsMemory.MeaningLength;
                }

                WordDictionary.StringMemory = StringMemoryBuilder.ToString();
                WordDictionary.StringLengthMemory = StringLengthMemoryBuilder.ToArray();
                WordDictionary.MeaningMemory = MeaningMemoryBuilder.ToArray();
            }

            // create the dictionary
            return new WordDictionary(words);
        }

        // helper class for dictionary loading job
        protected abstract class DictionaryLoadJob : IDisposable
        {
            protected Stream LoadStream;
            protected CancellationToken JobCancellationToken;
            protected bool BuildThumbs;
            
            internal List<Word> Words = new List<Word>();
            internal StringBuilder StringMemoryBuilder = new StringBuilder();
            internal List<ushort> StringLengthMemoryBuilder = new List<ushort>();
            internal MeaningListMemoryBuilder MeaningMemoryBuilder = new MeaningListMemoryBuilder();

            public DictionaryLoadJob(Stream stream, CancellationToken cancellationToken, bool buildThumbs)
            {
                LoadStream = stream;
                JobCancellationToken = cancellationToken;
                BuildThumbs = buildThumbs;
            }

            private bool disposed;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        LoadStream.Dispose();
                    }
                    disposed = true;
                }
            }

            public abstract void LoadDictionaryPart();
            
            private List<string> ThumbBuilder = new List<string>();
            private List<string> ThumbSmall = new List<string>();
            private List<string> ThumbMedium = new List<string>();
            private List<string> ThumbLarge = new List<string>();

            protected string BuildThumb(IEnumerable<string> list)
            {
                if (!BuildThumbs)
                {
                    return string.Empty;
                }

                const int maxThumbCharacters = 32;
                int totalCharacters = 0;
                
                foreach (string str in list)
                {
                    if (str.Length <= 8)
                    {
                        ThumbSmall.Add(str);
                    }
                    else if (str.Length <= 16)
                    {
                        ThumbMedium.Add(str);
                    }
                    else
                    {
                        ThumbLarge.Add(str);
                    }
                }

                // take small definitions first (like "to eat")
                foreach (string str in ThumbSmall)
                {
                    if (totalCharacters < maxThumbCharacters && ThumbBuilder.Count < 3)
                    {
                        ThumbBuilder.Add(str);
                        totalCharacters += str.Length;
                    }
                }
            
                // take medium definitions (like "to have lunch")
                foreach (string str in ThumbMedium)
                {
                    if (totalCharacters < maxThumbCharacters && ThumbBuilder.Count < 3)
                    {
                        ThumbBuilder.Add(str);
                        totalCharacters += str.Length;
                    }
                }

                // if there is at least 16 characters left, we can put one large definition with ellipsis
                foreach (string str in ThumbLarge)
                {
                    if (totalCharacters + 16 < maxThumbCharacters && ThumbBuilder.Count < 3)
                    {
                        if (str.Length < maxThumbCharacters - totalCharacters)
                        {
                            ThumbBuilder.Add(str);
                            totalCharacters += str.Length;
                        }
                        else
                        {
                            string s = str.Substring(0, maxThumbCharacters - totalCharacters - 3) + "...";
                            ThumbBuilder.Add(s);
                            totalCharacters += s.Length; 
                        }
                    }
                }

                string thumb = string.Join(", ", ThumbBuilder);

                ThumbBuilder.Clear();
                ThumbSmall.Clear();
                ThumbMedium.Clear();
                ThumbLarge.Clear();

                return thumb;
            }
        }
    }
}
