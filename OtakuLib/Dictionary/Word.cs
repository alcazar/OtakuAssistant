using System;
using System.Text;
using System.Collections.Generic;

namespace OtakuLib
{
    public class Word : IComparable<Word>
    {
        internal int StringStart;
        internal int ListStart;

        internal readonly byte HanziLength;
        internal readonly byte TraditionalLength;
        internal readonly byte ThumbPinyinLength;
        internal readonly byte ThumbTranslationLength;

        internal readonly StringListMemory PinyinsMemory;
        internal readonly StringListMemory TranslationsMemory;
        internal readonly StringListMemory TagsMemory;
        internal MeaningListMemory MeaningsMemory;

        internal readonly byte RadicalsLength;
        internal readonly byte LinkLength;

        internal int HanziOffset                { get { return StringStart; } }
        internal int TraditionalOffset          { get { return HanziOffset + HanziLength; } }
        internal int ThumbPinyinOffset          { get { return TraditionalOffset + TraditionalLength; } }
        internal int ThumbTranslationOffset     { get { return ThumbPinyinOffset + ThumbPinyinLength; } }

        internal int PinyinsOffset              { get { return ThumbTranslationOffset + ThumbTranslationLength; } }
        internal int TranslationsOffset         { get { return PinyinsOffset + PinyinsMemory.ListStringSize; } }
        internal int TagsOffset                 { get { return TranslationsOffset + TranslationsMemory.ListStringSize; } }

        internal int RadicalsOffset             { get { return TagsOffset + TagsMemory.ListStringSize; } }
        internal int LinkOffset                 { get { return RadicalsOffset + RadicalsLength; } }

        internal int TotalStringLength          { get { return LinkOffset + LinkLength - StringStart; } }

        internal int PinyinsListOffset          { get { return ListStart; } }
        internal int TranslationsListOffset     { get { return PinyinsListOffset + PinyinsMemory.ListLength; } }
        internal int TagsListOffset             { get { return TranslationsListOffset + TranslationsMemory.ListLength; } }
        
        internal int TotalListLength            { get { return TagsListOffset + TagsMemory.ListLength - ListStart; } }

        public StringPointer Hanzi              { get { return new StringPointer(HanziOffset,               HanziLength); } }
        public StringPointer Traditional        { get { return new StringPointer(TraditionalOffset,         TraditionalLength); } }
        public StringPointer ThumbPinyin        { get { return new StringPointer(ThumbPinyinOffset,         ThumbPinyinLength); } }
        public StringPointer ThumbTranslation   { get { return new StringPointer(ThumbTranslationOffset,    ThumbTranslationLength); } }
        
        public StringList Pinyins               { get { return new StringList(PinyinsOffset, PinyinsListOffset, PinyinsMemory.ListLength); } }
        public StringList Translations          { get { return new StringList(TranslationsOffset, TranslationsListOffset, TranslationsMemory.ListLength); } }
        public StringList Tags                  { get { return new StringList(TagsOffset, TagsListOffset, TagsMemory.ListLength); } }
        public MeaningList Meanings             { get { return new MeaningList(PinyinsOffset, PinyinsListOffset, TranslationsOffset, TranslationsListOffset, MeaningsMemory); } }

        public StringPointer Radicals           { get { return new StringPointer(RadicalsOffset, RadicalsLength); } }
        public StringPointer Link               { get { return new StringPointer(LinkOffset, LinkLength); } }

        internal Word(
            StringBuilder stringMemoryBuilder, List<ushort> listMemoryBuilder,
            string hanzi, string traditional, string thumbPinyin, string thumbTranslation, string radicals, string link,
            MeaningListMemoryBuilder meaningsListMemoryBuilder, StringListMemoryBuilder tagsListMemoryBuilder)
        {
            StringStart = stringMemoryBuilder.Length;
            ListStart = listMemoryBuilder.Count;

            stringMemoryBuilder.Append(hanzi);
            stringMemoryBuilder.Append(traditional);
            stringMemoryBuilder.Append(thumbPinyin);
            stringMemoryBuilder.Append(thumbTranslation);

            stringMemoryBuilder.Append(meaningsListMemoryBuilder.PinyinMemory.StringMemory);
            listMemoryBuilder.AddRange(meaningsListMemoryBuilder.PinyinMemory.ListMemory);
            stringMemoryBuilder.Append(meaningsListMemoryBuilder.TranslationMemory.StringMemory);
            listMemoryBuilder.AddRange(meaningsListMemoryBuilder.TranslationMemory.ListMemory);

            stringMemoryBuilder.Append(tagsListMemoryBuilder.StringMemory);
            listMemoryBuilder.AddRange(tagsListMemoryBuilder.ListMemory);

            stringMemoryBuilder.Append(radicals);
            stringMemoryBuilder.Append(link);

            HanziLength = (byte)hanzi.Length;
            TraditionalLength = (byte)traditional.Length;
            ThumbPinyinLength = (byte)thumbPinyin.Length;
            ThumbTranslationLength = (byte)thumbTranslation.Length;

            PinyinsMemory = new StringListMemory(meaningsListMemoryBuilder.PinyinMemory);
            TranslationsMemory = new StringListMemory(meaningsListMemoryBuilder.TranslationMemory);
            MeaningsMemory = new MeaningListMemory(meaningsListMemoryBuilder);
            TagsMemory = new StringListMemory(tagsListMemoryBuilder);

            RadicalsLength = (byte)radicals.Length;
            LinkLength = (byte)link.Length;
        }

        public int CompareTo(Word other)
        {
            int diff = Hanzi.CompareTo(other.Hanzi);
            return diff != 0 ? diff : Traditional.CompareTo(other.Traditional);
        }
    }
}
