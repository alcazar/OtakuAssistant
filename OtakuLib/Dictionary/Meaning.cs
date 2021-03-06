﻿using System;
using System.Collections;
using System.Collections.Generic;
using OtakuLib.Memory;

namespace OtakuLib
{
    public struct Meaning
    {
        private readonly int PinyinListStart;
        private readonly int TranslationListStart;
        private readonly MeaningMemory MeaningMemory;

        public StringList Pinyins { get { return new StringList(PinyinListStart, MeaningMemory.PinyinCount); } }
        public StringList Translations { get { return new StringList(TranslationListStart, MeaningMemory.TranslationCount); } }

        internal Meaning(int pinyinListStart, int translationListStart, MeaningMemory meaningMemory)
        {
            PinyinListStart = pinyinListStart;
            TranslationListStart = translationListStart;
            MeaningMemory = meaningMemory;
        }
    }

    public struct MeaningList : IEnumerable<Meaning>
    {
        private readonly int PinyinListStart;
        private readonly int TranslationListStart;
        private readonly MeaningListMemory MeaningListMemory;

        public int Count { get { return MeaningListMemory.MeaningLength; } }

        internal MeaningList(int pinyinListStart, int translationListStart, MeaningListMemory meaningListMemory)
        {
            PinyinListStart = pinyinListStart;
            TranslationListStart = translationListStart;
            MeaningListMemory = meaningListMemory;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(PinyinListStart, TranslationListStart, MeaningListMemory);
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
            private int PinyinListStart;
            private int TranslationListStart;
            private MeaningListMemory MeaningListMemory;
            private int MeaningIndex;

            internal Enumerator(int pinyinListStart, int translationListStart, MeaningListMemory meaningListMemory)
            {
                PinyinListStart = pinyinListStart;
                TranslationListStart = translationListStart;
                MeaningListMemory = meaningListMemory;
                MeaningIndex = -1;
            }

            public Meaning Current
            {
                get
                {
                    return new Meaning(PinyinListStart, TranslationListStart, WordDictionary.MeaningMemory[MeaningListMemory.MeaningStart + MeaningIndex]);
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
                    PinyinListStart += meaning.PinyinCount;
                    TranslationListStart += meaning.TranslationCount;
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
