using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace OtakuLib
{
    public class WordDictionary : IEnumerable<Word>
    {
        public static string CurrentDictionary;
        public static WordDictionary Words = new WordDictionary();

        public struct LoadingTask : IAsyncResult
        {
            public object AsyncState { get { return null; } }

            internal ManualResetEvent DictionaryLoadedNotifier;

            public WaitHandle AsyncWaitHandle { get { return DictionaryLoadedNotifier; } }

            public bool CompletedSynchronously { get { return false; } }

            public bool IsCompleted { get; internal set; }
        }
        public static LoadingTask Loading;
        
        public static string StringMemory { get; private set; }
        internal static StringPointer[] StringPointerMemory { get; private set; }
        internal static MeaningMemory[] MeaningMemory { get; private set; }

        private static List<Word> _Words;

        static WordDictionary()
        {
            Loading.DictionaryLoadedNotifier = new ManualResetEvent(false);
        }

        internal static void SetDictionary(string dictionary, List<Word> words, string stringMemory, StringPointer[] stringPointerMemory, MeaningMemory[] meaningMemory)
        {
            _Words = words;
            _Words.TrimExcess();

            CurrentDictionary = dictionary;
            StringMemory = stringMemory;
            StringPointerMemory = stringPointerMemory;
            MeaningMemory = meaningMemory;
        }
        
        public Word this[string hanzi]
        {
            get
            {
                foreach (Word word in _Words)
                {
                    if (word.Hanzi.CompareTo(hanzi) == 0 || word.Traditional.CompareTo(hanzi) == 0)
                    {
                        return word;
                    }
                }
                return null;
            }
        }

        public Word this[int index]
        {
            get
            {
                return _Words[index];
            }
        }

        public int Count
        {
            get
            {
                return _Words.Count;
            }
        }

        public List<Word>.Enumerator GetEnumerator()
        {
            return _Words.GetEnumerator();
        }

        IEnumerator<Word> IEnumerable<Word>.GetEnumerator()
        {
            return _Words.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Words.GetEnumerator();
        }
    }
}
