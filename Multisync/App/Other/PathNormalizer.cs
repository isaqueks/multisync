using System;
using System.Collections.Generic;
using System.Text;

namespace Multisync.App.Util
{
    public class PathNormalizer
    {
        public static string Normalize(string path)
        {
            path = path.Trim().Replace("\\", "/");

            while (path.StartsWith("/"))
                path = path.Substring(1);

            while (path.EndsWith("/"))
                path = path.Substring(0, path.Length - 1);

            return path;
        }
    }
}
