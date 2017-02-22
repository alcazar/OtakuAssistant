using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtakuLib
{
    internal struct MeaningMemory
    {
        internal readonly StringListMemory Pinyins;
        internal readonly StringListMemory Translations;

        internal MeaningMemory(StringListMemory pinyins, StringListMemory translations)
        {
            Pinyins = pinyins;
            Translations = translations;
        }
    }

    public struct Meaning
    {
        private readonly int PinyinStringStart;
        private readonly int PinyinListStart;
        private readonly int TranslationStringStart;
        private readonly int TranslationListStart;
        private readonly MeaningMemory MeaningMemory;

        public StringList Pinyins { get { return new StringList(PinyinStringStart, PinyinListStart, MeaningMemory.Pinyins.ListLength); } }
        public StringList Translations { get { return new StringList( TranslationStringStart, TranslationListStart, MeaningMemory.Translations.ListLength); } }

        internal Meaning(int pinyinStringStart, int pinyinListStart, int translationStringStart, int translationListStart, MeaningMemory meaningMemory)
        {
            PinyinStringStart = pinyinStringStart;
            PinyinListStart = pinyinListStart;
            TranslationStringStart = translationStringStart;
            TranslationListStart = translationListStart;
            MeaningMemory = meaningMemory;
        }
    }

    internal struct MeaningListMemory
    {
        internal int MeaningStart;
        internal readonly byte MeaningLength;

        internal MeaningListMemory(MeaningListMemoryBuilder builder)
        {
            MeaningStart = builder.MeaningStart;
            MeaningLength = (byte)(builder.MeaningMemory.Count - builder.MeaningStart);
        }
    }

    internal class MeaningListMemoryBuilder
    {
        internal StringListMemoryBuilder PinyinMemory = new StringListMemoryBuilder();
        internal StringListMemoryBuilder TranslationMemory = new StringListMemoryBuilder();
        internal List<MeaningMemory> MeaningMemory = new List<MeaningMemory>();
        internal int MeaningStart = 0;

        internal void Clear()
        {
            PinyinMemory.Clear();
            TranslationMemory.Clear();
            MeaningStart = MeaningMemory.Count;
        }

        internal void Add(StringListMemoryBuilder pinyins, StringListMemoryBuilder translations)
        {
            PinyinMemory.StringMemory.Append(pinyins.StringMemory);
            PinyinMemory.ListMemory.AddRange(pinyins.ListMemory);
            TranslationMemory.StringMemory.Append(translations.StringMemory);
            TranslationMemory.ListMemory.AddRange(translations.ListMemory);
            MeaningMemory.Add(new MeaningMemory(new StringListMemory(pinyins), new StringListMemory(translations)));
        }
    }

    public struct MeaningList : IEnumerable<Meaning>
    {
        private readonly int PinyinStringStart;
        private readonly int PinyinListStart;
        private readonly int TranslationStringStart;
        private readonly int TranslationListStart;
        private readonly MeaningListMemory MeaningListMemory;

        public int Count { get { return MeaningListMemory.MeaningLength; } }

        internal MeaningList(int pinyinStringStart, int pinyinListStart, int translationStringStart, int translationListStart, MeaningListMemory meaningListMemory)
        {
            PinyinStringStart = pinyinStringStart;
            PinyinListStart = pinyinListStart;
            TranslationStringStart = translationStringStart;
            TranslationListStart = translationListStart;
            MeaningListMemory = meaningListMemory;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(PinyinStringStart, PinyinListStart, TranslationStringStart, TranslationListStart, MeaningListMemory);
        }

        IEnumerator<Meaning> IEnumerable<Meaning>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<Meaning>
        {
            private int PinyinStringStart;
            private int PinyinListStart;
            private int TranslationStringStart;
            private int TranslationListStart;
            private MeaningListMemory MeaningListMemory;
            private int MeaningIndex;

            internal Enumerator(int pinyinStringStart, int pinyinListStart, int translationStringStart, int translationListStart, MeaningListMemory meaningListMemory)
            {
                PinyinStringStart = pinyinStringStart;
                PinyinListStart = pinyinListStart;
                TranslationStringStart = translationStringStart;
                TranslationListStart = translationListStart;
                MeaningListMemory = meaningListMemory;
                MeaningIndex = -1;
            }

            public Meaning Current
            {
                get
                {
                    return new Meaning(PinyinStringStart, PinyinListStart, TranslationStringStart, TranslationListStart, WordDictionary.MeaningMemory[MeaningListMemory.MeaningStart + MeaningIndex]);
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (MeaningIndex >= 0)
                {
                    MeaningMemory meaning = WordDictionary.MeaningMemory[MeaningListMemory.MeaningStart + MeaningIndex];
                    PinyinStringStart += meaning.Pinyins.ListStringSize;
                    PinyinListStart += meaning.Pinyins.ListLength;
                    TranslationStringStart += meaning.Translations.ListStringSize;
                    TranslationListStart += meaning.Translations.ListLength;
                }

                ++MeaningIndex;

                return MeaningIndex < MeaningListMemory.MeaningLength;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
