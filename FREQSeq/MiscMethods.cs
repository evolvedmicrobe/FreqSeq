using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace FREQSeq
{
    //A delegat to handle log events
    public delegate void LogEventHandler(object sender,string Message);
    public class MiscMethods
    {
        public static void ValidateACGT(string seq)
        {
            foreach (char c in seq)
            {
                switch (c)
                {
                    case 'A':
                        break;
                    case 'G':
                        break;
                    case 'C':
                        break;
                    case 'T':
                        break;
                    default:
                        throw new IOException("Sequence: "+seq+" contains non A, C, G or T elements.");
                }
            }
        }
    }
}
