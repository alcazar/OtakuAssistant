using System.Globalization;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    public struct Str
    {
        public string Value { get; set; }

        public Str(string value)
        {
            Value = value;
        }

        public static implicit operator string(Str str)
        {
            return str.Value;
        }

        public static implicit operator Str(string str)
        {
            return new Str(str);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public static class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Search(this string str, string substr)
        {
            return str.Search(substr, CompareOptions.IgnoreCase |  CompareOptions.IgnoreNonSpace);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Search(this string str, string substr, CompareOptions compareOptions)
        {
            if (substr.Length * 3 < str.Length)
            {
                return 0;
            }

            int index = CultureInfo.CurrentCulture.CompareInfo.IndexOf(str, substr, compareOptions);
            
            if (index < 0)
            {
                return 0;
            }
            else
            {
                bool wordStart = index == 0 || str[index - 1] == ' ';
                bool wordEnd   = (index + substr.Length) == str.Length || str[index + substr.Length] == ' ';

                float relevance = (float)substr.Length/str.Length;
                relevance += wordStart ? 0.3f : 0;
                relevance += wordEnd ? 0.15f : 0;

                return relevance;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChinese(this char c)
        {
            if (0x2F00 <= c && c <= 0x2FFF)
            {
                // Kangxi Radical                       02F00 - 02FDF
                return true;
            }
            else if (0x3400 <= c && c <= 0x9FFF)
            {
                // CJK Unified Ideographs Extension A   03400 – 04DBF
                // CJK Unified Ideograph                04E00 - 09FFF
                return true;
            }
            else if (0xF900 <= c && c <= 0xFAFF)
            {
                // CJK Compatibility Ideograph          0F900 - 0FAFF
                return true;
            }
            else if (0x20000 <= c && c <= 0x2CEAF)
            {
                // CJK Unified Ideographs Extension B   20000 - 2A6DF
                // CJK Unified Ideographs Extension C   2A700 – 2B73F
                // CJK Unified Ideographs Extension D   2B740 – 2B81F
                // CJK Unified Ideographs Extension E   2B820 – 2CEAF
                return true;
            }

            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChinese(this string str)
        {
            foreach (char c in str)
            {
                if (!c.IsChinese())
                {
                    return false;
                }
            }
            return true;
        }
    }
}