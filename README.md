FreqSeq
=======

Programs used to analyze data from Freq Seq Data.  See the main documentation pages at:

http://www.evolvedmicrobe.com/FreqSeq/index.html

The source code is divided in to several folders.

* FreqSeqWPF - the WPF client on windows
* FreqSeq- The library file containing the methods used by the GUI and command line programs.
* freqout - The command line program.

<h2>New Version on 9/27/2015</h2>

Bugs Fixed:

* Warning message if unequal length alleles are input into the program.

Improvements:

* Compiled binary version released for Ubuntu 14.04

<h2>New Version on 12/4/2013</h2>

Bugs Fixed:

* Bug in Smith-waterman-Gotoh alignment (The +/- sign on the penalty for the gap creation and extension was switched, leading to incorrect alignments)
* Bug in barcode assigner – barcodes within hamming distance of 1 were not being assigned.

Improvements:

* Reads gzipped fastq files (ending with .gz)
* Changed command line executable name to freqout
* Trimmed long sequences to within only 10 bp longer than the expected read to improve alignment speed and accuracy
* Increased hashing k-mer length to 9 to reduce the number of alignment candidates identified
* Automatically skipping reads less than 75 bp (minimum length can be set in XML).
* Output column names changed
