using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;

using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoogleDrive = Multisync.GoogleDriveInterface.Drive;
using Multisync.GoogleDriveInterface;
using Multisync.App;

namespace Multisync
{
    class Program
    {
        static void Main(string[] args)
        {

            App.App app = new App.App();
            app.Run();
        }
    }
}
