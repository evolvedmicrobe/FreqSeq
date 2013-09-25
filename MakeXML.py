direc=r'C:\Users\Clarity\Documents\My Dropbox\AFSeq\\'
fname=direc+"AF_BarCodes.txt"
data=open(fname).readlines()
for d in data:
    print "<Barcode>"+d.strip()+"</Barcode>"