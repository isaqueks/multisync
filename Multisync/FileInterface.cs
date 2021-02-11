using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using DriveFile = Google.Apis.Drive.v3.Data.File;

using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Multisync.GoogleDriveInterface
{

    public class ItemInterface
    {
        public enum AbstractMimeType
        {
            Audio = 0,
            Document = 1,
            DriveSDK = 2,
            Drawing = 3,
            File = 4,
            Folder = 5,
            Form = 6,
            FusionTable = 7,
            Map = 8,
            Photo = 9,
            Presentation = 10,
            Script = 11,
            Shortcut = 12,
            Site = 13,
            Spreadsheet = 14,
            Unknown = 15,
            Video = 16
        }

        public ItemInterface GetParent()
        {
            if (_parent != null)
                return _parent;

            _parent = this.GDrive.FindParent(this);
            return _parent;
        }

        public Drive GDrive;
        public DriveFile GoogleDriveFile;

        public string OriginalMimeType => GoogleDriveFile.MimeType;
        public AbstractMimeType MimeType
        {
            get
            {
                AbstractMimeType type = AbstractMimeType.Unknown;

                switch (OriginalMimeType)
                {
                    case "application/vnd.google-apps.audio":
                        type = (AbstractMimeType)0;
                        break;
                    case "application/vnd.google-apps.document":
                        type = (AbstractMimeType)1;
                        break;
                    case "application/vnd.google-apps.drive-sdk":
                        type = (AbstractMimeType)2;
                        break;
                    case "application/vnd.google-apps.drawing":
                        type = (AbstractMimeType)3;
                        break;
                    case "application/vnd.google-apps.file":
                        type = (AbstractMimeType)4;
                        break;
                    case "application/vnd.google-apps.folder":
                        type = (AbstractMimeType)5;
                        break;
                    case "application/vnd.google-apps.form":
                        type = (AbstractMimeType)6;
                        break;
                    case "application/vnd.google-apps.fusiontable":
                        type = (AbstractMimeType)7;
                        break;
                    case "application/vnd.google-apps.map":
                        type = (AbstractMimeType)8;
                        break;
                    case "application/vnd.google-apps.photo":
                        type = (AbstractMimeType)9;
                        break;
                    case "application/vnd.google-apps.presentation":
                        type = (AbstractMimeType)10;
                        break;
                    case "application/vnd.google-apps.script":
                        type = (AbstractMimeType)11;
                        break;
                    case "application/vnd.google-apps.shortcut":
                        type = (AbstractMimeType)12;
                        break;
                    case "application/vnd.google-apps.site":
                        type = (AbstractMimeType)13;
                        break;
                    case "application/vnd.google-apps.spreadsheet":
                        type = (AbstractMimeType)14;
                        break;
                    case "application/vnd.google-apps.unknown":
                        type = (AbstractMimeType)15;
                        break;
                    case "application/vnd.google-apps.video":
                        type = (AbstractMimeType)16;
                        break;
                }

                return type;
            }
        }
        public string MD5() => GoogleDriveFile.Md5Checksum;

        public DateTime? LastMod => GoogleDriveFile.ModifiedTime;

        public ItemID ItemIdentifier => new ItemID(GoogleDriveFile.Name, GoogleDriveFile.Id);
        public string Name => ItemIdentifier.Name;
        public string ID => ItemIdentifier.ID;

        private ItemInterface _parent;

        private string fullName = string.Empty;

        public string GetFullName()
        {
            if (!String.IsNullOrEmpty(fullName))
                return fullName;

            ItemInterface item = this;
            while(item != null)
            {
                fullName = item.Name + (string.IsNullOrEmpty(fullName) ? String.Empty : "\\") + fullName;
                item = item.GetParent();
            }

            return Multisync.App.Util.PathNormalizer.Normalize(fullName);
        }

        public bool IsFolder() => MimeType == AbstractMimeType.Folder;

        public ItemInterface() { }

        public ItemInterface(DriveFile file, Drive drive)
        {
            this.GoogleDriveFile = file;
            this.GDrive = drive;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class DirectoryInterface : ItemInterface
    {
        public DirectoryInterface() { }

        public DirectoryInterface (ItemInterface item): base(item.GoogleDriveFile, item.GDrive)
        {
            if (!item.IsFolder())
                throw new ArgumentException("item doesn't represent a folder!", "item");
        }

        public Drive.Page GetContent(string pageId = null, int pageSize = 100)
        {
            return this.GDrive.GetDirectoryContent(this.ItemIdentifier, pageSize, pageId);
        }

        public Drive.Page GetDirectories(string pageId = null, int pageSize = 100)
        {
            return this.GDrive.GetDirectories(this.ItemIdentifier, pageSize, pageId);
        }

        public Drive.Page GetFiles(string pageId = null, int pageSize = 100)
        {
            return this.GDrive.GetFiles(this.ItemIdentifier, pageSize, pageId);
        }
    }

    public class FileInterface : ItemInterface
    {
        public FileInterface() {  }

        public FileInterface(ItemInterface item): base(item.GoogleDriveFile, item.GDrive)
        {
            if (item.IsFolder())
                throw new ArgumentException("item represents a folder!", "item");
        }

        public long? Size()
        {
            return this.GoogleDriveFile.Size;
        }

        public Stream GetContentStream()
        {
            if (String.IsNullOrEmpty(ID))
                throw new NullReferenceException("ItemIdentifier.ID is null");

            return this.GDrive.Service().Files.Get(ID).ExecuteAsStream();
        }

        const string TMP_DOWNLOAD = "./download.multisync";
        public void DownloadTo(String path)
        {
            var stream = System.IO.File.OpenWrite(TMP_DOWNLOAD);

            this.GDrive.Service().Files.Get(ID).Download(stream);
            stream.Close();

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            if (!Directory.Exists(Directory.GetParent(path).FullName))
                Directory.CreateDirectory(Directory.GetParent(path).FullName);

            System.IO.File.Move(TMP_DOWNLOAD, path);
            Multisync.App.ModifiedFileLog.Main.SetFileLastWriteTime(path, (DateTime)this.LastMod);
        }

        public void UpdateFrom(String path)
        {
            //var uploadStream = System.IO.File.OpenRead(path);

            byte[] byteArray = System.IO.File.ReadAllBytes(path);
            System.IO.MemoryStream uploadStream = new System.IO.MemoryStream(byteArray);

            DriveFile body = new DriveFile();
            //body.Name = GoogleDriveFile.Name;
            //body.Parents = GoogleDriveFile.Parents;
            //body.MimeType = OriginalMimeType;
            //body.Id = ID;
            body.Description = "Test upload with Multisync!";
                
            var up = this.GDrive.Service().Files.Update(body,
                ID, uploadStream, base.OriginalMimeType);
            up.Fields = "*";
            up.Upload();
            up.AddParents = GetParent()?.ID;
            

            var newFile = GDrive.GetFileById(up.FileId);
            this.GoogleDriveFile = newFile.GoogleDriveFile;

            uploadStream.Close();
            var res = up.ResponseBody;

            Multisync.App.ModifiedFileLog.Main.SetFileLastWriteTime(path, (DateTime)this.LastMod);
           // System.IO.File.SetLastWriteTime(path, (DateTime)newFile.LastMod);
        }
    }
}
