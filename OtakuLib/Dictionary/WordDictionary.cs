using System;
using System.Collections;
using System.Collections.Generic;

namespace OtakuLib
{
    public class WordDictionary : IReadOnlyList<Word>
    {
        public static string StringMemory { get; set; }
        internal static StringPointer[] StringPointerMemory { get; set; }
        internal static MeaningMemory[] MeaningMemory { get; set; }

        private List<Word> Words;

        internal WordDictionary(List<Word> words)
        {
            Words = words;
            Words.TrimExcess();
        }
        
        public Word this[string hanzi]
        {
            get
            {
                foreach (Word word in Words)
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
                return Words[index];
            }
        }

        public int Count
        {
            get
            {
                return Words.Count;
            }
        }

        public IEnumerator<Word> GetEnumerator()
        {
            return Words.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Words.GetEnumerator();
        }
    }
}
