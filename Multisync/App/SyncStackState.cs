using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Multisync.GoogleDriveInterface;

namespace Multisync.App
{
    /// <summary>
    /// This will store and load to/from a file the items that is already synchronized
    /// and the sync type (Downloading, uploading, ...)
    /// </summary>
    public class SyncStackState
    {
        public List<SynchronizingItem> localStack;
        private Drive drive;

        public SyncStackState(Drive drive)
        {
            localStack = new List<SynchronizingItem>();
            this.drive = drive;
        }

        public void SaveStateTo(string path = "multisync_state.msync")
        {
            //string tmpPath = path + ".tmp";

            if (File.Exists(path))
                File.Delete(path);

            StreamWriter stream = new StreamWriter(path);
            foreach(var item in localStack)
            {
                string line = item.ID + " " + ((int)item.State).ToString() + " " + ((int)item.Task).ToString();
                stream.WriteLine(line);
            }
            stream.Flush();
            stream.Close();

            //File.Delete(path);
            //File.Move(tmpPath, path);
        }

        public void LoadStateFrom(string path = "multisync_state.msync")
        {
            /* check file */
            if (!File.Exists(path)) return;

            StreamReader stream = new StreamReader(path);
            string line;
            while ((line = stream.ReadLine()) != null)
            {
                string[] args = line.Split(" ");
                string id = args[0];
                int state = int.Parse(args[1]);
                SynchronizingItem.SyncState convertedState =
                    ((SynchronizingItem.SyncState)state);

                int task = int.Parse(args[2]);
                SynchronizingItem.TaskState convertedTask =
                    ((SynchronizingItem.TaskState)task);

                SynchronizingItem item = new SynchronizingItem(id, drive, convertedTask);
                localStack.Add(item);
            }

            stream.Close();
        }

        public SynchronizingItem[] GetPendingItems()
        {
            List<SynchronizingItem> items = new List<SynchronizingItem>();

            foreach(var item in localStack)
            {
                if (item.Task != SynchronizingItem.TaskState.Done)
                    items.Add(item);
            }

            return items.ToArray();
        }

        public SynchronizingItem GetItem(string id)
        {
            int index = -1;
            int i = 0;
            foreach (var localItem in localStack)
            {
                if (id == localItem.ID)
                {
                    index = i;
                    break;
                }
                i++;
            }

            if (index >= 0)
            {
                return localStack[index];
            }

            return null;
        }
             
        public void UpdateState(SynchronizingItem item)
        {
            int index = -1;
            int i = 0;
            foreach (var localItem in localStack)
            {
                if (item.ID == localItem.ID)
                {
                    index = i;
                    break;
                }
                i++;
            }
            if (item.State == SynchronizingItem.SyncState.DeletingAll
                && item.Task == SynchronizingItem.TaskState.Done)
            {
                if (index >= 0)
                {
                    localStack.Remove(item);
                }
                // Else, just don't add
            }
            else
            {
                if (index < 0)
                {
                    localStack.Add(item);
                }
                else
                {
                    localStack[index] = item;
                }
            }
        }
    }
}
