using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace robinhood
{
    public class RobinHoodDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private const float LOAD_FACTOR = 0.9f;
        private const int SAFE_HASH = 0x40000000;

        private readonly IEqualityComparer<TKey> keyComparer;

        private Entry[] buckets;
        private int count;
        private int countMod;
        private int countUsed;
        private int growAt;
        private int shrinkAt;

        public RobinHoodDictionary(int size, IEqualityComparer<TKey> comparer = null) : this(comparer)
        {
            Resize(NextPow2(size));
        }

        public RobinHoodDictionary(IEqualityComparer<TKey> comparer = null)
        {
            keyComparer = comparer ?? EqualityComparer<TKey>.Default;
            Clear();
        }

        private IEnumerable<KeyValuePair<TKey, TValue>> Entries
        {
            get
            {
                for (var i = 0; i < count; i++)
                    if (buckets[i].hash != 0)
                        yield return new KeyValuePair<TKey, TValue>(buckets[i].key, buckets[i].value);
            }
        }

        private void Resize(int newSize, bool auto = true)
        {
#if DEBUG
            if (newSize != 0)
            {
                Debug.Assert((count != newSize) && (countUsed <= newSize));
                Debug.Assert(NextPow2(newSize) == newSize);
            }
#endif
            var oldCount = count;
            var oldBuckets = buckets;

            count = newSize;
            countMod = newSize - 1;
            buckets = new Entry[newSize];

            growAt = auto ? (int)(newSize *LOAD_FACTOR) : newSize;
            shrinkAt = auto ? newSize >> 2 : 0;

            if ((countUsed > 0) && (newSize != 0))
            {
                Debug.Assert(countUsed <= newSize);
                Debug.Assert(oldBuckets != null);

                countUsed = 0;

                for (var i = 0; i < oldCount; i++)
                    if (oldBuckets[i].hash != 0)
                        PutInternal(oldBuckets[i], false, false);
            }
        }

        private bool Get(TKey key, out TValue value)
        {
            int index;
            if (Find(key, out index))
            {
                value = buckets[index].value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        private bool Put(TKey key, TValue val, bool canReplace)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (countUsed == growAt)
                ResizeNext();

            return PutInternal(new Entry(GetHash(key), key, val), canReplace, true);
        }

        private bool PutInternal(Entry entry, bool canReplace, bool checkDuplicates)
        {
            var
                indexInit = entry.hash & countMod;
            var
                probeCurrent = 0;

            for (var i = 0; i < count; i++)
            {
                var
                    indexCurrent = (indexInit + i) & countMod;
                if (buckets[indexCurrent].hash == 0)
                {
                    countUsed++;
                    buckets[indexCurrent] = entry;
                    return true;
                }

                if (checkDuplicates && (entry.hash == buckets[indexCurrent].hash) &&
                    keyComparer.Equals(entry.key, buckets[indexCurrent].key))
                {
                    if (!canReplace)
                        throw new ArgumentException("An entry with the same key already exists", nameof(entry.key));

                    buckets[indexCurrent] = entry;
                    return true;
                }

                var
                    probeDistance = DistanceToInitIndex(indexCurrent);
                if (probeCurrent > probeDistance)
                {
                    probeCurrent = probeDistance;
                    Swap(ref buckets[indexCurrent], ref entry);
                }
                probeCurrent++;
            }

            return false;
        }

        private bool Find(TKey key, out int index)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            index = 0;
            if (countUsed > 0)
            {
                var
                    hash = GetHash(key);
                var
                    indexInit = hash & countMod;
                var
                    probeDistance = 0;

                for (var i = 0; i < count; i++)
                {
                    index = (indexInit + i) & countMod;

                    if ((hash == buckets[index].hash) && keyComparer.Equals(key, buckets[index].key))
                        return true;

                    if (buckets[index].hash != 0)
                        probeDistance = DistanceToInitIndex(index);

                    if (i > probeDistance)
                        break;
                }
            }

            return false;
        }

        private bool RemoveInternal(TKey key)
        {
            int index;
            if (Find(key, out index))
            {
                for (var i = 0; i < count; i++)
                {
                    var curIndex = (index + i) & countMod;
                    var nextIndex = (index + i + 1) & countMod;

                    if ((buckets[nextIndex].hash == 0) || (DistanceToInitIndex(nextIndex) == 0))
                    {
                        buckets[curIndex] = default(Entry);

                        if (--countUsed == shrinkAt)
                            Resize(shrinkAt);

                        return true;
                    }

                    Swap(ref buckets[curIndex], ref buckets[nextIndex]);
                }
            }

            return false;
        }

        private int DistanceToInitIndex(int indexStored)
        {
            Debug.Assert(buckets[indexStored].hash != 0);

            var indexInit = buckets[indexStored].hash & countMod;
            if (indexInit <= indexStored)
                return indexStored - indexInit;
            return indexStored + (count - indexInit);
        }

        private void ResizeNext()
        {
            Resize(count == 0 ? 1 : count*2);
        }

        private int GetHash(TKey o)
        {
            var h = o.GetHashCode();
            if (h == 0)
                return SAFE_HASH;

            //JDK bit spread, to ensure we have
            //a fair loword distribution
            return h ^ (h >> 16);
        }

        private struct Entry
        {
            public Entry(int hash, TKey key, TValue value)
            {
                this.hash = hash;
                this.key = key;
                this.value = value;
            }

            public readonly int hash;
            public readonly TKey key;
            public readonly TValue value;
        }

        #region Statics

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NextPow2(int c)
        {
            c--;
            c |= c >> 1;
            c |= c >> 2;
            c |= c >> 4;
            c |= c >> 8;
            c |= c >> 16;
            return ++c;
        }

        private static void Swap<T>(ref T first, ref T second)
        {
            var temp = first;
            first = second;
            second = temp;
        }

        #endregion

        #region IDictionary

        public void Add(TKey key, TValue value)
        {
            Put(key, value, false);
        }

        public bool ContainsKey(TKey key)
        {
            int index;
            return Find(key, out index);
        }

        public ICollection<TKey> Keys
        {
            get { return Entries.Select(entry => entry.Key).ToList(); }
        }

        public bool Remove(TKey key)
        {
            return RemoveInternal(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return Get(key, out value);
        }

        public ICollection<TValue> Values
        {
            get { return Entries.Select(entry => entry.Value).ToList(); }
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue result;
                if (!Get(key, out result))
                    throw new KeyNotFoundException(key.ToString());

                return result;
            }
            set { Put(key, value, true); }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Put(item.Key, item.Value, false);
        }

        public void Clear()
        {
            Resize(0);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue result;
            return Get(item.Key, out result) && EqualityComparer<TValue>.Default.Equals(result, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            var kvpList = Entries.ToList();
            kvpList.CopyTo(array, arrayIndex);
        }

        public int Count => countUsed;

        public bool IsReadOnly => false;

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}