using System.IO;
using System.Collections.Generic;
using System.Text;
using Multisync.GoogleDriveInterface;

namespace Multisync.App
{
    /// <summary>
    /// The state of a synchronizing (or a to be synchronized) folder of file
    /// </summary>
    public class SynchronizingItem
    {
        public enum SyncState
        {
            Downloading =           0,
            Uploading =             1,
            DeletingLocaly =        2,
            DeletingRemote =        3,
            DeletingAll =           4,
            CheckDownloadOrUpload = 5,
            None =                  6
        }

        public enum TaskState
        {
            NotStarted =   0,
            Executing  =   1,
            Done       =   2
        }

        private ItemInterface _item;

        public ItemInterface GetItem()
        {
            if (_item != null)
                return _item;

            _item = Drive.GetFileById(ID);
            return _item;
        }

        public TaskState Task;
        public SyncState State;

        public Drive Drive;
        public string ID;

        public SynchronizingItem(string id, Drive drive, TaskState task, SyncState state = SyncState.None)
        {
            this.Drive = drive;
            this.ID = id;
            this.Task = task;
            this.State = state;
        }

        public SynchronizingItem(ItemInterface item, TaskState task, SyncState state = SyncState.None)
        {
            this.Drive = item.GDrive;
            this.ID = item.ID;
            this.Task = task;
            this.State = state;
        }
    }
}
