using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace FREQSeq
{
    public class FastQParser
    {
        string DirecName;
        public static int LeadLength;
        public static int ReadLength;
        public static int totalLength;
        int MaxReadSize;
        List<string> FastQNames = new List<string>();
        public event LogEventHandler LogEvent;
        public FastQParser(List<string> FileNames)
        {
            this.FastQNames.AddRange(FileNames);
        }
        private void FireLogEvent(string msg)
        {
            if (LogEvent != null)
            {
                LogEvent(this, msg);
            }
        }
        public List<FastQRead> GetFirstReadsFromFile(int SizeToGrab = 5000)
        {
            try
            {
                if (this.FastQNames.Count == 0)
                    throw new Exception("Tried to Parse FastQ files before they were set");
                string FileName = this.FastQNames[0];
                FireLogEvent("Pre-parsing " + SizeToGrab.ToString() + " reads from " + FileName + " to get initial statistics for filter");
                StreamReader SR = new StreamReader(FileName);
                
                List<FastQRead> toReturn = new List<FastQRead>();
                string line;
                while ((line = SR.ReadLine()) != null)
                {
                    ///The array might be too big
                    if (line.StartsWith("\0")) { break; }
                    string line2, line3, line4;
                    line2 = SR.ReadLine();
                    //cheap way to skip lines
                    line3 = SR.ReadLine();
                    line4 = SR.ReadLine();
                    FastQRead fqr = new FastQRead(line, line2, line3, line4);
                    toReturn.Add(fqr);
                    if (toReturn.Count >= SizeToGrab) break;
                }
                FireLogEvent("Successfully grabbed " + toReturn.Count.ToString() + " reads.");
                SR.Close();
                return toReturn;
            }
            catch (Exception thrown)
            {
                FireLogEvent("Error: Could not parse the start of a FASTQ file to obtain statistics");
                throw thrown;
            }
        }
        public IEnumerable<StreamReader> GetStreamReaderForSequences(int SizeToReturn = 75000)
        {
            Encoding ENC = Encoding.Unicode;
            Encoding AscEnc = Encoding.ASCII;
            int BufferSize = 1 << 17;
            foreach (string FileName in this.FastQNames)
            {
                FireLogEvent("Parsing: " + FileName);
                //This hsould be streamlined so it only happens once, kinda slow now
                StreamReader SR = new StreamReader(FileName,Encoding.UTF8,false,BufferSize);
                LeadLength = SR.ReadLine().Length + 1;
                ReadLength = SR.ReadLine().Length;
                totalLength = SR.ReadLine().Length + 1 + SR.ReadLine().Length + 1 + LeadLength + ReadLength + 1;
                MaxReadSize = totalLength * 2;
                SR.Close();
                int MinLength = MaxReadSize * 200;
                if (SizeToReturn < MinLength)
                {
                    SizeToReturn = MinLength;
                }

                int SizeToRead = SizeToReturn - MaxReadSize*16;//Lot bigger to give room on the end
                SR = new StreamReader(FileName, AscEnc, true, SizeToReturn);
                char[] ReadsToReturn = new char[SizeToReturn];
                
                while (SR.Peek() != -1)
                {
                    int ReturnedCount = SR.Read(ReadsToReturn, 0, SizeToRead);
                    int CurrentPos = SizeToRead;
                    //Bullshit because of use of '+' and '@' as QC quality score characters
                    //Here is the plan, going to add 8 more lines, figure out where I am 
                    //based on that, then add an appropriate number of additional lines
                    //Position verified by '@' plus a '+' line a couple of lines after
                    if (ReturnedCount == SizeToRead)
                    {
                        List<string> toAdd=new List<string>();
                        //We are starting in the middle of a line so need to finish that off
                        string line = SR.ReadLine();
                        //are we not miraculously at the end?
                        if (line != null)
                        {
                            toAdd.Add(line);
                            for (int i = 0; i < 8; i++)
                            {
                                line = SR.ReadLine();
                                if (line == null) break;
                                //Just in case we land at "\n"
                                //if (line == "") { line = SR.ReadLine(); }
                                if (line == null) break;
                                toAdd.Add(line);
                            }
                            if (toAdd.Count == 9)
                            {
                                //Find out where I am
                                int ReadStart = -9;
                                for (int i = 0; i < 4; i++)
                                {
                                    //Already need to add the first, line so offset by one
                                    if (toAdd[i+1][0] == '@' && toAdd[(i +1)+ 2][0] == '+')
                                    {
                                        ReadStart = i;
                                        break;
                                    }
                                }
                                for (int i = 0; i < ReadStart; i++)
                                {
                                    toAdd.Add(SR.ReadLine());
                                }
                            }
                            foreach (string str in toAdd)
                            {
                                char[] toCopy = (str + "\n").ToArray();
                                Array.Copy(toCopy, 0, ReadsToReturn, CurrentPos, toCopy.Length);
                                CurrentPos += toCopy.Length;
                            }
                        }
                    }
                    byte[] arr = ENC.GetBytes(ReadsToReturn);
                    MemoryStream MS = new MemoryStream(arr);
                    StreamReader SR2 = new StreamReader(MS, ENC, false);
                    Array.Clear(ReadsToReturn, 0, ReadsToReturn.Length);
                    yield return SR2;
                }
            }
        }
        /// <summary>
        /// Takes a StreamReader on a memory stream containing the raw unicode data for a portion of a FASTQ
        /// file and converts them all to FASTQ read classes.  The idea is that this conversion can be done 
        /// by multiple threads while the main thread reads off the disk and creates the streams.
        /// </summary>
        /// <returns>A collection of FASTQ Reads</returns>
        public static List<FastQRead> GetFastQReadsFromStream(StreamReader FastQPortionStream)
        {
            List<FastQRead> toReturn = new List<FastQRead>();
            string line;
            while ((line = FastQPortionStream.ReadLine()) != null)
            {
                ///The array might be too big
                if (line.StartsWith("\0")) { break; }
                string line2, line3, line4;
                line2 = FastQPortionStream.ReadLine();
                //cheap way to skip lines
                line3 = FastQPortionStream.ReadLine();
                line4 = FastQPortionStream.ReadLine();
                FastQRead fqr = new FastQRead(line, line2, line3, line4);
                toReturn.Add(fqr);
            }
            FastQPortionStream.Close();
            return toReturn;
        }
    }
    /// <summary>
    /// A FastQRead
    /// </summary>
    public class FastQRead
    {
        public readonly string Sequence,id;
        public readonly sbyte[] QCscores;
        double pAvgQuality=-999;
        public double AvgQuality
        {
            get
            {
                if (pAvgQuality == -999)
                {
                    //pAvgQuality = QCscores.Sum()/(double) QCscores.Length;
                    //pAvgQuality = ((double) QCscores.Sum())/(double) QCscores.Length;
                    //pAvgQuality = QCscores.Sum(x=>(double)x) / (double)QCscores.Length;
                    int t = 0;
                    for (int j = 0; j < QCscores.Length; j++)
                        t += QCscores[j];
                    pAvgQuality = t/(double) QCscores.Length;
                }
                return pAvgQuality;
            }
        }
        private float pPercN = -999;
        public float PercN
        {
            get
            {
                if (pPercN == -999) CalcPercentageN();
                return pPercN;
            }
        }
        private void CalcPercentageN()
        {
            float count=0;
            foreach (char c in Sequence)
            {
                if (c == 'N') count = count + 1;
            }
            pPercN = count / (float)Sequence.Length;
        }
        public FastQRead(string line1, string line2, string line3, string line4)
        {
            //First to validate lines
            if (line1[0]!='@' || line3[0]!='+')
            {
                string ExceptionMessage = "FastQ Lines Formatted Poorly, missing @ or + symbol in correct spot\nData From File Below\n";
                ExceptionMessage += line1 + "\n" + line2 + "\n" + line3 + "\n" + line4;
                throw new IOException(ExceptionMessage);
            }
            //QC line same length as sequence line
            if(line2.Length!=line4.Length)
            {
                int u = line2.Length;
                int k = line4.Length;
                string ExceptionMessage = "FastQ QC line length ("+k.ToString()+") does not equal to sequence length("+u.ToString()+")";
                ExceptionMessage += line1 + "\n" + line2 + "\n" + line3 + "\n" + line4;
                throw new IOException(ExceptionMessage);
            }
            this.Sequence = line2;
            this.id = line1;
            //Now to get QC Score, I believe this is a phred score obtained by subtracting 64 from the ASCII character
            QCscores = new sbyte[line2.Length];
            //QCscores=new int[line2.Length];
            int i=0;
            int dif = 32;//Amount added to each Illumina ASCII character to convert from ASCII to phred.
            int sum = 0;
            foreach(char c in line4)
            {
                //QCscores[i] = (c - dif);
                int tmp = (c - dif);
                sum += tmp;
                QCscores[i] = (sbyte) tmp;
                //QCscores[i] = (sbyte)(c - dif);
                i++;
            }
            pAvgQuality = sum/(double) QCscores.Length;
        }
        public override string ToString()
        {
            return this.id +"\n"+ this.Sequence+"\n";
        }
    }
}
