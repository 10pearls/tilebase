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
         public static DataTable parseCsv(string csvPath)
        {
        //Create a DataTable.  
        DataTable dt = new DataTable();  
        // dt.Columns.AddRange(new DataColumn[3] {
        // // new DataColumn("lat", typeof(string)),  
        // // new DataColumn("lng", typeof(string)),  
        // // new DataColumn("address", typeof(string)) });

        // });

        //Read the contents of CSV file.  
        string csvData = File.ReadAllText(csvPath);


        // List<string> splittedCsvData = csvData.Split('\n').ToList();

        // for (var i = 0 ; i < splittedCsvData.Count; i++) {
        //     foreach (string cell in splittedCsvData[i].Split(','))  
        //         {  
        //     if (i == 0) {
        //         dt.Columns.Add(cell);
        //     }
        //     else{
        //         DataRow dr =dt.NewRow();
        //     }
        //         }
        // }


        var isFirstRow = true;
        //Execute a loop over the rows.  
        foreach (string row in csvData.Split('\n'))  
        {
            if (isFirstRow) {
                System.Console.WriteLine("Inside first row");
                foreach (string cell in row.Split(','))  
                { 
                    
                System.Console.WriteLine(cell); 
                dt.Columns.Add(cell);
            }
            isFirstRow = false;
            }


            if (!string.IsNullOrEmpty(row))  
            {  
                dt.Rows.Add();  
                int i = 0;  
  
                //Execute a loop over the columns. 
                System.Console.WriteLine(row.ToString());
                string polygonValue =row.Substring(row.IndexOf("POLYGON"), row.IndexOf("))") - row.IndexOf("POLYGON"));
                System.Console.WriteLine(polygonValue);

                foreach (string cell in row.Split(','))  
                {
                    System.Console.WriteLine(cell.Replace("'", ""));  
                    dt.Rows[dt.Rows.Count - 1][i] = cell.Replace("'", "");
                    i++;  
                }  
            }  
        }
        return dt;  
        }


    }
}

        // //Upload and save the file  
        // string csvPath = Server.MapPath("~/Files/") + Path.GetFileName(FileUpload1.PostedFile.FileName);  
        // FileUpload1.SaveAs(csvPath);  
  
        // //Create a DataTable.  
        // DataTable dt = new DataTable();  
        // dt.Columns.AddRange(new DataColumn[5] { new DataColumn("Id", typeof(int)),  
        // new DataColumn("Name", typeof(string)),  
        // new DataColumn("Technology", typeof(string)),  
        // new DataColumn("Company", typeof(string)),  
        // new DataColumn("Country",typeof(string)) });  
  
        // //Read the contents of CSV file.  
        // string csvData = File.ReadAllText(csvPath);  
  
        // //Execute a loop over the rows.  
        // foreach (string row in csvData.Split('\n'))  
        // {  
        //     if (!string.IsNullOrEmpty(row))  
        //     {  
        //         dt.Rows.Add();  
        //         int i = 0;  
  
        //         //Execute a loop over the columns.  
        //         foreach (string cell in row.Split(','))  
        //         {  
        //             dt.Rows[dt.Rows.Count - 1][i] = cell;  
        //             i++;  
        //         }  
        //     }  
        // }  
  
        // //Bind the DataTable.  
        // GridView1.DataSource = dt;  
        // GridView1.DataBind();  
