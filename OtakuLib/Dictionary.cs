using System;
using System.Collections;
using System.Collections.Generic;

namespace OtakuLib
{
    public class WordDictionary : IReadOnlyList<Word>
    {
        private List<Word> Words;

        public WordDictionary(List<Word> words)
        {
            words.TrimExcess();
            words.Sort();
            Words = words;
        }
        
        public Word this[string hanzi]
        {
            get
            {
                int index = Words.BinarySearch(new Word(hanzi));
                return index >= 0 ? Words[index] : null;
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
