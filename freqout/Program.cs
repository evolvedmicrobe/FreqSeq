using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NDesk.Options;
using FREQSeq;
using System.IO;
using System.Globalization;
using System.Diagnostics;

namespace freqout
{
    sealed class Program
	{
        private const double KB = 1024;
		private const double MB = KB * KB;
		private const double GB = MB * KB;
		static List<string> FileNames = new List<string> ();
		static bool SetVerboseAfterLoad = false;
        /// <summary>
        /// Used to manually override the output file name.  If not String.Empty, we change the name.
        /// </summary>
        static string OutputNameAfterLoad = String.Empty;
		static string XMLFilename;

		static void Main (string[] args)
		{
			Stopwatch sw = new Stopwatch ();
			sw.Start ();
			ParseCommandLine (args);
			VerifySettings ();
			RunAnalysis ();
			Process p = Process.GetCurrentProcess ();
			Console.WriteLine ("Peak Memory used " + FormatMemorySize (p.PeakWorkingSet64));
			Console.WriteLine ("Total CPU time taken: {0}", p.TotalProcessorTime);
			sw.Stop ();
			// Get the elapsed time as a TimeSpan value.
			TimeSpan ts = sw.Elapsed;

			// Format and display the TimeSpan value. 
			string elapsedTime = String.Format ("{0:00}:{1:00}:{2:00}.{3:00}",
				                     ts.Hours, ts.Minutes, ts.Seconds,
				                     ts.Milliseconds / 10);
			Console.WriteLine ("Clock Run Time: " + elapsedTime);
		}

		static void VerifySettings ()
		{
			try {
				if (XMLFilename == null || XMLFilename == "")
					throw new IOException ("XML file not set, be sure to use -xml= flag");
				if (FileNames.Count == 0) {
					throw new IOException ("No FASTQ files specified for analysis");
				}
			} catch (Exception thrown) {
				Console.WriteLine ("Error: Could not verify settings");
				Console.WriteLine ("Exception is: " + thrown.Message);
                Console.WriteLine ("Stack Trace is: " + thrown.StackTrace);
				System.Environment.Exit (-1);
			}
		}

		private static string FormatMemorySize (long value)
		{
			string result = null;
			if (value > GB) {
				result = (Math.Round (value / GB, 2)).ToString (CultureInfo.InvariantCulture) + " GB";
			} else if (value > MB) {
				result = (Math.Round (value / MB, 2)).ToString (CultureInfo.InvariantCulture) + " MB";
			} else if (value > KB) {
				result = (Math.Round (value / KB, 2)).ToString (CultureInfo.InvariantCulture) + " KB";
			} else {
				result = value.ToString (CultureInfo.InvariantCulture) + " Bytes";
			}

			return result;
		}

		static void SetXMLFile (string fname)
		{
			try {
				if (!File.Exists (fname)) {
					throw new IOException ("Could not find XML file:" + fname);
				} else if (!fname.EndsWith (".xml")) {
					throw new IOException ("XML file: " + fname + "\nDoes not have a .xml extension");
				} else {
					Program.XMLFilename = fname;
				}
			} catch (Exception thrown) {
				Console.WriteLine ("Error: Could not get XMLfile");
				Console.WriteLine ("Exception is: " + thrown.Message);
				System.Environment.Exit (-1);
			}
		}

		static void LoadFastQForDirectory (string direc)
		{
			try {
				DirectoryInfo DI = new DirectoryInfo (direc);
				bool FileAdded = false;
				foreach (FileInfo FI in DI.GetFiles()) {
					if (FI.Extension == ".fastq" || FI.Extension == FREQSeq.Helper.ZippedFileExtension) {
						FileNames.Add (FI.FullName);
						FileAdded = true;
					}
				}
				if (!FileAdded) {
					throw new IOException ("Could not find any files with extension .fastq in the directory");
				}
			} catch (Exception thrown) {
				Console.WriteLine ("Error: Could not get FASTQ files from directory: ");
				Console.WriteLine (direc);
				Console.WriteLine ("Exception is: " + thrown.Message);
				System.Environment.Exit (-1);
			}
		}

		static void AddFileToList (string fname)
		{
			try {
				if (!File.Exists (fname)) {
					throw new IOException ("Could not find file:" + fname);
				} else {
					FileNames.Add (fname);
				}
			} catch (Exception thrown) {
				Console.WriteLine ("Error: Could not get FASTQ files");
				Console.WriteLine ("Exception is: " + thrown.Message);
				System.Environment.Exit (-1);
			}
		}
            

		static void ParseCommandLine (string[] args)
		{
			OptionSet OS = new OptionSet () {
				{ "h|help","Show Help",v => ShowHelp () },
				{ "d=","Search Directory For FASTQ Files",v => LoadFastQForDirectory (v) },
                { "v","Show verbose output",v => Program.SetVerboseAfterLoad = true },
				{ "xml=","Set XML File",v => SetXMLFile (v) },
                { "o=", "Set Output File Prefix", v => OutputNameAfterLoad = v }, 
				{ "<>","Fastq file to analyze",v => AddFileToList (v) }
			};
			OS.Parse (args);
			OS.WriteOptionDescriptions (new StreamWriter (Console.OpenStandardOutput ()));
		}

		static void ShowHelp ()
		{
			List<string> Help = new List<string> () {"", "freqout - Freq-Seq Console Application",
				"Program must specify an XML file and at least one FASTQ file (or directory)",
				"-xml\tThe XML file with the analysis settings",
				"",
				"Additional Options:",
				"-d\tDirectory to find FASTQ files in (files must have .fastq extension)",
				"-v\tVerbose (overrides XML)",
                "-o\tSet Output File Name Prefix (overrides XML)",
				"-help\tShow Help",
				"\n",
				"Example: PC",
				"freqout.exe -xml=Simple.xml C:\\SeqData\\MyFile.fastq",
				"\n",
				"Example: Apple/Linux",
				"mono freqout.exe -xml=Simple.xml C:\\SeqData\\MyFile.fastq",
				"",
				"Note that Apple/Linux use requires installation of Mono: http://www.mono-project.com/Main_Page",
				"",
                "More info: http://www.evolvedmicrobe.com/FreqSeq/index.html",
				""
			};
			foreach (string str in Help) {
				Console.WriteLine (str);
			}
			System.Environment.Exit (0);
		}

		static void RunAnalysis ()
		{
			try {
				DateTime start = DateTime.Now;
				Console.WriteLine ("FreqOut Analysis of " + FileNames.Count.ToString () + " files started.\n");
				AlleleFinder AF = XML_Parser.CreateAlleleFinderFromXML (XMLFilename);
				if (SetVerboseAfterLoad) {
					if (AF.Verbose == false)
						Console.WriteLine ("Overriding XML and setting verbose option to true,");
					AF.Verbose = true;
				}
				if (AF.Verbose) {
					AF.LoggerEvent += new LogEventHandler (AF_LoggerEvent);
				}
                if (OutputNameAfterLoad != String.Empty) {
                    AF.OutputFileNamePrefix = OutputNameAfterLoad;
                    
                }
				AF.SetFileNames (FileNames);
				AF.ParseFiles ();
				AF.MakeReport ();
				double totMinutes = DateTime.Now.Subtract (start).TotalMinutes;
				Console.WriteLine ("Finished successfully");
				Console.WriteLine ("Analysis took: " + totMinutes.ToString ("F") + " minutes");
			} catch (Exception thrown) {
				
                Console.WriteLine ("Error: Could not run analysis");
				Console.WriteLine ("Exception is: " + thrown.Message);
                Console.WriteLine ("Stack Trace is: " + thrown.StackTrace);
                if (thrown is AggregateException)
                {
                    var age=thrown as AggregateException;
                    foreach (var ex in age.InnerExceptions)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
				System.Environment.Exit (-1);
			}
		}

		static void AF_LoggerEvent (object sender, string Message)
		{
			Console.WriteLine (Message);
		}
	}
}
