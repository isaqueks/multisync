using System;
using System.Collections.Generic;
using System.Text;
using Multisync.GoogleDriveInterface;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using Multisync.App.Util;

namespace Multisync.App
{
    public class SyncStack
    {
        // This will be locked while other thread syncs this list
        private List<SynchronizingItem> lockedSynchronizingItems;

        // But this will be opened. Will be merged whith lockedSynchronizingItems
        // when sync thread finishes sync
        private List<SynchronizingItem> tempSyncItems;
        // But it can also be locked for a small period of time
        bool tempSyncItems_locked = false;

        public ModifiedFileLog ModLog;

        private SyncStackState localStack;
        private Drive drive;
        private string folder;

        private string synchronizingItemPath = string.Empty;

        private bool paused = false;

        public void Pause() => paused = true;
        public void Resume() => paused = false;

        public bool IsPaused => paused;

        public string SynchronizingItemPath => synchronizingItemPath;

        void Log(string action, string item, bool isDir)
        {
            if (isDir) Console.ForegroundColor = ConsoleColor.Blue;
            else Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(isDir ? "DIR  " : "FILE ");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(action + " ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(item);
        }

        public SyncStack(string folderToSync, Drive drive)
        {
            tempSyncItems = new List<SynchronizingItem>();
            lockedSynchronizingItems = new List<SynchronizingItem>();
            localStack = new SyncStackState(drive);
            this.drive = drive;
            this.folder = folderToSync;

            ModLog = new ModifiedFileLog();
            ModLog.LoadFrom();

            localStack.LoadStateFrom();

            var pending = localStack.GetPendingItems();
            tempSyncItems.AddRange(pending);
        }

        public SynchronizingItem GetItemById(string id)
        {
            return localStack.GetItem(id);
        }

        public void Upload(string localPath, string absolutePath)
        {
            string drivePath =
                PathNormalizer.Normalize(absolutePath).Replace(
                PathNormalizer.Normalize(localPath), String.Empty);

            var item = drive.CreateNewFile(absolutePath, drivePath);
            //ModLog.SetFileLastWriteTime(absolutePath, new DateTime(DateTime.Now.Ticks + 10000));
            this.SyncWithState(item, SynchronizingItem.SyncState.Uploading);

        }

        private enum DownloadOrUpload
        {
            Download = 0,
            Upload = 1,
            Update = 2,
            Nothing = 3
        }

        /// <summary>
        /// Check if the file should be downloaded or uploaded (FILE ONLY)
        /// </summary>
        /// <param name="item">The item to check</param>
        /// <returns>DownloadOrUpload</returns>
        private DownloadOrUpload CheckDownloadOrUpload(string basePath, ItemInterface item)
        {
            string finalPath = PathNormalizer.Normalize(
                Path.Combine(basePath, item.GetFullName())
            );

            if (item == null)
                return DownloadOrUpload.Upload;

            if (item.IsFolder())
                throw new Exception("Item should be a file, not folder!");

            // If reached here, is a file
            if (File.Exists(finalPath))
            {
                var itemHash = item.MD5();
                var localHash = HashMD5(finalPath);

                if (itemHash == localHash)
                    return DownloadOrUpload.Nothing;

                else
                {
                    var driveMod = item.LastMod;
                    var localMod = File.GetLastWriteTime(finalPath);

                    if (driveMod > localMod)
                        // Drive's item is more recent, let's download it
                        return DownloadOrUpload.Download;
                    else if (driveMod < localMod)
                        // Local item is more recent, let's upload it
                        return DownloadOrUpload.Update;
                }

            }
            bool exists = File.Exists(finalPath);
            // File does not exists, let's download
            return DownloadOrUpload.Download;
        }

        public void SyncWithState(ItemInterface item, SynchronizingItem.SyncState state)
        {
            while (tempSyncItems_locked) /* wait */;


            var localItem = localStack.GetItem(item.ID);
            if (localItem != null && (localItem.State == state
                && localItem.Task != SynchronizingItem.TaskState.Done))
                return;

            var syncItem = new SynchronizingItem(item, SynchronizingItem.TaskState.NotStarted, state);

            int index = 0;
            bool shouldBeUpdatedAtIndex = false;

            foreach(var sItem in tempSyncItems)
            {
                if (sItem.ID == syncItem.ID)
                {
                    shouldBeUpdatedAtIndex = true;
                    break;
                }

                index++;
            }

            tempSyncItems_locked = true;

            if (shouldBeUpdatedAtIndex)
            {
                tempSyncItems[index] = syncItem;
            }
            else
            {
                tempSyncItems.Add(syncItem);
            }

            tempSyncItems_locked = false;
        }

        void WriteStreamToFile(Stream stream, string destinationFile, int bufferSize = 4096, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.ReadWrite)
        {
            using (var destinationFileStream = new FileStream(destinationFile, mode, access, share))
            {
                while (stream.Position < stream.Length)
                {
                    destinationFileStream.WriteByte((byte)stream.ReadByte());
                }
            }
        }

        string HashMD5(string filePath)
        {
            var md5 = MD5.Create();
            var stream = File.OpenRead(filePath);
            var hash = BitConverter.ToString(md5.ComputeHash(stream))
                .Replace("-", "")
                .ToLower();

            stream.Close();
            md5.Clear();

            return hash;
        }

        void ModifyFile(string path)
        {
            path = PathNormalizer.Normalize(path);

            ModLog.UpdateFileMultisyncModDate(path);
        }

        public bool ModifiedFile(string path)
        {
            path = PathNormalizer.Normalize(path);

            var modDate = ModLog.GetMultisyncModDate(path);
            var fileModDate = File.GetLastWriteTime(path);

            return modDate == fileModDate;
        }

        void sync()
        {
            while (paused || tempSyncItems_locked) ;

            if (tempSyncItems.Count > 0)
            {
                tempSyncItems_locked = true;

                lockedSynchronizingItems.Clear();
                lockedSynchronizingItems.AddRange(tempSyncItems);
                tempSyncItems.Clear();

                tempSyncItems_locked = false;
            }

            
            foreach(var syncItem in lockedSynchronizingItems)
            {
                while (paused) ;

                var item = syncItem.GetItem();
                var isDir = item.IsFolder();

                string finalPath = PathNormalizer.Normalize(
                    Path.Combine(folder, item.GetFullName())
                );

                if (isDir)
                {
                    if (!Directory.Exists(finalPath))
                    {
                        Log("DOWNLOAD", item.GetFullName(), true);
                        Directory.CreateDirectory(finalPath);
                    }

                    // Else does nothing, directory already exists
                    syncItem.State = SynchronizingItem.SyncState.Downloading;
                    syncItem.Task = SynchronizingItem.TaskState.Done;
                }
                else
                {
                    FileInterface file = new FileInterface(item);

                    DownloadOrUpload downOrUp;
                    if (syncItem.State == SynchronizingItem.SyncState.Uploading)
                        downOrUp = DownloadOrUpload.Upload;
                    else if (syncItem.State == SynchronizingItem.SyncState.Downloading)
                        downOrUp = DownloadOrUpload.Download;
                    else
                        downOrUp = CheckDownloadOrUpload(folder, item);

                    if (downOrUp == DownloadOrUpload.Nothing)
                    {
                        syncItem.State = SynchronizingItem.SyncState.None;
                    }
                    else if (downOrUp == DownloadOrUpload.Download)
                    {
                        Log("DOWNLOAD", finalPath, false);

                        syncItem.State = SynchronizingItem.SyncState.Downloading;
                        syncItem.Task = SynchronizingItem.TaskState.Executing;
                        localStack.UpdateState(syncItem);

                        file.DownloadTo(finalPath);
                    }
                    else if (downOrUp == DownloadOrUpload.Update)
                    {
                        Log("UPLOAD", finalPath, false);

                        syncItem.State = SynchronizingItem.SyncState.Uploading;
                        syncItem.Task = SynchronizingItem.TaskState.Executing;
                        localStack.UpdateState(syncItem);

                        file.UpdateFrom(finalPath);
                    }
                }

                syncItem.Task = SynchronizingItem.TaskState.Done;
                localStack.UpdateState(syncItem);
            }

        }
        Thread saveTh;

        void save()
        {
            while(true)
            {
                Thread.Sleep(5000);
                localStack.SaveStateTo();
                ModLog.SaveTo();
            }
        }

        public void SyncSync()
        {
            sync();
        }

        public void Start()
        {
            saveTh = new Thread(save);
            saveTh.Start();
        }
    }
}
