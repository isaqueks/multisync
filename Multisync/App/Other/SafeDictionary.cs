using System;
using System.Collections.Generic;
using System.Text;

namespace Multisync.App.Util
{
    public class SafeDictionary<TKey, TValue>: Dictionary<TKey, TValue>, ILockable
    {
        /* ILockable */
        bool locked = false;
        public void Lock() => locked = true;
        public void Unlock() => locked = false;
        public bool IsLocked() => locked;

        void awaitWhileLocked()
        {
            while (locked)
                /* Do nothing */ ;
        }

        public new void Add(TKey key, TValue value)
        {
            awaitWhileLocked();
            Lock();
            if (base.ContainsKey(key))
                base.Remove(key);
            base.Add(key, value);
            Unlock();
        }

        public new void Remove(TKey key)
        {
            awaitWhileLocked();
            base.Remove(key);
        }

        public new void Clear()
        {
            awaitWhileLocked();
            base.Clear();
        }
    }
}
