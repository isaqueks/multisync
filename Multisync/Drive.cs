using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Multisync.App.Util;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using DriveFile = Google.Apis.Drive.v3.Data.File;

using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Multisync.GoogleDriveInterface
{
    /* Google Drive */
    public class Drive
    {
        //const string DEFAULT_FIELDS = "nextPageToken, files(id, name, mimeType, parents)";
        const string DEFAULT_FIELDS = "*";

        public class Page
        {
            public string SearchQuery;
            public int MaxAllowedSize;
            public string ID;
            public string NextPageID;
            public ItemInterface[] Items;

            public DirectoryInterface[] GetDirectories()
            {
                List<DirectoryInterface> dirs = new List<DirectoryInterface>();

                foreach(var item in Items)
                {
                    if (item.IsFolder())
                    {
                        dirs.Add(new DirectoryInterface(item));
                    }
                }

                return dirs.ToArray();
            }

            public Page() { }

            public Page(string id, string nextId, ItemInterface[] items) {
                this.ID = id;
                this.NextPageID = nextId;
                this.Items = items;
            }

            public Page Next(Drive drive)
            {
                return drive.RunQueryAsPage(SearchQuery, MaxAllowedSize, NextPageID);
            }
        }

        private UserCredential userCredential;
        private DriveService driveService;

        public DriveService Service() => driveService;

        public Drive(UserCredential credential)
        {
            this.userCredential = credential;
            this.driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Multisync",
            });
        }

        public ItemID FindID(string name)
        {
            FilesResource.ListRequest listRequest = this.driveService.Files.List();
            listRequest.PageSize = 1;
            listRequest.Fields = DEFAULT_FIELDS;
            listRequest.Q = $"name = '{name}'";
            var list = listRequest.Execute();
            if (list.Files.Count == 0)
                throw new NullReferenceException("No ID found for this name.");

            return new ItemID(name, list.Files[0].Id);
        }

        public ItemID FindName(string id)
        {
            var result = Service().Files.Get(id).Execute();

            return new ItemID(result.Name, result.Id);
        }

        public ItemInterface GetFileById(string rawID)
        {
            var req = Service().Files.Get(rawID);
            req.Fields = DEFAULT_FIELDS;
            var result = req.Execute();
            return new ItemInterface(result, this);
        }

        public ItemInterface FindParent(ItemInterface child)
        {
            if (child.GoogleDriveFile.Parents == null ||
                child.GoogleDriveFile.Parents.Count == 0 ||
                 child.GoogleDriveFile.Parents[0] == null)
            {
                return null;
            }

            string id =  child.GoogleDriveFile.Parents[0];
            var parent = GetFileById(id);
            return parent;
        }

        public ItemInterface FindItemFromPath(string fullPath)
        {
            fullPath = PathNormalizer.Normalize(fullPath);
            string[] items = PathNormalizer.Normalize(fullPath).Split("/");
            //Page page = this.RunQueryAsPage($"name = '{items[0]}'");
            ItemInterface current = this.GetFileById("root");

            for (int i = 1; i < items.Length; i++)
            {
                string item = items[i];
                var result = this.GetDirectoryContent(current.ItemIdentifier);
                foreach(var resultFile in result.Items)
                {
                    if (resultFile.Name == item)
                    {
                        current = resultFile;
                        break;
                    }
                }
            }

            string check = current.GetFullName();
            bool equal = check == fullPath;
            if (!equal)
                return null;

            return current;
        }

        public ItemInterface CreateNewFile(string absoluteFile, string localPath, string mimeType = "application/unknown")
        {
            var dinfo = Directory.GetParent(localPath);
            var parentPath = 
                dinfo
                .FullName
                // Remove the C:\\
                .Substring(dinfo.Root.Name.Length);

            var parent = this.FindItemFromPath(parentPath);

            if (parent == null && localPath.Split('/').Length > 2)
            {// Not root
                parent = CreateNewFile(Directory.GetParent(absoluteFile).FullName, parentPath, "application/vnd.google-apps.folder");
            }

            var itemName = Path.GetFileName(localPath);
            DriveFile body = new DriveFile();
            body.Name = itemName;
            body.MimeType = mimeType;
            if (parent != null)
            {
                if (body.Parents == null)
                    body.Parents = new List<string>();
                body.Parents.Add(parent.ID);
            }

            var createRequest = Service().Files.Create(body);
            var res = createRequest.Execute();

            return new ItemInterface(res, this);
        }
            
        public FileList RunQuery(string query, int pageSize = 100, string pageToken = null, string fields = DEFAULT_FIELDS)
        {
            FilesResource.ListRequest listRequest = this.driveService.Files.List();
            listRequest.PageSize = pageSize;
            listRequest.Fields = fields;

            if (query != null)
                listRequest.Q = "not trashed and " + query;

            if (pageToken != null)
                listRequest.PageToken = pageToken;

            return listRequest.Execute();
        }

        public Page RunQueryAsPage(string query, int pageSize = 100, string pageToken = null)
        {
            FileList list = RunQuery(query, pageSize, pageToken);

            List<ItemInterface> files = new List<ItemInterface>();
            foreach(DriveFile dfile in list.Files)
            {
                ItemInterface IFile = new ItemInterface(dfile, this);
                files.Add(IFile);
            }

            Page page = new Page(pageToken, list.NextPageToken, files.ToArray());
            page.SearchQuery = query;
            page.MaxAllowedSize = pageSize;
            return page;
        }

        public Page GetDirectoryContent(ItemID dirID, int pageSize = 100, string pageToken = null)
        {
            if (dirID.ID == null)
                throw new NullReferenceException("dirID.ID is null");

            return RunQueryAsPage($"'{dirID.ID}' in parents", pageSize, pageToken);
        }

        public Page GetDirectories(ItemID parentDirID, int pageSize = 100, string pageToken = null)
        {
            if (parentDirID.ID == null)
                throw new NullReferenceException("parentDirID.ID is null");

            return RunQueryAsPage($"'{parentDirID.ID}' in parents and mimeType = 'application/vnd.google-apps.folder'", pageSize, pageToken);
        }

        public Page GetFiles(ItemID parentDirID, int pageSize = 100, string pageToken = null)
        {
            if (parentDirID.ID == null)
                throw new NullReferenceException("parentDirID.ID cannot be null!");

            return RunQueryAsPage($"'{parentDirID.ID}' in parents and mimeType != 'application/vnd.google-apps.folder'", pageSize, pageToken);
        }

    }
}
