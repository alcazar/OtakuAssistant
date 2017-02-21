using System;
using System.Collections;
using System.Collections.Generic;

namespace OtakuLib
{
    public class WordDictionary : IReadOnlyList<Word>
    {
        public static string StringMemory { get; set; }
        internal static ushort[] StringLengthMemory { get; set; }
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
                return null;
                //int index = Words.BinarySearch(new Word(hanzi));
                //return index >= 0 ? Words[index] : null;
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
