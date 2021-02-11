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
    public class ItemID
    {
        public static ItemID ROOT = new ItemID(null, "root");

        public string Name;
        // Essential
        public string ID;

        public ItemID(string name, string id)
        {
            this.Name = name;
            this.ID = id;
        }

        public static ItemID FromID(string id)
        {
            return new ItemID(null, id);
        }
    }
}