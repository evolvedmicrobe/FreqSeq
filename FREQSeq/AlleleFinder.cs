//#define DEBUG 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FREQSeq
{
    /// <summary>
    /// A class that takes a FASTQ file and various settings and parses the files to count the amount of alleles 
    /// present at each of the barcodes.  Should usually be passed a BarCodecollection and AlleleCollection when initialized.
    /// </summary>
    public class AlleleFinder
    {
        public const string unknownID = "UNKNOWN";
        //length of the barcode sequence, should always be 6
        public const int BARCODE_LENGTH = 6;
        public const int ALLELE_START_POS = 23;
        public const string M13_SEQUENCE="GTAAAACGACGGCCAGT";
        public int InitialReadsToParse { get; set; }
		public int MinReadLength { get; set; }
        public bool RequireM13Sequence{get;set;}
        public bool AllowReadsWithM13WithinHamming1ToBeAssigned { get; set; }
        public bool AllowBarCodesWithinHamming1ToBeAssigned { get; set; }
        /// <summary>
        /// Filter reads with an average quality below this value 
        /// if the QualityFilter flag is set.
        /// </summary>
        public float MinAverageQualityForInexactMatches { get; set; }
        /// <summary>
        /// The highest percentage of "N" we will allow in a read before we will not use the read
        /// </summary>
        public float MaxPercentageNForInexactMatches { get; set; }
        /// <summary>
        /// Determines if we should try to align and assign inexact matches
        /// </summary>
        public bool AssignImperfectlyMatchedReads { get; set; }
        /// <summary>
        /// Reads with AvgQC less than this quantile cutoff will not be used for 
        /// inexact read assignments by alignment
        /// </summary>
        public float QuantileOfAvgReadQualityCutoff { get; set; }

        
        public float MisMatchPenalty { get; set; }
        public float MatchScore { get; set; }
        public float GapStartPenalty { get; set; }
        public float GapExtendPenalty { get; set; }
        /// <summary>
        /// The name to output the file with
        /// </summary>
        public string OutputFileNamePrefix { get; set; }
        /// <summary>
        /// Determines how much information is printed to the screen
        /// </summary>
        public bool Verbose { get; set; }

        FastQParser FQP;
        BarCodeCollection BC;
        AlleleCollection AC;
        List<string> FileNames=new List<string>();
        public void SetFileNames(List<string> FNames)
        {
             foreach (string fn in FNames)
             {
                    if (!File.Exists(fn))
                        throw new IOException("File: " + fn + " cannot be found");
             }
             this.FQP = new FastQParser(FNames);
             if (Verbose)
             {
                 FQP.LogEvent += new LogEventHandler(ReceiveChildEvent);
             }
             FileNames.AddRange(FNames);
            //Now to parse through and attempt to get the relevant Statistics 
             List<FastQRead> firstReads = FQP.GetFirstReadsFromFile();
             var QCAvg = (from x in firstReads select x.AvgQuality).ToList();
             var percNAvg = (from x in firstReads select x.PercN).ToList();
             float avgQC = (float)QCAvg.Average();
             QCAvg.Sort();
             FireLogEvent("Average scaled QC values based on initial reads is: " + avgQC.ToString());
             int LowIndex = (int)(QuantileOfAvgReadQualityCutoff * (float)QCAvg.Count);
             MinAverageQualityForInexactMatches = (float)QCAvg[LowIndex];
             FireLogEvent("Requiring an average scaled read QC value of " + MinAverageQualityForInexactMatches.ToString("F") + " before attempting assignment based on alignment.");
            double avgN=percNAvg.Average();
            FireLogEvent("The average percentage of 'N' basepairs in initial reads is: " + avgN.ToString("F"));
           

        }
        public AlleleFinder(BarCodeCollection BCC, AlleleCollection AC,List<string> FNames=null)
        {
            this.BC = BCC;
            this.AC = AC;
            this.BC.AC = AC;
            foreach (BarCodeGroup BC in BCC.BarCodes)
            {
                BC.AssignParentCollection(AC);
            }

            this.BC.LogEvent += new LogEventHandler(ReceiveChildEvent);
            if (FNames != null)
            {
                SetFileNames(FNames);
            }
        }
        public void SetDefaultOptions()
        {
            this.AssignImperfectlyMatchedReads=true;
            this.InitialReadsToParse=10000;
            this.GapExtendPenalty = -1;
            this.GapStartPenalty = -2;
            this.MatchScore = 1;
            this.MisMatchPenalty = -2;
            this.QuantileOfAvgReadQualityCutoff = 0.02F;
            this.MaxPercentageNForInexactMatches=0.2F;
            this.OutputFileNamePrefix = "Results";
            this.AllowBarCodesWithinHamming1ToBeAssigned = true;
            this.RequireM13Sequence = true;
			this.MinReadLength = 75;
            this.AllowReadsWithM13WithinHamming1ToBeAssigned=true;
        }
        /// <summary>
        /// Register with this event to get update messages
        /// </summary>
        public event LogEventHandler LoggerEvent;
        public void FireLogEvent(string message)
        {
            if (LoggerEvent != null && Verbose)
            {
                LoggerEvent(this, message+"\n");
            }
        }
        /// <summary>
        /// Receives messages from AlleleCollection, FastQParser, etc. and passes them on to the main event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="msg"></param>
        public void ReceiveChildEvent(object sender, string msg)
        {
            FireLogEvent(msg);
        }
        public void MakeReport()
        {
            BC.MakeReport(this.OutputFileNamePrefix+".csv");
           // BC.MakeReport(outFileName,"");
        }
        /// <summary>
        /// Used to determine if the sequence contains an M13 Tag
        /// </summary>
        /// <param name="read"></param>
        /// <returns>Whether it does by the criteria</returns>
        private bool ContainsM13Sequence(FastQRead read)
        {
            string M13=read.Sequence.Substring(BARCODE_LENGTH, M13_SEQUENCE.Length);
            if(M13==M13_SEQUENCE)
            {return true;}
            if(AllowReadsWithM13WithinHamming1ToBeAssigned && BarCodeCollection.CalculateHammingDistance(M13_SEQUENCE,M13)<2)
            {return true;}
            return false;

        }
        /// <summary>
        /// The main work horse function, responsible for parsing the FASTQ files
        /// </summary>
        public void ParseFiles()
        {
            AC.FinishAndFreeze(this);
            BC.FinishAndFreeze();
#if DEBUG
            AlleleTypeAssigner ata = AC.SpawnTypeAssigner();
            foreach (StreamReader FR in FQP.GetStreamReaderForSequences())

           // Parallel.ForEach(FQP.GetStreamReaderForSequences(700000000), FR =>
#else
            Parallel.ForEach(FQP.GetStreamReaderForSequences(70000), FR =>
#endif

            {
                //First convert the lines of text (already in memory) to FastQReads;
                List<FastQRead> reads = FastQParser.GetFastQReadsFromStream(FR);
                //drop reference after conversion so GC can free memory
                FR.Dispose();
#if !DEBUG
                FR = null;
                AlleleTypeAssigner ata = AC.SpawnTypeAssigner();
#endif

                BarCodeAssigner bca = BC.SpawnBarCodeAssigner();
                //now loop through them
                int UnassignedReads = 0;
				int ReadsTooShort = 0;
                int NoM13Reads = 0;
                var CountingDictionary = BC.ReturnIdentifyingDictionary();
                int totalReads = reads.Count;
                foreach (FastQRead read in reads)
                {
					if (read.Sequence.Length < MinReadLength) {
						ReadsTooShort++;
						continue;
					}
                    if (RequireM13Sequence && !ContainsM13Sequence(read))
                    {
                        UnassignedReads++;
                        NoM13Reads++;
                        continue;
                    }
                    Assignment barCodeAssignment = bca.AssignToGroup(read);
                    if (barCodeAssignment.Group != AlleleFinder.unknownID)
                    {
                        Assignment typeAssignment = ata.AssignReadToType(read);
                        //now match the assignment
                        var toUpdate = CountingDictionary[barCodeAssignment.Group];
                        var Counter = toUpdate[typeAssignment.Group];
                        if (typeAssignment.ExactAssignment)
                        {
                            Counter.totalExactAssignments++;
                            Counter.totalExactAvgQualityScore += read.AvgQuality;
                        }
                        else
                        {
                            Counter.totalInexactAssignments++;
                            Counter.totalInexactAvgQualityScore += read.AvgQuality;
                        }
                    }
                    else
                    {
                        UnassignedReads++;
                    }
                }
                //Now To update
				BC.AddIdentifyingDictionary(CountingDictionary, UnassignedReads, totalReads, NoM13Reads,ReadsTooShort);
#if DEBUG
                
            }
            ata.tmp.report();
#else
                }
                );
#endif
            
           
          
            //Program has a bad tendency to let stuff sit around eating up memory
            GC.Collect();
        }
    }
}
