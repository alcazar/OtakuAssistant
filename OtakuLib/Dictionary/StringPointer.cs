using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtakuLib
{
    public struct StringPointer : IComparable<StringPointer>, IReadOnlyList<char>
    {
        public readonly int Start;
        public readonly int Length;

        public int End { get { return Start + Length; } }
        public int TotalLength { get { return Length; } }

        public string Value
        {
            get { return WordDictionary.StringMemory.Substring(Start, Length); }
        }

        public int Count { get { return Length; } }
        public char this[int index] { get { return WordDictionary.StringMemory[Start + index]; } }

        internal StringPointer(int start, int length)
        {
            Start = start;
            Length = length;
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

    internal struct StringListMemory
    {
        internal readonly ushort ListStringSize;
        internal readonly ushort ListLength;

        internal StringListMemory(StringListMemoryBuilder builder)
        {
            ListStringSize = (ushort)builder.StringMemory.Length;
            ListLength = (ushort)builder.ListMemory.Count;
        }
    }

    internal class StringListMemoryBuilder
    {
        internal StringBuilder StringMemory = new StringBuilder();
        internal List<ushort> ListMemory = new List<ushort>();

        internal void Clear()
        {
            StringMemory.Clear();
            ListMemory.Clear();
        }

        internal void Add(string str)
        {
            StringMemory.Append(str);
            ListMemory.Add((ushort)str.Length);
        }
    }
    
    public struct StringList : IEnumerable<StringPointer>
    {
        private readonly int StringStart;
        private readonly int ListStart;
        private readonly int ListLength;

        public int Count { get { return ListLength; } }

        internal StringList(int stringStart, int listStart, int listLength)
        {
            StringStart = stringStart;
            ListStart = listStart;
            ListLength = listLength;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(StringStart, ListStart, ListLength);
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
            private int StringCurrent;
            private int StringCurrentLength;
            private int ListCurrent;
            private int ListEnd;

            internal Enumerator(int stringStart, int listStart, int listLength)
            {
                StringCurrent = stringStart;
                StringCurrentLength = 0;
                ListCurrent = listStart;
                ListEnd = listStart + listLength;
            }

            public StringPointer Current
            {
                get
                {
                    return new StringPointer(StringCurrent, StringCurrentLength);
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
                if (ListCurrent < ListEnd)
                {
                    StringCurrent += StringCurrentLength;
                    StringCurrentLength = WordDictionary.StringLengthMemory[ListCurrent];
                    ++ListCurrent;
                    return true;
                }

                return false;
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
