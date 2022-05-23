using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    public static class ValidationHelper
    {
        public static void ValidateArgs(String[] p_arrArgs)
        {

            if (p_arrArgs.Length != 3)
            {
                throw new Exception("Wrong arguments count.\n");
            }

            if (p_arrArgs[0].ToLower() != "compress" && p_arrArgs[0].ToLower() != "decompress")
            {
                throw new Exception("First argument must be \"compress\" or \"decompress\".\n");
            }

            if (p_arrArgs[1].Length == 0)
            {
                throw new Exception("No source file name was specified.\n");
            }

            if (!File.Exists(p_arrArgs[1]))
            {
                throw new Exception("No source file was found.\n");
            }

            FileInfo fiInputFileInfo = new FileInfo(p_arrArgs[1]);
            FileInfo fiOutputFileInfo = new FileInfo(p_arrArgs[2]);

            if (!Directory.Exists(fiOutputFileInfo.Directory.ToString()))
            {
                throw new Exception("Destination path does not exist.");
            }

            if (fiInputFileInfo.Length == 0)
            {
                throw new Exception("Can't make this file even smaller! :)");
            }

            if (p_arrArgs[1] == p_arrArgs[2])
            {
                throw new Exception("Source and destination files must be different.");
            }

            if (fiInputFileInfo.Extension == ".gz" && p_arrArgs[0] == "compress")
            {
                throw new Exception("File has already been compressed.");
            }

            if (fiOutputFileInfo.Extension == ".gz" && fiOutputFileInfo.Exists)
            {
                throw new Exception("Destination file already exists. Please specify the different file name.");
            }

            if (fiInputFileInfo.Extension != ".gz" && p_arrArgs[0] == "decompress")
            {
                throw new Exception("File to be decompressed must have .gz extension.");
            }

            if (fiOutputFileInfo.Extension != ".gz" && p_arrArgs[0] == "compress")
            {
                throw new Exception("Destination compresse file must have .gz extension.");
            }

            if (p_arrArgs[2].Length == 0)
            {
                throw new Exception("No destination file name was specified.");
            }
        }
    }
}
