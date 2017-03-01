using OtakuLib.Builders;

namespace OtakuLib.Memory
{
    internal struct MeaningMemory
    {
        internal readonly byte PinyinCount;
        internal readonly byte TranslationCount;

        internal MeaningMemory(StringPointerBuilder pinyins, StringPointerBuilder translations)
        {
            PinyinCount = (byte)pinyins.StringPointers.Count;
            TranslationCount = (byte)translations.StringPointers.Count;
        }
    }

    internal struct MeaningListMemory
    {
        internal int MeaningStart;
        internal readonly byte MeaningLength;

        internal MeaningListMemory(MeaningListBuilder builder)
        {
            MeaningStart = builder.MeaningStart;
            MeaningLength = (byte)(builder.MeaningMemory.Count - builder.MeaningStart);
        }
    }
}
