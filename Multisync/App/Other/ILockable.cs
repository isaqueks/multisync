using System;
using System.Collections.Generic;
using System.Text;

namespace Multisync.App.Util
{
    public interface ILockable
    {
        void Lock();
        void Unlock();
        bool IsLocked();
    }
}
