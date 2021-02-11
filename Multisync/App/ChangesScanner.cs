using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Multisync.App.Util;

namespace Multisync.App
{
    public delegate void FileChangedEvent(string fileName);

    public class ChangesScanner
    {
        private SyncStack syncStack;
        private ModifiedFileLog modLog => syncStack.ModLog;
        private FileChangedEvent callback;
        private Thread scanThread;
        private string root;

        private bool paused;

        public void Pause() => paused = true;
        public void Resume() => paused = false;
        public bool IsPaused => paused;

        public ChangesScanner(string root, SyncStack syncStack, FileChangedEvent callback)
        {
            this.syncStack = syncStack;
            this.callback = callback;
            this.root = root;
        }

        void scanDir(string dir)
        {
            while (paused) /* wait */;

            string[] subDirs = Directory.GetDirectories(dir);
            string[] files = Directory.GetFiles(dir);

            foreach(var raw_file in files)
            {
                string file = PathNormalizer.Normalize(raw_file);

                var lastModDate = modLog.GetMultisyncModDate(file);
                var realLastModDate = File.GetLastWriteTime(file);

                bool moddedByMultisync = lastModDate == realLastModDate;
                if (!moddedByMultisync &&
                    file != syncStack.SynchronizingItemPath)
                {
                    modLog.UpdateFileMultisyncModDate(file);
                    this.callback(file);
                }
            }

            foreach(var subdir in subDirs)
            {
                scanDir(subdir);
            }
        }

        void fsScan()
        {
            while (true)
            {
                while (paused) /* wait */;

                scanDir(root);
            }
        }

        public void StartAsync()
        {
            scanThread = new Thread(fsScan);
            scanThread.Start();
        }
    }
}
