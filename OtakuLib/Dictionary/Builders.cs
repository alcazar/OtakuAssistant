using System.Collections.Generic;
using System.Text;
using OtakuLib.Memory;

namespace OtakuLib.Builders
{
    internal class StringPointerBuilder
    {
        internal StringBuilder StringBuilder = new StringBuilder();
        internal List<StringPointer> StringPointers = new List<StringPointer>();
        
        internal void Clear()
        {
            StringBuilder.Clear();
            StringPointers.Clear();
        }

        internal void Add(string str)
        {
            StringPointers.Add(new StringPointer(StringBuilder.Length, (ushort)str.Length, (ushort)str.ActualLength()));
            StringBuilder.Append(str);
        }

        internal void Append(StringPointerBuilder stringList)
        {
            int stringOffset = StringBuilder.Length;

            StringBuilder.Append(stringList.StringBuilder);

            foreach (StringPointer stringPointer in stringList.StringPointers)
            {
                StringPointers.Add(new StringPointer(stringPointer.Start + stringOffset, stringPointer.Length, stringPointer.ActualLength));
            }
        }
    }

    internal class MeaningListBuilder
    {
        internal StringPointerBuilder Pinyins = new StringPointerBuilder();
        internal StringPointerBuilder Translations = new StringPointerBuilder();
        internal List<MeaningMemory> MeaningMemory = new List<MeaningMemory>();
        internal int MeaningStart = 0;

        internal void Clear()
        {
            Pinyins.Clear();
            Translations.Clear();
            MeaningStart = MeaningMemory.Count;
        }

        internal void Add(StringPointerBuilder pinyins, StringPointerBuilder translations)
        {
            Pinyins.Append(pinyins);
            Translations.Append(translations);
            MeaningMemory.Add(new MeaningMemory(pinyins, translations));
        }
    }
}
