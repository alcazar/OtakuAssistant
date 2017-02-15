using System;
using System.Collections.Generic;

namespace OtakuLib
{
    public struct Meaning
    {
        public Str[] Pinyins            { get; set; }
        public Str[] Translations       { get; set; }
    }

    public class Word : IComparable<Word>
    {
        public string Hanzi             { get; set; }
        public string Traditional       { get; set; }
        public string Radicals          { get; set; }
        public string Link              { get; set; }
        public string ThumbPinyin       { get; set; }
        public string ThumbTranslation  { get; set; }
        public Meaning[] Meanings       { get; set; }
        public string[] Tags            { get; set; }

        public Word()
        {
            Hanzi = null;
            Traditional = null;
            Radicals = null;
            Link = null;
            ThumbPinyin = null;
            ThumbTranslation = null;
            Meanings = null;
            Tags = null;
        }

        /// to be used as BinarySearch parameter
        public Word(string hanzi)
            : this()
        {
            Hanzi = hanzi;
        }

        private static List<Word> WordsBuilder = new List<Word>();
        
        /// it's impratical to build similar words for every character during loading
        /// instead, build similar words on demand
        public Word[] GetSimilarWords(WordDictionary wordList)
        {
            WordsBuilder.Clear();

            foreach (Word word in wordList)
            {
                if (word != this)
                {
                    bool in1 = Hanzi.Search(word.Hanzi) > 0;
                    bool in2 = word.Hanzi.Search(Hanzi) > 0;

                    if (in1 || in2)
                    {
                        WordsBuilder.Add(word);
                    }
                }
            }

            WordsBuilder.Sort();
            Word[] similarWords = WordsBuilder.ToArray();
            WordsBuilder.Clear();
            WordsBuilder.TrimExcess();

            return similarWords;
        }

        public int CompareTo(Word other)
        {
            return Hanzi.CompareTo(other.Hanzi);
        }
    }
}
