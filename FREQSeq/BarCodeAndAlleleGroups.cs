//#define DEBUG 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace FREQSeq
{
	/// <summary>
	/// A collection of barcodes used in the analysis
	/// 
	/// Also contains methods for assigning strings to groups
	/// </summary>
	public sealed class BarCodeCollection
	{
		public readonly SimpleSubstitutionMatrix BarCodeSubstitutionMatrix;
		public List<BarCodeGroup> BarCodes = new List<BarCodeGroup> ();
		private bool Frozen;
		public long UnAssignedReadCount;
		public long TooShortCount;
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		public long TotalReads;
        public long LastReportValue = 1;
		public long M13_Excluded;
		private object ReadCountLock = new object ();
		public List<string> AllBarCodes = new List<string> ();
		public LocusCollection AC;

		public event LogEventHandler LogEvent;

		/// <summary>
		/// Determines if we can take non-exact matches, this must be equal to true and the 
		/// number of exact matches 
		/// </summary>
		public bool RequireExactMatchForAssignment = false;

		private void FireLogEvent (string msg)
		{
			if (LogEvent != null) {
				LogEvent (this, msg);
			}
		}

		public BarCodeCollection ()
		{
			BarCodeSubstitutionMatrix = new SimpleSubstitutionMatrix (2, -1, -4, -2);
            sw.Start();
		}

		public void AddBarCodeGroup (BarCodeGroup BCG)
		{
			if (Frozen)
				throw new Exception ("Tried to add Barcode to collection after it was frozen.");
			if (this.AllBarCodes.Contains (BCG.Identifier))
				throw new IOException ("Tried to add the same barcode twice: " + BCG.Identifier);
			else {
				this.BarCodes.Add (BCG);
				this.AllBarCodes.Add (BCG.Identifier);
			}
		}

		public Dictionary<string, Dictionary<string,AssignmentResults>> ReturnIdentifyingDictionary ()
		{
			Dictionary<string,  Dictionary<string,AssignmentResults>> toRetu = new Dictionary<string, Dictionary<string,AssignmentResults>> ();
			foreach (BarCodeGroup BCG in BarCodes) {
				toRetu [BCG.Identifier] = BCG.CreateAlleleCountDictionary ();
			}
			return toRetu;
		}

		private BarCodeAssigner mainBCA;

		public BarCodeAssigner SpawnBarCodeAssigner ()
		{
			//seperate spawning was a bit slower
			if (mainBCA == null) {
				string[] AllBarCodes = this.AllBarCodes.ToArray ();
				mainBCA = new BarCodeAssigner (AllBarCodes, !this.RequireExactMatchForAssignment);
			}
			return mainBCA;
			//string[] AllBarCodes=this.AllBarCodes.ToArray();
			//BarCodeAssigner bca=new BarCodeAssigner(AllBarCodes,this.RequireExactMatchForAssignment);
			//return bca;
		}

		public void AddIdentifyingDictionary (Dictionary<string,Dictionary<string,AssignmentResults>> toAdd, int unassignedcount, int TotalProcessedCount, int noM13, int tooShortCount)
		{
			lock (ReadCountLock) {
				UnAssignedReadCount += unassignedcount;
				TotalReads += TotalProcessedCount;
				M13_Excluded += noM13;
				TooShortCount += tooShortCount;
                if (TotalReads / 500000 > LastReportValue)
                {
                    sw.Stop();
                    LastReportValue++;
                    Console.WriteLine("Processed: " + TotalReads.ToString() + " reads.");
                    Console.WriteLine("Time elapsed for batch: {0}", sw.Elapsed);
                    sw.Reset();
                    sw.Start();
                }
			}
			foreach (KeyValuePair<string, Dictionary<string,AssignmentResults>> set in toAdd) {
				var BCG = (from x in BarCodes
				                       where x.Identifier == set.Key
				                       select x).First ();
				BCG.AddAssignmentResults (set.Value);
			}
		}

		private sealed class OutputColumn
		{
			public readonly string Name;
			public readonly Func<BarCodeGroup, string> outFunc;

			public OutputColumn (string Name, Func<BarCodeGroup, string> OutputFunction)
			{
				this.Name = Name;
				this.outFunc = OutputFunction;
			}
		}

		public void MakeReport (string outputFileName)
		{
			FireLogEvent ("Writing output file: " + outputFileName);
			StreamWriter SW = new StreamWriter (outputFileName);
			SW.WriteLine ("Total Reads," + TotalReads.ToString ());
			SW.WriteLine ("Reads Too Short For Consideration," + TooShortCount.ToString ());
			SW.WriteLine ("Total Reads Not Assigned to Barcodes," + UnAssignedReadCount.ToString ());
			SW.WriteLine ("Percentage Not Assigned to Barcodes," + (UnAssignedReadCount / (double)TotalReads).ToString ());
			SW.WriteLine ("Reads Excluded for no M13," + M13_Excluded.ToString ());
			var unAssignedWithin = BarCodes.Select (x => x.TotalUnassignedReads).Sum ();
			SW.WriteLine ("Total Reads Not Assigned within Barcodes," + unAssignedWithin.ToString ());
			SW.WriteLine ("Total Percentage Unassigned, " + ((unAssignedWithin + UnAssignedReadCount) / (double)TotalReads).ToString ());
            
			SW.WriteLine ();
			string toOut = "";
			toOut += "Read Assignment Counts By Barcode\n";
			//Create output functions
			List<OutputColumn> Cols = new List<OutputColumn> () {
				new OutputColumn ("Barcode", x => x.Identifier),
				new OutputColumn ("Total Reads Assigned to Barcode", x => x.TotalReadsAssigned.ToString ()),
				new OutputColumn ("Total Reads Unassigned to Alleles in Barcode", x => x.AlleleCounts [AlleleFinder.unknownID].totalInexactAssignments.ToString ()),
				new OutputColumn ("Percentage UnAssigned", x => (x.TotalUnassignedReads / (double)x.TotalReadsAssigned).ToString ()),
				new OutputColumn ("Avg QC Score", x => x.AvgAllAssignedReadQuality.ToString ()),
				new OutputColumn ("Avg Exact QC Score", x => x.AvgExactAssignedReadQuality.ToString ()),
				new OutputColumn ("Avg Inexact QC Score", x => x.AvgInExactAssignedReadQuality.ToString ()),
				new OutputColumn ("Avg Unassigned QC Score", x => x.AvgUnassignedReadQCScore.ToString ()),
				new OutputColumn ("Exactly Assigned Reads", x => x.CountofReadsExactlyAssigned.ToString ()),
				new OutputColumn ("Inexactly Assigned Reads", x => (x.CountofReadsNotExactlyAssigned - x.TotalUnassignedReads).ToString ())
			};
			foreach (OutputColumn c in Cols) {
				SW.Write (c.Name + ",");
			}
			SW.Write ("\n");
			foreach (BarCodeGroup BCG in BarCodes) {
				foreach (OutputColumn c in Cols) {
					SW.Write (c.outFunc (BCG) + ",");
				}
				SW.Write ("\n");
			}
			SW.Write ("\n");
			//Now for each of the allele groups
			AC.WriteReport (SW, this);
			SW.Close ();

		}

		/// <summary>
		/// This method is called once all barcodes are loaded, it determines if the barcodes 
		/// are far enough apart to allow inexact matches
		/// </summary>
		public void FinishAndFreeze ()
		{
			Frozen = true;
			if (AC == null) {
				throw new Exception ("Tried to freeze barcode collection before specifying allele collection");
			}
			//First to determine the minimum hamming distance between a given set of sequences is greater than 2
			//do all N choose 2 combinations to check this, if so we will except barcodes within a hamming distance of 1.
			int[] Difs = new int[(AllBarCodes.Count * (AllBarCodes.Count - 1)) / 2];
			int curValue = 0;

			for (int i = 0; i < AllBarCodes.Count; i++) {
				for (int j = (i + 1); j < AllBarCodes.Count; j++) {
					Difs [curValue] = CalculateHammingDistance (AllBarCodes [i], AllBarCodes [j]);
					curValue++;
				}
			}
			int maxDif = Difs.Min ();
			string msg = "Minimum hamming distance between barcodes is " + maxDif.ToString ();
			if (maxDif >= 2) {
				RequireExactMatchForAssignment = false;
				msg += ". Assigning barcodes if within a hamming distance of 1.";
			} else {
				RequireExactMatchForAssignment = true;
				msg += ". Not accepting any inexact barcode matches.";
			}
			FireLogEvent (msg);
            
		}

		public static int CalculateHammingDistance (string seq1, string seq2)
		{
			int difs = 0;
			for (int i = 0; i < seq1.Length; i++) {
				if (seq1 [i] != seq2 [i])
					difs += 1;
			}
			return difs;

		}
	}

	public struct Assignment
	{
		/// <summary>
		/// Was this based on an exact match?
		/// </summary>
		public bool ExactAssignment;
		/// <summary>
		/// The group it was assigned to
		/// </summary>
		public string Group;

		public Assignment (string group, bool exactMatch)
		{
			this.Group = group;
			this.ExactAssignment = exactMatch;
		}
	}

	public sealed class AssignmentResults
	{
		public long totalExactAssignments;
		public long totalInexactAssignments;
		/// <summary>
		/// The sum of all the avg quality scores for each read
		/// </summary>
		public double totalExactAvgQualityScore;
		public double totalInexactAvgQualityScore;

		public long totalAssignments {
			get { return totalInexactAssignments + totalExactAssignments; }
		}
	}

	/// <summary>
	/// A class that each thread can get an instance of, it 
	/// will assign each 
	/// </summary>
	public sealed class BarCodeAssigner
	{
		public readonly string[] BarCodes;
		public readonly bool UseInExactMatches;
		public readonly HashSet<string> hBarCodes;

		public BarCodeAssigner (string[] groups, bool AttemptInexactMatches)
		{
			hBarCodes = new HashSet<string> (groups);
			BarCodes = groups;
			UseInExactMatches = AttemptInexactMatches;
		}

		public Assignment AssignToGroup (FastQRead read)
		{
			string bc = read.Sequence.Substring (0, AlleleFinder.BARCODE_LENGTH);
			if (hBarCodes.Contains (bc))
				return new Assignment (bc, true);
			else {
				if (!UseInExactMatches) {
					return new Assignment (AlleleFinder.unknownID, false);
				} else {
					//Assign anything with a hamming distance of 1
					foreach (string str in BarCodes) {
						if (BarCodeCollection.CalculateHammingDistance (str, bc) == 1)
							return new Assignment (str, false);
					}
				}
			}
			return new Assignment (AlleleFinder.unknownID, false);
		}
	}

	/// <summary>
	/// A class that holds data from all samples with a 
	/// particular barcode used in the analysis
	/// </summary>
	public sealed class BarCodeGroup
	{
		public readonly string Identifier;
		public Dictionary<string, AssignmentResults> AlleleCounts;
		public LocusCollection alleleCollection;

		/// <summary>
		/// Create a new barcode group
		/// </summary>
		/// <param name="Barcode">The string in the Barcode</param>
		/// <param name="Name">An alias for the group name - NOT YET IMPLEMENTED</param>
		public BarCodeGroup (string Barcode, string Name = "None")
		{
			if (Barcode.Length != AlleleFinder.BARCODE_LENGTH) {
				throw new IOException ("Barcode " + Barcode + " is not the correct length, check the XML file.");
			}
			MiscMethods.ValidateACGT (Barcode);
			this.Identifier = Barcode;            
		}

		public void AssignParentCollection (LocusCollection ac)
		{
			this.alleleCollection = ac;
			this.AlleleCounts = CreateAlleleCountDictionary ();
            
		}

		private long pTotalReadsAssigned = -1;

		public long TotalReadsAssigned {
			get {
				if (pTotalReadsAssigned == -1) {
					var Values = AlleleCounts.Values;
					pTotalReadsAssigned = 0;
					foreach (var v in Values) {
						pTotalReadsAssigned += (long)v.totalExactAssignments + (long)v.totalInexactAssignments;
					}
				}
				return pTotalReadsAssigned;
			}
		}

		public long TotalUnassignedReads {
			get { return this.AlleleCounts [AlleleFinder.unknownID].totalInexactAssignments; }
		}

		private long pExactCount = -1;

		public long CountofReadsExactlyAssigned {
			get {
				if (pExactCount == -1) {
					var Values = AlleleCounts.Values;
					pExactCount = 0;
					foreach (var v in Values) {
						pExactCount += (long)v.totalExactAssignments;
					}
				}
				return pExactCount; 
			}
		}

		public double AvgAllAssignedReadQuality {
			get {
				var Values = AlleleCounts.Values;
				long Count = 0;
				double curSum = 0;

				foreach (var v in Values) {
					curSum += v.totalExactAvgQualityScore + v.totalInexactAvgQualityScore;
					Count += (long)v.totalExactAssignments + v.totalInexactAssignments;
				}
				return curSum / (double)Count;
				return Count; 
			}   
		}

		public double AvgUnassignedReadQCScore {
			get {
				var res = AlleleCounts [AlleleFinder.unknownID];
				return res.totalInexactAvgQualityScore / (double)res.totalInexactAssignments;
			}
		}

		public double AvgExactAssignedReadQuality {
			get {
				var Values = AlleleCounts.Values;
				long Count = 0;
				double curSum = 0;

				foreach (var v in Values) {
					curSum += v.totalExactAvgQualityScore;
					Count += (long)v.totalExactAssignments;
				}
				return curSum / (double)Count;
			}
		}

		const double UNASSIGNED_VALUE = -1.0;
		private double pAvgInExactAssignedReadQuality = UNASSIGNED_VALUE;

		public double AvgInExactAssignedReadQuality {
			get {
				if (pAvgInExactAssignedReadQuality == UNASSIGNED_VALUE) {

					long Count = 0;
					double curSum = 0;
					foreach (KeyValuePair<string, AssignmentResults> v in AlleleCounts) {
						if (v.Key == AlleleFinder.unknownID)
							continue;
						curSum += v.Value.totalInexactAvgQualityScore;
						Count += (long)v.Value.totalInexactAssignments;
					}
					pAvgInExactAssignedReadQuality = curSum / (double)Count;
				}
				return pAvgInExactAssignedReadQuality;
			}
		}

		public long CountofReadsNotExactlyAssigned {
			get {
				var Values = AlleleCounts.Values;
				long Count = 0;
				foreach (var v in Values) {
					Count += (long)v.totalInexactAssignments;
				}
				return Count;
			}
		}

		public Dictionary<string,AssignmentResults> CreateAlleleCountDictionary ()
		{
			List<string> outputs = alleleCollection.AllSequences.ToList ();
			outputs.Add (AlleleFinder.unknownID);
			Dictionary<string,AssignmentResults> toReturn = new Dictionary<string,AssignmentResults> (outputs.Count);
			foreach (string str in outputs) {
				toReturn [str] = new AssignmentResults ();
			}
			return toReturn;
		}

		public void AddAssignmentResults (Dictionary<string, AssignmentResults> toAdd)
		{
			lock (this.AlleleCounts) {
				foreach (KeyValuePair<string,AssignmentResults> bit in toAdd) {
					var v = AlleleCounts [bit.Key];
					v.totalInexactAvgQualityScore += bit.Value.totalInexactAvgQualityScore;
					v.totalInexactAssignments += bit.Value.totalInexactAssignments;
					v.totalExactAssignments += bit.Value.totalExactAssignments;
					v.totalExactAvgQualityScore += bit.Value.totalExactAvgQualityScore;
				}
			}
		}
	}

	public sealed class LocusCollection
	{
        public List<string> AllSequences
        {
            get {
                return Loci.SelectMany(x => x.Alleles).ToList();
            }
        }
		public List<Locus> Loci = new List<Locus> ();
		public Dictionary<string,List<TypeToAlign>> HashedKMERS;
		public SimpleSubstitutionMatrix SubMatForAlignment;

        private int pMaxAllowableSize;
        public int MaxReadSize
        {
            get { return pMaxAllowableSize; }
        }
		public float MinAverageQCScoreForInexactMatches;
		public float MaxNPercForInexactMatches;
		public bool AttemptInExactMatches;
		private bool Frozen;

        /// <summary>
        /// The class the holds the locus and the alleles present at that locus 
        /// </summary>
		public class Locus
		{
            /// <summary>
            /// This will be used to determine if this is a SNP variant or not.
            /// SNP variants have only two members, and are distinguished by a simple difference.
            /// </summary>
            public bool IsSNPVariant = false;

			/// <summary>
			/// The types of genetic variants present (usually only 2);
			/// </summary>
			public List<string> Alleles = new List<string> ();

			public Locus ()
			{
			}

			public void AddTypes (List<string> types)
			{
 				// Verify equal length requirement.
                for (int i = 0; i < types.Count; i++) {
                    for (int j = (i + 1); j < types.Count; j++) {
                        if (types [j].Length != types [i].Length) {
                            throw new IOException ("Two types have different lengths, which can skew assignment results.  Please make all variants at a " +
                            "locus have the same input length.  Problems with: \t" + types [j] + "\n" + types [i]);
                        }
                        if (types [j] == types [i]) {
                            throw new IOException("Two variants are the same, please fix this:\n " + types[j] +"\n"+ types[i]);
                        }
                    }
                }  
                this.Alleles.AddRange(types);
                if (Alleles.Count == 2 && AlleleTypeAssigner.GetHammingDistance(Alleles[0], Alleles[1]) == 1)
                {
                    IsSNPVariant = true;
                }
			}
		}
		public LocusCollection ()
		{
		}

		public void AddLocus (Locus toAdd)
		{
			if (Frozen)
				throw new Exception ("Tried to add allele after collection was frozen");
			foreach (string type in toAdd.Alleles) {
				foreach (string alr in this.AllSequences) {
					if (alr.StartsWith (type)) {
						throw new IOException ("Type: " + type + " appears in different variants!");
					}
				}
			}
			this.Loci.Add (toAdd);
			this.AllSequences.AddRange (toAdd.Alleles);
		}

		public void WriteReport (StreamWriter SW, BarCodeCollection BC)
		{
			int SetCount = 1;
			foreach (Locus A in Loci) {
				SW.WriteLine ("Allele Group " + SetCount.ToString ());
				int NumTypes = A.Alleles.Count;
				int i = 1;
				string header = "Barcode,";
				foreach (string t in A.Alleles) {
					SW.WriteLine ("Type " + i.ToString () + ": " + t);
					header += "Type " + i.ToString () + "%,";
					i++;
				}
				header += "Total Allele Counts";
				SW.WriteLine (header);
				foreach (BarCodeGroup BCG in BC.BarCodes) {
					ulong[] counts = new ulong[NumTypes];
					int j = 0;
					foreach (var type in A.Alleles) {
						counts [j] = (ulong)BCG.AlleleCounts [type].totalAssignments;
						j++;

					}
					SW.Write (BCG.Identifier + ",");
					double total = (double)counts.Sum (x => (int)x);
					for (j = 0; j < NumTypes; j++) {
						SW.Write ((counts [j] / total).ToString () + ",");
					}
					SW.Write (((int)total).ToString () + "\n");
  
				}
				SW.WriteLine ();
				SetCount++;
			}
		}

		public void FinishAndFreeze (AlleleFinder parentAF)
		{
			Frozen = true;
			this.AttemptInExactMatches = parentAF.AssignImperfectlyMatchedReads;
			this.SubMatForAlignment = new SimpleSubstitutionMatrix (parentAF.MatchScore, parentAF.MisMatchPenalty, parentAF.GapStartPenalty, parentAF.GapExtendPenalty);
			this.MaxNPercForInexactMatches = parentAF.MaxPercentageNForInexactMatches;
			this.MinAverageQCScoreForInexactMatches = parentAF.MinAverageQualityForInexactMatches;
			HashedKMERS = new Dictionary<string, List<TypeToAlign>> ();
            foreach (Locus lc in Loci)
            {
                foreach (string str in lc.Alleles)
                {
                    //Figure out minimum score required, harcoded at 75% of perfect

                    float MinScoreRequired = SubMatForAlignment.MatchScore * str.Length * .75F;// +SubMatForAlignment.gapExistPenalty + SubMatForAlignment.gapExistPenalty + SubMatForAlignment.MisMatchScore;
                    TypeToAlign t = new TypeToAlign(str, MinScoreRequired,lc);
                    string[] mers = AlleleTypeAssigner.CreateKMERS(str);
                    foreach (string mer in mers)
                    {
                        if (HashedKMERS.ContainsKey(mer))
                        {
                            HashedKMERS[mer].Add(t);
                        }
                        else
                        {
                            HashedKMERS[mer] = new List<TypeToAlign>() { t };
                        }
                    }
                }
            }
            pMaxAllowableSize = AllSequences.Max(x => x.Length) + 10;
		}
		/// <summary>
		/// Try to put memory near the rest of what the thread is working on by spawing off a type
		/// </summary>
		public AlleleTypeAssigner SpawnTypeAssigner ()
		{
			//Seperate spawing turned out to be slower
			//return new AlleleTypeAssigner(this);
			if (mainATA == null)
				mainATA = new AlleleTypeAssigner (this);
			return mainATA;
		}

		private AlleleTypeAssigner mainATA;
	}

	public sealed class TypeToAlign
	{
		public readonly float MinScoreForAssignment;
		public readonly string TypeSeq;
        public readonly LocusCollection.Locus Locus;
        public TypeToAlign (string seq, float minScoreRequired, LocusCollection.Locus parentLocus)
		{
			this.MinScoreForAssignment = minScoreRequired;
			this.TypeSeq = seq;
            Locus = parentLocus;
		}
	}

	public sealed class AlleleTypeAssigner
	{
		//Kmers for reduced word search
		public const int KMER_SIZE = 9;
		public readonly float MinScoreDifferenceRequired;
		/// <summary>
		/// Maps K-mers to possible sequences, used to avoid trying to align to all possible 
		/// sequences
		/// </summary>
		public readonly Dictionary<string,List<TypeToAlign>> HashedKMERS;
		private readonly float MinAvgQCScoreToAttemptInexactMatch;
		private readonly bool AssignInexactMatches;
		private readonly float MaxNPercentageToAttemptInExactMatch;
		private readonly string[] AllTypes;
		private readonly SimpleSubstitutionMatrix subMat;
        /// <summary>
        /// If the reads are much larger than the alleles, we trim them down.
        /// </summary>
        private readonly int MaxAllowableSequenceLength;

		public AlleleTypeAssigner (LocusCollection parentLC)
		{
            this.MaxAllowableSequenceLength = parentLC.MaxReadSize;
			this.AssignInexactMatches = parentLC.AttemptInExactMatches;
			//not deep copying type to align, not sure what, if any, performace implications this has
			this.HashedKMERS = parentLC.HashedKMERS.ToDictionary (x => x.Key, x => x.Value);
			this.MaxNPercentageToAttemptInExactMatch = parentLC.MaxNPercForInexactMatches;
			this.MinAvgQCScoreToAttemptInexactMatch = parentLC.MinAverageQCScoreForInexactMatches;
			this.subMat = parentLC.SubMatForAlignment.Clone ();
			this.AllTypes = parentLC.AllSequences.ToArray ();
			//make minimum dif equal to a SNP
			this.MinScoreDifferenceRequired = this.subMat.MatchScore - this.subMat.MisMatchScore;
		}
		#if DEBUG
		public TEMPCountDict tmp = new TEMPCountDict ();

		public class TEMPCountDict
		{
			int tots = 0;
			Dictionary<string, int> Counts = new Dictionary<string, int> ();

			public void addSeq (string seq)
			{
				tots++;
				int cur = 0;
				if (Counts.ContainsKey (seq)) {
					cur = Counts [seq];
				}
				Counts [seq] = cur + 1;
				if (tots > 100000) {
					int jjj = 1;
					jjj++;
                   
				}
			}

			public void report ()
			{
				StreamWriter SQ = new StreamWriter ("Tmp.csv");
				foreach (KeyValuePair<string, int> kvin in Counts) {
					SQ.WriteLine (kvin.Key + "," + kvin.Value.ToString ());
				}
				SQ.Close ();
				Console.WriteLine ("Outted");
			}
		}
		#endif
		public Assignment AssignReadToType (FastQRead read)
		{
			//get sequence
			string seq = read.Sequence.Substring (AlleleFinder.ALLELE_START_POS);
            if (seq.Length > MaxAllowableSequenceLength) { seq = seq.Substring(0, MaxAllowableSequenceLength); }
			//try for exact match 
			foreach (string s in AllTypes) {
				if (seq.StartsWith (s)) {
					return new Assignment (s, true);
				}
			}
			//Are we going to assign inexact matches?
			if (AssignInexactMatches) {
				//Make sure quality is high enough to even bother with an inexact match
				if (read.AvgQuality >= MinAvgQCScoreToAttemptInexactMatch && read.PercN <= MaxNPercentageToAttemptInExactMatch) {
					//Get KMERS
					string[] kmers = CreateKMERS (seq);
					//Use kmers to get small set to align to
                    Dictionary<TypeToAlign, int> countingDict = new Dictionary<TypeToAlign, int>();
					foreach (string mer in kmers) {
						List<TypeToAlign> att;
						if (HashedKMERS.TryGetValue (mer, out att)) {
							foreach (var at in att) {
                                if (countingDict.ContainsKey(at)) {
                                    countingDict[at] += 1;
                                }
                                else {
                                    countingDict[at] = 1;
                                }
							}
						}
					}
                    kmers = null;
                    //Now decide between options based on counts.
                    //Compare based on scores and alignment.
                    var possibles=countingDict.ToList();
                    if (possibles.Count == 0)
                    {
                        return new Assignment(AlleleFinder.unknownID, false);
                    }
                    possibles.Sort((x, y) => -x.Value.CompareTo(y.Value));
                  
                    //have to have at least 25% as many k-mer matches as top hit.
                    int topKmerMatchCountCutoff=(int)(possibles[0].Value*0.25);
                    var toAttempt = possibles.Where(z => (z.Value >= topKmerMatchCountCutoff)).ToList();
                    
                    //TODO: Remove after experimental verification
                    if (possibles[0].Key != toAttempt[0].Key)
                    {
                        throw new InvalidOperationException("The best k-mer hit was not included as the top hit to attempt an inexact assignment.  This is "
                        + " a program bug, please report it to Nigel.");
                    }
                    possibles = null;
                        //If 1, see if the kmers indicate it is good enough, and if not, try an ungapped alignment
                        if (toAttempt.Count == 1)
                        {
                            var cur=toAttempt[0].Key;
                            {
                                if (
                                    (GetUnGappedAlignmentScore(seq,cur.TypeSeq) >= cur.MinScoreForAssignment ) ||
                                    (ScoreOnlySmithWatermanGotoh.GetSmithWatermanScore(cur.TypeSeq,seq,subMat) >= cur.MinScoreForAssignment))
                                {
                                    return new Assignment(cur.TypeSeq, false);
                                }
                                else
                                {
                                    return new Assignment(AlleleFinder.unknownID, false);
                                }                                
                            }
                        }
                        //see if it is a simple SNP, this means the locus (and kmer counts) are entirely the same except at that base
                        if (toAttempt.Count == 2 && toAttempt[0].Key.Locus == toAttempt[1].Key.Locus
                            && toAttempt[0].Key.Locus.IsSNPVariant)
                        {
                            //let's just do a simple check to make sure we don't assign total garbage, min score has
                            //to be greater than 50% of max score
                            var best = toAttempt[0].Key;
                            var perfectScore = subMat.MatchScore * Math.Min(seq.Length, best.TypeSeq.Length);
                            var mustBeat = best.MinScoreForAssignment;
                            if (
                                (GetUnGappedAlignmentScore(best.TypeSeq, seq) >= mustBeat)
                                || (ScoreOnlySmithWatermanGotoh.GetSmithWatermanScore(best.TypeSeq, seq, subMat) >= mustBeat))
                            {
                                return new Assignment(best.TypeSeq, false);
                            }
                            else
                            {
                                return new Assignment(AlleleFinder.unknownID, false);
                            }
                        }
                        else
                        {
                            //Otherwise time consuming pairwise global alignment
                            var Res = (from x in toAttempt
                                       select new { type = x.Key, score = ScoreOnlySmithWatermanGotoh.GetSmithWatermanScore(x.Key.TypeSeq, seq, subMat) }).ToList();
                            if (Res.Count > 1)
                            {
                                Res.Sort((x, y) => -x.score.CompareTo(y.score));
                                var top = Res[0];
                                float scoreDif = top.score - Res[1].score;
                                //check that it is much better than the last one
                                if (scoreDif >= MinScoreDifferenceRequired && top.score>top.type.MinScoreForAssignment)
                                {
                                    return new Assignment(top.type.TypeSeq, false);
                                }
                                else{
                                    return new Assignment(AlleleFinder.unknownID, false);
                                }
                            }
                        }
				}
			}
			return new Assignment (AlleleFinder.unknownID, false);
		}
        public float GetUnGappedAlignmentScore(string seq1, string seq2)
        {
            var size = Math.Min(seq1.Length, seq2.Length);
            int dist = 0;
            for (int i = 0; i < size; i++)
            {
                if (seq1[i] != seq2[i]) dist++;
            }
            return (size - dist) * subMat.MatchScore + dist * subMat.MisMatchScore;

        }



        /// <summary>
        /// Gets the minimimum score based on the number of k-mers matching. Assuming all kmer hits are a in acontinguous line
        /// </summary>
        /// <param name="kmerBasedMatch"></param>
        /// <returns></returns>
        private float GetMinScore(KeyValuePair<TypeToAlign,int> kmerBasedMatch,int queryLength)
        {
            //Assume the lowest is all matches and the rest is a mismatch 
            //The most possible kmer hits
            var alnLength=Math.Min(kmerBasedMatch.Key.TypeSeq.Length, queryLength);
            int actualMers =Math.Max(kmerBasedMatch.Value, alnLength - KMER_SIZE + 1);
            var minBPHit = actualMers + KMER_SIZE - 1;
            var maxBPMissed = alnLength-minBPHit;            
            var minHitScore = minBPHit * subMat.MatchScore + (maxBPMissed) * subMat.MisMatchScore;
            return minHitScore;
        }
        /// <summary>
        /// Gets the maximum score based on the number of k-mers matching
        /// </summary>
        /// <param name="kmerBasedMatch"></param>
        /// <returns></returns>
        private float GetMaxScore(KeyValuePair<TypeToAlign, int> kmerBasedMatch,int queryLength)
        {
            //every gap or mismatch introduces a penalty of one, which would result in missing a 
            //a number of kmers equal to the kmer size
            var minPenalty = Math.Max(subMat.gapExistPenalty, subMat.MisMatchScore);
            var alnLength = Math.Min(kmerBasedMatch.Key.TypeSeq.Length, queryLength);
            var missedHits =((alnLength-KMER_SIZE+1) - kmerBasedMatch.Value)/KMER_SIZE;
            var maxScore = (alnLength-missedHits)* subMat.MatchScore + missedHits * minPenalty;
            return maxScore;
        }
		public static string[] CreateKMERS (string seq)
		{
			int totalMers = seq.Length - KMER_SIZE + 1;
			if (totalMers > 0) {
				string[] to = new string[totalMers];
				for (int i = 0; i < to.Length; i++) {
					to [i] = seq.Substring (i, KMER_SIZE);
				}
				return to;
			} else {
				Console.WriteLine ("Failure to hash: " + seq);
				throw new Exception ("Tried to hash read of length: " + seq.Length.ToString () + " into KMERS of size " + KMER_SIZE.ToString ());
			}
		}

        public static int GetHammingDistance(string seq1, string seq2) {
            int dist = 0;
            for (int i = 0; i < seq1.Length; i++)
            {
                if (seq1[i] != seq2[i]) dist++;
            }
            return dist;
        }
	}
}
