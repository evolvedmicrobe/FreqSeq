using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace FREQSeq
{
    /// <summary>
    /// Creates an Allele Finder class and parses the XML file for the 
    /// </summary>
    public class XML_Parser
    {
        //Names of the various nodes
        const string BASE_NODE_NAME = "AFSeq";
        const string OPTIONS_NODE_NAME = "Options";
        const string BARCODE_NODE_NAME = "Barcode";
        const string BARCODES_NODE_NAME = "Barcodes";
        const string VARIANTS_NODE_NAME = "Variants";
        const string VARIANT_NODE_NAME = "Variant";
        const string TYPE_NODE_NAME = "Type";
        /// <summary>
        /// Parses an XML file and returns an Allele Finder class 
        /// with the settings made appropriately
        /// </summary>
        /// <returns>An allele finder class which can be used to parse a series of FASTQ files </returns>
        public static AlleleFinder CreateAlleleFinderFromXML(string Filename)
        {
            if (!File.Exists(Filename))
                throw new IOException("File: " + Filename + " does not appear to exist and cannot be found.");
            XmlDocument XmlDoc = new XmlDocument();
            XmlTextReader XReader = new XmlTextReader(Filename);
            XmlDoc.Load(XReader);
            //first node is xml, second is the protocol, this is assumed and should be the case
            XmlNode baseNode = XmlDoc;
            ValidateXMLHasEssentialElements(baseNode);
            //Get the barcode groups
            XmlNode barcodeXML = baseNode.SelectSingleNode("//" + BARCODES_NODE_NAME);
            BarCodeCollection BCC=CreateBarCodeCollectionFromBarcodeXMLNode(barcodeXML);
            XmlNode variantXML=baseNode.SelectSingleNode("//"+ VARIANTS_NODE_NAME);
            AlleleCollection AC=CreateAlleleCollectionFromVariantsXMLNode(variantXML);
            AlleleFinder AF = new AlleleFinder(BCC,AC);
            AF.SetDefaultOptions();
            XmlNode options = baseNode.SelectSingleNode("//" + OPTIONS_NODE_NAME);
            if (options != null)
            {
                SetAlleleFinderValuesFromXML(options, AF);
            }
            return AF;
        }
        private static void ValidateXMLHasEssentialElements(XmlNode xmlNode)
        {
            XmlNode baseNode = xmlNode.SelectSingleNode("//" + BASE_NODE_NAME);
            if (baseNode.Name != BASE_NODE_NAME)
            {
                throw new IOException("Base node in the XML file is not named "+BASE_NODE_NAME+".  Remember it is case-sensitive.");
            }
            XmlNodeList variants=baseNode.SelectNodes("//"+VARIANTS_NODE_NAME);
            XmlNodeList barcodes=baseNode.SelectNodes("//"+BARCODES_NODE_NAME);
            if(variants.Count!=1 || barcodes.Count!=1)
            {
                throw new IOException("Not enough or two many "+BARCODES_NODE_NAME+" or "+VARIANTS_NODE_NAME+" nodes.  Should be only one of each. Remember their names are case-sensitive.");
            }
        }
        private static BarCodeCollection CreateBarCodeCollectionFromBarcodeXMLNode(XmlNode barCodeNode)
        {
            BarCodeCollection BCC = new BarCodeCollection();
            foreach (XmlNode childNode in barCodeNode.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element && childNode.Name == BARCODE_NODE_NAME)
                {
                    string bcStr=childNode.InnerText.Trim();
                    
                    BarCodeGroup bcg = new BarCodeGroup(bcStr);
                    BCC.AddBarCodeGroup(bcg);
                }
            }
            if (BCC.AllBarCodes.Count == 0)
                throw new IOException("No Barcodes were created from this XML file.");
            return BCC;
        }
        private static AlleleCollection CreateAlleleCollectionFromVariantsXMLNode(XmlNode variantsXML)
        {
            AlleleCollection AC = new AlleleCollection();
            foreach (XmlNode childNode in variantsXML.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element && childNode.Name == VARIANT_NODE_NAME)
                {
                    XmlNodeList types = childNode.SelectNodes(TYPE_NODE_NAME);
                    if (types.Count < 2)
                    {
                        throw new IOException("Need more than two types defined in XML for all variants");
                    }
                    AlleleCollection.Allele curAllele = new AlleleCollection.Allele();
                    foreach (XmlNode node in types)
                    {
                        string curType = node.InnerText.Trim();
                        curAllele.AddType(curType);
                    }
                    AC.AddAllele(curAllele);                   
                }
            }
            if (AC.AllSequences.Count == 0)
                throw new IOException("No Alleles were created from this XML file.  Check the formatting.");
            return AC;

        }
        private static void SetAlleleFinderValuesFromXML(XmlNode optionsXML,AlleleFinder AF)
        {
            try
            {
                Type thisType = AF.GetType();
                foreach (XmlNode childNode in optionsXML.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        string propertyName = childNode.Name;
                        //get the variable type info
                        XmlNode typeNode = childNode.Attributes.RemoveNamedItem("Type");
                        if (typeNode == null)
                        {
                            throw new Exception("Option Type not set in xml, please declare the variable type for all "
                                + " options including  " + propertyName.ToString());
                        }
                        Type VariableType = System.Type.GetType(typeNode.Value);
                        var Value = Convert.ChangeType(childNode.InnerText, VariableType);
                        //now get the property and change it
                        var prop = thisType.GetProperty(propertyName);
                        if (prop == null)
                        {
                            throw new Exception("No option called " + propertyName
                                + "\n so the xml file needs to be fixed");
                        }
                        prop.SetValue(AF, Value, null);
                    }
                }
            }
            catch (Exception thrown)
            {
                IOException newExcept = new IOException("Could not parse the options XML node" + thrown.Message, thrown);
                throw newExcept;
            }


        }
    }
}
