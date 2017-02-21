using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    public static class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBlank(this char c)
        {
            return c <= '.';
        }

        public static string RemoveAccents(this string str)
        {
            byte[] temp = Encoding.GetEncoding("ISO-8859-8").GetBytes(str);
            return Encoding.UTF8.GetString(temp, 0, temp.Length).Replace("?", "").Replace("`", "");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChinese(this char c)
        {
            if (0x2E80 <= c && c <= 0x303F)
            {
                // CJK Radicals Supplement              02E80 - 02EFF
                // Kangxi Radical                       02F00 - 02FDF
                // CJK Symbols and Punctuation          03000 - 0303F
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChinese(this StringPointer str)
        {
            for (int i = str.Start; i < str.End; ++i)
            {
                char c = WordDictionary.StringMemory[i];
                if (!c.IsChinese())
                {
                    return false;
                }
            }
            return true;
        }

        private const string Initials = @"[bpmfdtnlgkhjqxzcsr]|zh|ch|sh";
        private const string StandaloneFinals = @"a|o|e|ai|ei|ao|ou|an|en|ang|eng|ong";
        private const string Finals = StandaloneFinals + @"
            |i|ia|ie|iao|iu|ian|in|iang|ing|iong
            |u|ua|uo|uai|ui|uan|un|uang|ueng
            |u|ue|uan|un";
        private const string Standalone = StandaloneFinals + @"
            |yi|ya|ye|yao|you|yan|yin|yang|ying|yong
            |wu|wa|wo|wai|wei|wan|wen|wang|weng
            |yu|yue|yuan|yun
            ";
        private static readonly string PinyinRegex = string.Format(@"(({0})({1})|{2})r?", Initials, Finals, Standalone).Replace(" ", "").Replace("\r", "").Replace("\n", "");

        private static readonly Regex PinyinMatchRegex = new Regex(
            string.Format(@"^(?<1>{0})+$", PinyinRegex),
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPinyin(this string str)
        {
            return PinyinMatchRegex.IsMatch(str);
        }

        public static string[] SplitPinyins(this string str)
        {
            Match match = PinyinMatchRegex.Match(str);

            string[] pinyins = new string[match.Groups[1].Captures.Count];
            int i = 0;
            foreach (Capture capture in match.Groups[1].Captures)
            {
                pinyins[i++] = capture.Value;
            }
            return pinyins;
        }
    }
}