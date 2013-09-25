import re
"""This file generates XML for the options settings tag based on some C# Code"""

typeToXML={ "float":"System.Single",
            "double":"System.Double",
            "bool":"System.Boolean",
            "string":"System.String",
            "int":"System.Int32"}

fieldRE=re.compile(r'((bool)|(float)|(double)|(string)|(int))\s+([^\s]*)')

txt="""        
public const string unknownID = "UNKNOWN";
        //length of the barcode sequence, should always be 6
        public const int BARCODE_LENGTH = 6;
        public const int ALLELE_START_POS = 23;
        /// <summary>
        /// Should reads be quality filtered?
        /// </summary>
        public bool QualityFilter { get; set; }
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

        private SimpleSubstitutionMatrix SubMat;
        public float MisMatchPenalty { get; set; }
        public float MatchScore { get; set; }
        public float GapStartPenalty { get; set; }
        public float GapExtendPenalty { get; set; }
        /// <summary>
        /// The name to output the file with
        /// </summary>
        public string OutputFileName { get; set; }
        /// <summary>
        /// Determines how much information is printed to the screen
        /// </summary>
        public bool Verbose { get; set; }
public int InitialReadsToParse { get; set; }
        """

for match in fieldRE.findall(txt):
    t=match[0]
    val=match[-1]
    nodeName=val
    print "<"+nodeName+' Type="'+typeToXML[t]+'"> </'+nodeName+">"
    