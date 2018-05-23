using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Data;

namespace CustomRegionPOC.Common.Helper
{
    public class csvHelper
    {
        public static DataTable parsePropertyCsv(string propertiesPath, string propertyAddressPath)
        {
            DataTable dtProperties = new DataTable();
            DataTable dtPropertyAddresses = new DataTable();

            //Read the contents of CSV file.  
            string propertiesData = File.ReadAllText(propertiesPath);
            string propertyAddressesData = File.ReadAllText(propertyAddressPath);

            dtProperties = parseCSV(propertiesData);

            return dtProperties;
        }

        public static DataTable parseAreaCsv(string csvPath)
        {
            string csvData = File.ReadAllText(csvPath);
            return parseCSV(csvData);
        }

        public static DataTable parseCSV(string csvData)
        {
            DataTable dt = new DataTable();
            var isFirstRow = true;
            foreach (string row in csvData.Split('\n'))
            {
                if (isFirstRow)
                {
                    System.Console.WriteLine("Inside first row");
                    foreach (string cell in row.Split(','))
                    {

                        System.Console.WriteLine(cell);
                        dt.Columns.Add(cell);
                    }
                    isFirstRow = false;
                }
                else if (!string.IsNullOrEmpty(row))
                {
                    dt.Rows.Add();
                    int i = 0;

                    //Execute a loop over the columns. 
                    string polygonValue = string.Empty;
                    if (row.Contains("\"MULTIPOLYGON"))
                    {
                        polygonValue = row.Substring(row.IndexOf("\"MULTIPOLYGON"), row.IndexOf(")))\"") - row.IndexOf("\"MULTIPOLYGON") + 4);
                    }
                    else if (row.Contains("\"POLYGON"))
                    {
                        polygonValue = row.Substring(row.IndexOf("\"POLYGON"), row.IndexOf("))\"") - row.IndexOf("\"POLYGON") + 4);
                    }

                    string rowData = row;
                    if (!string.IsNullOrEmpty(polygonValue))
                    {
                        rowData = row.Replace(polygonValue, string.Empty);
                    }

                    foreach (string cell in rowData.Split(','))
                    {
                        dt.Rows[dt.Rows.Count - 1][i] = cell.Replace("'", "");
                        i++;
                    }

                    if (!string.IsNullOrEmpty(polygonValue))
                    {
                        dt.Rows[dt.Rows.Count - 1]["OriginalPolygon"] = polygonValue.Replace("\"", string.Empty);
                    }
                }
            }
            return dt;
        }
    }
}