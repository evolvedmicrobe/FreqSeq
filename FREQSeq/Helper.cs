using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FREQSeq
{
    public class Helper
    {

        /// <summary>
        /// The .gz extension to indicate gzipped files
        /// </summary>
        public const string ZippedFileExtension = ".gz";
        public static bool FileEndsWithZippedExtension(string fileName)
        {
            return fileName.EndsWith(ZippedFileExtension);
        }

        
			

    }
}
