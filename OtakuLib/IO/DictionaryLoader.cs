using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OtakuLib.Builders;
using OtakuLib.Memory;

namespace OtakuLib
{
    public abstract class DictionaryLoader
    {
        public string DictionaryName { get; private set; }

        public Task LoadTask { get; private set; }
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
            
            WordDictionary.Loading.IsCompleted = false;
            WordDictionary.Loading.DictionaryLoadedNotifier.Reset();
            
            LoadTaskCanceller = new CancellationTokenSource();
            LoadTask = Task.Run(LoadDictionaryTask, LoadTaskCanceller.Token);

            Debug.WriteLine("Loading dictionary " + dictionaryName);
        }

        protected abstract Task<List<DictionaryLoadJob>> GetDictionaryLoadJobs();

        protected virtual void SortDictionaryWords()
        {
            // sort
            WordDictionary._Words.Sort();

            // repack everything...
            StringPointerBuilder stringBuilder = new StringPointerBuilder();

            MeaningListBuilder meaningBuilder = new MeaningListBuilder();
            StringPointerBuilder pinyinBuilder = new StringPointerBuilder();
            StringPointerBuilder translationBuilder = new StringPointerBuilder();
            StringPointerBuilder tagBuilder = new StringPointerBuilder();

            List<Word> newWords = new List<Word>();

            foreach (Word word in WordDictionary.Words)
            {
                foreach (Meaning meaning in word.Meanings)
                {
                    foreach (StringPointer pinyin in meaning.Pinyins)
                    {
                        pinyinBuilder.Add(pinyin);
                    }

                    foreach (StringPointer translation in meaning.Translations)
                    {
                        translationBuilder.Add(translation);
                    }

                    meaningBuilder.Add(pinyinBuilder, translationBuilder);

                    pinyinBuilder.Clear();
                    translationBuilder.Clear();
                }

                foreach (StringPointer tag in word.Tags)
                {
                    tagBuilder.Add(tag);
                }

                newWords.Add(new Word(stringBuilder,
                    word.Hanzi, word.Traditional, word.ThumbPinyin, word.ThumbTranslation, word.Radicals, word.Link, meaningBuilder, tagBuilder, word.PinyinMask));

                meaningBuilder.Clear();
                tagBuilder.Clear();
            }

            WordDictionary.SetDictionary(
                DictionaryName,
                newWords,
                stringBuilder.StringBuilder.ToString(),
                stringBuilder.StringPointers.ToArray(),
                meaningBuilder.MeaningMemory.ToArray());
        }

        protected struct IndexedWordOccurrence : IEquatable<IndexedWordOccurrence>, IComparable<IndexedWordOccurrence>
        {
            public readonly string IndexedWord;
            public readonly int WordIndex;

            public IndexedWordOccurrence(string indexedWord, int wordIndex)
            {
                IndexedWord = indexedWord;
                WordIndex = wordIndex;
            }

            public override bool Equals(object obj)
            {
                return (obj as IndexedWordOccurrence?)?.Equals(this) ?? false;
            }

            public override int GetHashCode()
            {
                return IndexedWord.GetHashCode() | WordIndex;
            }

            public static bool operator ==(IndexedWordOccurrence a, IndexedWordOccurrence b)
            {
                return a.Equals(b);
            }

            public static bool operator !=(IndexedWordOccurrence a, IndexedWordOccurrence b)
            {
                return !a.Equals(b);
            }

            public bool Equals(IndexedWordOccurrence other)
            {
                return WordIndex == other.WordIndex && IndexedWord.Equals(other.IndexedWord);
            }

            public int CompareTo(IndexedWordOccurrence other)
            {
                int result = IndexedWord.CompareTo(other.IndexedWord);

                if (result != 0)
                {
                    return result;
                }
                else
                {
                    return WordIndex - other.WordIndex;
                }
            }
        }
        
        protected virtual async Task BuildIndex()
        {
            List<IndexedWordOccurrence> indexedWordOccurences = new List<IndexedWordOccurrence>();
            List<StringPointer> _indexedWordOccurences = new List<StringPointer>();

            for (int i = 0; i < WordDictionary.Words.Count; ++i)
            {
                Word word = WordDictionary.Words[i];

                foreach (Meaning meaning in word.Meanings)
                {
                    foreach (StringPointer translation in meaning.Translations)
                    {
                        _indexedWordOccurences.Clear();
                        WordDictionary.StringMemory.SplitWords(translation.Start, translation.Length, _indexedWordOccurences);

                        foreach (StringPointer occurrence in _indexedWordOccurences)
                        {
                            string soccurrence = occurrence.ToString();
                            soccurrence = soccurrence.ToLower().RemoveAccents();
                            indexedWordOccurences.Add(new IndexedWordOccurrence(soccurrence, i));
                        }
                    }
                }
            }
            indexedWordOccurences.Sort();

            StringBuilder sbuilder = new StringBuilder();
            sbuilder.Append(WordDictionary.StringMemory);

            List<IndexedWord> indexedWords = new List<IndexedWord>();
            List<int> indexedWordMatches = new List<int>();

            int wordMatchCount = 0;
            IndexedWordOccurrence lastOccurence = new IndexedWordOccurrence(new StringPointer(0, 0, 0), 0);
            foreach (IndexedWordOccurrence indexedWord in indexedWordOccurences)
            {
                if (indexedWord.IndexedWord == lastOccurence.IndexedWord)
                {
                    if (indexedWord.WordIndex != lastOccurence.WordIndex)
                    {
                        indexedWordMatches.Add(indexedWord.WordIndex);
                        ++wordMatchCount;
                    }
                }
                else
                {
                    if (lastOccurence.IndexedWord.Length > 0)
                    {
                        int start = sbuilder.Length;
                        sbuilder.Append(lastOccurence.IndexedWord);
                        indexedWords.Add(new IndexedWord(new StringPointer(start, (ushort)lastOccurence.IndexedWord.Length, (ushort)lastOccurence.IndexedWord.ActualLength()), lastOccurence.IndexedWord.LetterMask(), wordMatchCount));
                    }

                    wordMatchCount = 1;
                    indexedWordMatches.Add(indexedWord.WordIndex);
                }

                lastOccurence = indexedWord;
            }

            if (lastOccurence.IndexedWord.Length > 0)
            {
                int start = sbuilder.Length;
                sbuilder.Append(lastOccurence.IndexedWord);
                indexedWords.Add(new IndexedWord(new StringPointer(start, (ushort)lastOccurence.IndexedWord.Length, (ushort)lastOccurence.IndexedWord.ActualLength()), lastOccurence.IndexedWord.LetterMask(), wordMatchCount));
            }

            WordDictionary.StringMemory = sbuilder.ToString();
            WordDictionary.IndexedWords = indexedWords.ToArray();
            WordDictionary.IndexedWordMatches = indexedWordMatches.ToArray();

            // suppress compiler warning
            await Task.Yield();
        }

        private async Task LoadDictionaryTask()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // spawn jobs
            List<DictionaryLoadJob> dictionaryLoadJobs = await GetDictionaryLoadJobs();
            List<Task> jobTasks = new List<Task>();
            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                jobTasks.Add(Task.Run((Action)dictionaryLoadJob.LoadDictionaryPart, LoadTaskCanceller.Token));
            }

            // wait for all jobs to complete
            Task.WaitAll(jobTasks.ToArray(), LoadTaskCanceller.Token);
            
            // merge results
            StringPointerBuilder stringBuilder = new StringPointerBuilder();
            List<MeaningMemory> meaningMemoryBuilder = new List<MeaningMemory>();

            List<Word> words = new List<Word>();

            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                foreach (Word word in dictionaryLoadJob.Words)
                {
                    word.WordStart += stringBuilder.StringPointers.Count;
                    word.MeaningsMemory.MeaningStart += meaningMemoryBuilder.Count;
                }
                words.AddRange(dictionaryLoadJob.Words);

                stringBuilder.Append(dictionaryLoadJob.StringBuilder);
                meaningMemoryBuilder.AddRange(dictionaryLoadJob.MeaningBuilder.MeaningMemory);
            }

            WordDictionary.SetDictionary(
                DictionaryName,
                words,
                stringBuilder.StringBuilder.ToString(),
                stringBuilder.StringPointers.ToArray(),
                meaningMemoryBuilder.ToArray());

            foreach (DictionaryLoadJob dictionaryLoadJob in dictionaryLoadJobs)
            {
                dictionaryLoadJob.Dispose();
            }

            SortDictionaryWords();
            await BuildIndex();

            stopwatch.Stop();
            Debug.WriteLine("Loaded dictionary {1} in 0.{0:fffffff}", stopwatch.Elapsed, DictionaryName);
            
            WordDictionary.Loading.IsCompleted = true;
            WordDictionary.Loading.DictionaryLoadedNotifier.Set();
        }

        // helper class for dictionary loading job
        protected abstract class DictionaryLoadJob : IDisposable
        {
            protected Stream LoadStream;
            protected CancellationToken JobCancellationToken;
            protected bool BuildThumbs;
            
            internal List<Word> Words = new List<Word>();
            internal StringPointerBuilder StringBuilder = new StringPointerBuilder();
            internal MeaningListBuilder MeaningBuilder = new MeaningListBuilder();

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
