using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OtakuLib
{
    public struct StringPointer : IComparable<StringPointer>, IComparable<string>, IReadOnlyList<char>
    {
        public readonly int Start;
        public readonly ushort Length;
        public readonly ushort ActualLength;

        public static readonly StringPointer Empty = new StringPointer(0, 0, 0);

        public int End { get { return Start + Length; } }

        public string Value
        {
            get { return WordDictionary.StringMemory.Substring(Start, Length); }
        }

        public int Count { get { return Length; } }
        public char this[int index] { get { return WordDictionary.StringMemory[Start + index]; } }

        internal StringPointer(int start, ushort length, ushort actualLength)
        {
            Start = start;
            Length = length;
            ActualLength = actualLength;
        }

        public static implicit operator string(StringPointer pointer)
        {
            return pointer.Value;
        }

        public string Substring(int start, int count)
        {
            return WordDictionary.StringMemory.Substring(Start + start, count);
        }
        
        public string Substring(int start)
        {
            return Substring(start, Length - start);
        }

        public int CompareTo(string other)
        {
            if (Length == other.Length)
            {
                return string.Compare(WordDictionary.StringMemory, Start, other, 0, Length);
            }
            else if (Length < other.Length)
            {
                int diff = string.Compare(WordDictionary.StringMemory, Start, other, 0, Length);
                return diff != 0 ? diff : -1;
            }
            else
            {
                int diff = string.Compare(WordDictionary.StringMemory, Start, other, 0, other.Length);
                return diff != 0 ? diff : 1;
            }
        }

        public int CompareTo(StringPointer other)
        {
            if (Length == other.Length)
            {
                return string.Compare(WordDictionary.StringMemory, Start, WordDictionary.StringMemory, other.Start, Length);
            }
            else if (Length < other.Length)
            {
                int diff = string.Compare(WordDictionary.StringMemory, Start, WordDictionary.StringMemory, other.Start, Length);
                return diff != 0 ? diff : -1;
            }
            else
            {
                int diff = string.Compare(WordDictionary.StringMemory, Start, WordDictionary.StringMemory, other.Start, other.Length);
                return diff != 0 ? diff : 1;
            }
        }

        public override string ToString()
        {
            return Value;
        }

        public IEnumerator<char> GetEnumerator()
        {
            return new Enumerator(Start, End);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(Start, End);
        }

        public struct Enumerator : IEnumerator<char>
        {
            private int CurrentChar;
            private int End;

            internal Enumerator(int start, int end)
            {
                CurrentChar = start - 1;
                End = end;
            }

            public char Current { get { return WordDictionary.StringMemory[CurrentChar]; } }
            object IEnumerator.Current { get { return Current; } }

            public void Dispose() { }

            public bool MoveNext()
            {
                ++CurrentChar;
                return CurrentChar < End;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }

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
    
    public struct StringList : IEnumerable<StringPointer>
    {
        private readonly int ListStart;
        private readonly int ListLength;

        public int Count { get { return ListLength; } }

        internal StringList(int listStart, int listLength)
        {
            ListStart = listStart;
            ListLength = listLength;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ListStart, ListLength);
        }

        IEnumerator<StringPointer> IEnumerable<StringPointer>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<StringPointer>
        {
            private int ListCurrent;
            private int ListEnd;

            internal Enumerator(int listStart, int listLength)
            {
                ListCurrent = listStart - 1;
                ListEnd = listStart + listLength;
            }

            public StringPointer Current
            {
                get
                {
                    return WordDictionary.StringPointerMemory[ListCurrent];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public bool MoveNext()
            {
                ++ListCurrent;
                return ListCurrent < ListEnd;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
            }
        }
    }
}
