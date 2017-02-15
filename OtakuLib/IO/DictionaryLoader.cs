using System;
using System.Collections.Generic;
using System.Xml;
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
            List<Task<List<Word>>> jobTasks = new List<Task<List<Word>>>();
            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                jobTasks.Add(Task.Run((Func<List<Word>>)dictionaryLoadJob.LoadDictionaryPart, LoadTaskCanceller.Token));
            }

            // wait for all jobs to complete
            Task.WaitAll(jobTasks.ToArray(), LoadTaskCanceller.Token);
            
            // merge results
            List<Word> words = new List<Word>();
            foreach (Task<List<Word>> dictionaryLoadJob in jobTasks)
            {
                words.AddRange(dictionaryLoadJob.Result);
                dictionaryLoadJob.Result.Clear();
                dictionaryLoadJob.Result.TrimExcess();
            }

            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                dictionaryLoadJob.Dispose();
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

            public DictionaryLoadJob(Stream stream, CancellationToken cancellationToken, bool buildThumbs)
            {
                LoadStream = stream;
                JobCancellationToken = cancellationToken;
                BuildThumbs = buildThumbs;
            }

            public void Dispose()
            {
                LoadStream.Dispose();
            }

            public abstract List<Word> LoadDictionaryPart();
            
            private List<string> ThumbBuilder = new List<string>();
            private List<string> ThumbSmall = new List<string>();
            private List<string> ThumbMedium = new List<string>();
            private List<string> ThumbLarge = new List<string>();

            protected string BuildThumb(IEnumerable<Str> list)
            {
                if (!BuildThumbs)
                {
                    return null;
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
