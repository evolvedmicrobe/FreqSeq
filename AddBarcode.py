import sys
inName = sys.argv[1]
outName = sys.argv[2]

# Create data to add in front of each read
barcode = "CGTGAT"
m13 = "GTAAAACGACGGCCAGT"
seq_to_add = barcode + m13
qv_to_add = "F" * len(seq_to_add)

# In and out files
d = open(inName)
o = open(outName, 'w')

# Now add them in.
while True:
	name = d.readline()
	if len(name) ==0:
		break
	seq = seq_to_add + d.readline()
	o.write(name)
	o.write(seq)
	o.write(d.readline()) # '+'
	qvs = d.readline()
	quals = qvs[:len(seq_to_add)] + qvs
	print quals
	o.write(quals)

d.close()
o.close()