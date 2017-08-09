using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OtakuLib
{
    public struct StringPointer : IEquatable<StringPointer>, IEquatable<string>, IComparable<StringPointer>, IComparable<string>, IReadOnlyList<char>
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

        public override bool Equals(object obj)
        {
            return (obj as StringPointer?)?.Equals(this) ?? false;
        }

        public override int GetHashCode()
        {
            return Start | Length | (ActualLength << 16);
        }

        public static bool operator==(StringPointer a, StringPointer b)
        {
            return a.Equals(b);
        }

        public static bool operator!=(StringPointer a, StringPointer b)
        {
            return !a.Equals(b);
        }

        public bool Equals(string other)
        {
            if (Length == other.Length)
            {
                return StringSearch.Search(this, other, SearchFlags.IGNORE_CASE | SearchFlags.IGNORE_DIACRITICS).Found && StringSearch.Search(other, this, SearchFlags.IGNORE_CASE | SearchFlags.IGNORE_DIACRITICS).Found;
                //return string.Compare(WordDictionary.StringMemory, Start, other, 0, Length) == 0;
            }
            else
            {
                return false;
            }
        }

        public bool Equals(StringPointer other)
        {
            if (Length == other.Length)
            {
                return StringSearch.Search(this, other, SearchFlags.IGNORE_CASE | SearchFlags.IGNORE_DIACRITICS).Found && StringSearch.Search(other, this, SearchFlags.IGNORE_CASE | SearchFlags.IGNORE_DIACRITICS).Found;
                //return string.Compare(WordDictionary.StringMemory, Start, WordDictionary.StringMemory, other.Start, Length) == 0;
            }
            else
            {
                return false;
            }
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
    
    public struct StringList : IEnumerable<StringPointer>
    {
        private readonly int ListStart;
        private readonly int ListLength;

        public int Count { get { return ListLength; } }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StringList(int listStart, int listLength)
        {
            ListStart = listStart;
            ListLength = listLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(ListStart, ListLength);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<StringPointer> IEnumerable<StringPointer>.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<StringPointer>
        {
            private int ListCurrent;
            private int ListEnd;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(int listStart, int listLength)
            {
                ListCurrent = listStart - 1;
                ListEnd = listStart + listLength;
            }

            public StringPointer Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return WordDictionary.StringPointerMemory[ListCurrent];
                }
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Current;
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
