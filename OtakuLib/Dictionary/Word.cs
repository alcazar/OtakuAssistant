using System;
using OtakuLib.Builders;
using OtakuLib.Memory;

namespace OtakuLib
{
    public class Word : IComparable<Word>
    {
        internal int WordStart;
        private const byte HanziOffset = 0;
        private const byte TraditionalOffset = 1;
        private const byte ThumbPinyinOffset = 2;
        private const byte ThumbTranslationOffset = 3;
        private const byte RadicalsLength = 4;
        private const byte LinkOffset = 5;
        
        internal int ListStart { get { return WordStart + 6; } }
        internal readonly byte PinyinListLength;
        internal readonly byte TranslationListLength;
        internal readonly byte TagListLength;
        internal MeaningListMemory MeaningsMemory;

        internal int PinyinListOffset          { get { return ListStart; } }
        internal int TranslationListOffset     { get { return PinyinListOffset + PinyinListLength; } }
        internal int TagListOffset             { get { return TranslationListOffset + TranslationListLength; } }
        
        internal int TotalListLength            { get { return PinyinListLength + TranslationListLength + TagListLength; } }

        public StringPointer Hanzi              { get { return WordDictionary.StringPointerMemory[WordStart + HanziOffset]; } }
        public StringPointer Traditional        { get { return WordDictionary.StringPointerMemory[WordStart + TraditionalOffset]; } }
        public StringPointer ThumbPinyin        { get { return WordDictionary.StringPointerMemory[WordStart + ThumbPinyinOffset]; } }
        public StringPointer ThumbTranslation   { get { return WordDictionary.StringPointerMemory[WordStart + ThumbTranslationOffset]; } }
        public StringPointer Radicals           { get { return WordDictionary.StringPointerMemory[WordStart + RadicalsLength]; } }
        public StringPointer Link               { get { return WordDictionary.StringPointerMemory[WordStart + LinkOffset]; } }
        
        public StringList Pinyins               { get { return new StringList(PinyinListOffset, PinyinListLength); } }
        public StringList Translations          { get { return new StringList(TranslationListOffset, TranslationListLength); } }
        public StringList Tags                  { get { return new StringList(TagListOffset, TagListLength); } }
        public MeaningList Meanings             { get { return new MeaningList(PinyinListOffset, TranslationListOffset, MeaningsMemory); } }

        public ulong PinyinMask;

        internal Word(
            StringPointerBuilder stringPointerBuilder,
            string hanzi, string traditional, string thumbPinyin, string thumbTranslation, string radicals, string link,
            MeaningListBuilder meaningBuilder, StringPointerBuilder tagBuilder, ulong pinyinMask)
        {
            WordStart = stringPointerBuilder.StringPointers.Count;

            stringPointerBuilder.Add(hanzi);
            stringPointerBuilder.Add(traditional);
            stringPointerBuilder.Add(thumbPinyin);
            stringPointerBuilder.Add(thumbTranslation);
            stringPointerBuilder.Add(radicals);
            stringPointerBuilder.Add(link);

            stringPointerBuilder.Append(meaningBuilder.Pinyins);
            stringPointerBuilder.Append(meaningBuilder.Translations);
            stringPointerBuilder.Append(tagBuilder);

            PinyinListLength = (byte)meaningBuilder.Pinyins.StringPointers.Count;
            TranslationListLength = (byte)meaningBuilder.Translations.StringPointers.Count;
            TagListLength = (byte)tagBuilder.StringPointers.Count;
            
            MeaningsMemory = new MeaningListMemory(meaningBuilder);

            PinyinMask = pinyinMask;
        }

        public int CompareTo(Word other)
        {
            int diff = Hanzi.CompareTo(other.Hanzi);
            return diff != 0 ? diff : Traditional.CompareTo(other.Traditional);
        }
    }
}
