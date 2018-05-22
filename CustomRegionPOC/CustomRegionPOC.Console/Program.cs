using System;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using CustomRegionPOC.Common.Model;
using CustomRegionPOC.Common.Service;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Amazon.DynamoDBv2.DocumentModel;
using System.Drawing;
using System.Net;
using System.IO;
using CustomRegionPOC.Common.Helper;
using CustomRegionPOC.Service;
using System.Data;

namespace CustomRegionPOC.Console
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
            System.Console.WriteLine("Initiating Data Migration");
            if (args[0] == "listingsPath") {
                migrateListings(args[1]);
            }

        }

        static void migrateListings(string url) {

            System.Console.WriteLine("Migrating Listings");

            var dt = new DataTable();

            try {

            dt = csvHelper.parseCsv(url);
            }
            catch (Exception ex) {
                System.Console.WriteLine("Unable to parse csv. invalid path?");
                throw ex;
            }
                    
            // Set up configuration sources.
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true);

            Configuration = builder.Build();

            var regionServiceInstance = new RegionService(Configuration);
             System.Console.WriteLine(Configuration["AWS:region"]);

        var pointsList = new List<PointF>();
        var listTile = new List<Tile>();
        var regions = new List<Region>();

        Parallel.ForEach (dt.Rows.Cast<DataRow>(), row => {

            System.Console.WriteLine(row.ToString());

            // var pointFInstance = new PointF
            // {
            //     X = (float)(Convert.ToDecimal(row[0].ToString())),
            //     Y = (float)(Convert.ToDecimal(row[1].ToString()))
            // };

            // pointsList.Add(pointFInstance);
            // listTile = regionServiceInstance.getCoordinateTile(pointsList);

            // regions.Add(new Region {
            //     Name = row[2].ToString(),
            //     Tile = regionServiceInstance.getTileStr((int)listTile[0].Row, (int)listTile[0].Column),
            //     Latitude = Convert.ToDecimal(listTile[0].Lat),
            //     Longitude = Convert.ToDecimal(listTile[0].Lng),
            //     Type = RecordType.Listing,
            //     Guid = Guid.NewGuid().ToString()
            // });
        });

        System.Console.WriteLine("added all regions");
        //regionServiceInstance.saveRegions(regions);   

        }
    }
}
