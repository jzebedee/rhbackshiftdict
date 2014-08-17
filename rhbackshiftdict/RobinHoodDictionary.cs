using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace rhbackshiftdict
{
    public class RobinHoodDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        const float LOAD_FACTOR = 0.86f;
        const uint SAFE_HASH = 0x80000000;

        struct Entry
        {
            public Entry(uint hash, TKey key, TValue value)
            {
                this.hash = hash;
                this.key = key;
                this.value = value;
            }

            public readonly uint hash;
            public readonly TKey key;
            public readonly TValue value;
        }

        private IEnumerable<KeyValuePair<TKey, TValue>> Entries
        {
            get
            {
                for (uint i = 0; i < count; i++)
                    if (buckets[i].hash != 0)
                        yield return new KeyValuePair<TKey, TValue>(buckets[i].key, buckets[i].value);
            }
        }

        private Entry[] buckets;
        private uint count;
        private uint countMod;
        private uint countUsed;
        private uint growAt;
        private uint shrinkAt;

        private readonly IEqualityComparer<TKey> keyComparer;

        public RobinHoodDictionary(int size, IEqualityComparer<TKey> comparer = null) : this((uint)size, comparer) { }
        public RobinHoodDictionary(uint size, IEqualityComparer<TKey> comparer = null)
            : this(comparer)
        {
            Resize(NextPow2(size), false);
        }
        public RobinHoodDictionary(IEqualityComparer<TKey> comparer = null)
        {
            keyComparer = comparer ?? EqualityComparer<TKey>.Default;
            Clear();
        }

        private void Resize(uint newSize, bool auto = true)
        {
#if DEBUG
            if (newSize != 0)
            {
                Debug.Assert(count != newSize && countUsed <= newSize);
                Debug.Assert(NextPow2(newSize) == newSize);
            }
#endif
            var oldCount = count;
            var oldBuckets = buckets;

            count = newSize;
            countMod = newSize - 1;
            buckets = new Entry[newSize];

            growAt = auto ? Convert.ToUInt32(newSize * LOAD_FACTOR) : newSize;
            shrinkAt = auto ? newSize >> 2 : 0;

            if (countUsed > 0 && newSize != 0)
            {
                Debug.Assert(countUsed <= newSize);
                Debug.Assert(oldBuckets != null);

                countUsed = 0;

                for (uint i = 0; i < oldCount; i++)
                    if (oldBuckets[i].hash != 0)
                        PutInternal(oldBuckets[i], false, false);
            }
        }

        bool Get(TKey key, out TValue value)
        {
            uint index;
            if (Find(key, out index))
            {
                value = buckets[index].value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        bool Put(TKey key, TValue val, bool canReplace)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (countUsed == growAt)
                ResizeNext();

            return PutInternal(new Entry(GetHash(key), key, val), canReplace, true);
        }

        bool PutInternal(Entry entry, bool canReplace, bool checkDuplicates)
        {
            uint
                indexInit = entry.hash & countMod,
                indexCurrent,

                probeCurrent = 0,
                probeDistance;

            for (uint i = 0; i < count; i++)
            {
                indexCurrent = (indexInit + i) & countMod;
                if (buckets[indexCurrent].hash == 0)
                {
                    countUsed++;
                    buckets[indexCurrent] = entry;
                    return true;
                }
                else if (checkDuplicates && entry.hash == buckets[indexCurrent].hash && keyComparer.Equals(entry.key, buckets[indexCurrent].key))
                {
                    if (canReplace)
                    {
                        buckets[indexCurrent] = entry;
                        return true;
                    }
                    else
                    {
                        throw new ArgumentException("An entry with the same key already exists", "key");
                    }
                }
                else
                {
                    probeDistance = DistanceToInitIndex(indexCurrent);

                    if (probeCurrent > probeDistance)
                    {
                        probeCurrent = probeDistance;
                        Swap(ref buckets[indexCurrent], ref entry);
                    }
                    probeCurrent++;
                }
            }

            return false;
        }

        bool Find(TKey key, out uint index)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            index = 0;
            if (countUsed > 0)
            {
                uint
                    hash = GetHash(key),
                    indexInit = hash & countMod,
                    probeDistance = 0;

                for (uint i = 0; i < count; i++)
                {
                    index = (indexInit + i) & countMod;

                    if (hash == buckets[index].hash && keyComparer.Equals(key, buckets[index].key))
                        return true;

                    if (buckets[index].hash != 0)
                        probeDistance = DistanceToInitIndex(index);

                    if (i > probeDistance)
                        break;
                }
            }

            return false;
        }

        bool RemoveInternal(TKey key)
        {
            uint index;
            if (Find(key, out index))
            {
                uint curIndex, nextIndex;
                for (uint i = 0; i < count; i++)
                {
                    curIndex = (index + i) & countMod;
                    nextIndex = (index + i + 1) & countMod;

                    if (buckets[nextIndex].hash == 0 || DistanceToInitIndex(nextIndex) == 0)
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

        private uint DistanceToInitIndex(uint indexStored)
        {
            Debug.Assert(buckets[indexStored].hash != 0);

            uint indexInit = buckets[indexStored].hash & countMod;
            if (indexInit <= indexStored)
                return indexStored - indexInit;
            else
                return indexStored + (count - indexInit);
        }

        private void ResizeNext()
        {
            Resize(count == 0 ? 1 : count * 2);
        }

        private uint GetHash(TKey o)
        {
            uint h = (uint)o.GetHashCode();

            if (h == 0)
                return SAFE_HASH;

            //JDK bit spread, to ensure we have
            //a fair loword distribution
            return h ^ (h >> 16);
        }

        #region Statics
        private static uint NextPow2(uint c)
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
            uint index;
            return Find(key, out index);
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return Entries.Select(entry => entry.Key).ToList();
            }
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
            get
            {
                return Entries.Select(entry => entry.Value).ToList();
            }
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
            set
            {
                Put(key, value, true);
            }
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

        public int Count
        {
            get { return (int)countUsed; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Entries.ToList().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}
