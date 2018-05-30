using System;
using CustomRegionPOC.Common.Model;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing;
using System.IO;
using CustomRegionPOC.Common.Helper;
using CustomRegionPOC.Service;
using System.Data;
using System.Runtime.Serialization.Formatters.Binary;
using Amazon.DynamoDBv2.Model;
using CustomRegionPOC.Common.Extension;
using Amazon.DynamoDBv2;

namespace CustomRegionPOC.Console
{
    public static class Program
    {
        public static IConfiguration Configuration { get; set; }

        public static RegionService regionServiceInstance;

        public static void Main(string[] args)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true);

            Configuration = builder.Build();

            regionServiceInstance = new RegionService(Configuration);

            System.Console.WriteLine("Initiating Data Migration");

            string csvPath = string.Empty;

            migrate(Directory.GetCurrentDirectory() + "/AreasFinal.csv", Directory.GetCurrentDirectory() + "/Properties.csv", Directory.GetCurrentDirectory() + "/PropertyAddresses.csv");

        }

        public static void migrate(string areasPath, string propertiesPath, string propertyAddressPath)
        {
            try
            {
                DataRow[] dtAreas = csvHelper.parseAreaCsv(areasPath).Rows.Cast<DataRow>().ToArray();
                DataRow[] dtProperties = csvHelper.parseAreaCsv(propertiesPath).Rows.Cast<DataRow>().ToArray();
                DataRow[] dtPropertyAddresses = csvHelper.parseAreaCsv(propertyAddressPath).Rows.Cast<DataRow>().ToArray();

                //migrateAreas(dtAreas);

                migrateProperty(dtProperties, dtPropertyAddresses);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public static void migrateAreas(DataRow[] dtAreas)
        {
            List<Area> areaMigration = new List<Area>();
            foreach (DataRow area in dtAreas)
            {
                Area obj = new Area();

                obj.AreaID = area[0].ToString();
                obj.AreaName = area[1].ToString();
                obj.URLName = area[2].ToString();
                obj.URLPath = area[3].ToString();
                obj.StateFIPS = area[4].ToString();
                obj.FIPS = area[5].ToString();
                obj.State = area[6].ToString();
                obj.USPSCity = area[7].ToString();
                obj.AreaTypeID = area[8].ToString();
                obj.SubTypeID = area[9].ToString();
                obj.OriginalPolygon = area[10].ToString();
                obj.Latitude = Convert.ToDecimal(area[11].ToString());
                obj.Longitude = Convert.ToDecimal(area[12].ToString());
                obj.North = area[13].ToString();
                obj.South = area[14].ToString();
                obj.East = area[15].ToString();
                obj.West = area[16].ToString();
                obj.TopLeveeaID = area[17].ToString();
                obj.SourceID = area[18].ToString();
                obj.SourceKey = area[19].ToString();
                obj.AreaStatus = !area.Table.Columns.Contains("Status") ? string.Empty : area[21].ToString();

                areaMigration.Add(obj);
            }

            var areaListingMigration = dtAreas.Select(area => new AreaMaster()
            {
                GUID = Guid.NewGuid().ToString(),
                AreaID = area[0].ToString(),
                AreaName = area[1].ToString(),
                IsPredefine = true,
                OriginalPolygon = area[10].ToString()
            });

            List<Area> areas = new List<Area>();
            foreach (Area obj in areaMigration)
            {
                obj.Points = obj.OriginalPolygon.Replace("MULTIPOLYGON", "").Replace("POLYGON", "").Replace("(", "").Replace(")", "").Split(",").Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => new LocationPoint() { Lng = Convert.ToDecimal(x.Substring(0, x.IndexOf(" ")).Trim()), Lat = Convert.ToDecimal(x.Substring(x.IndexOf(" "), x.Length - x.IndexOf(" ")).Trim()) }).ToList();

                List<Tile> rasterizePoints = regionServiceInstance.GetCoordinateTile(obj.Points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList(), true);

                foreach (var point in rasterizePoints)
                {
                    Area tempObj = (Area)obj.Clone();
                    tempObj.Tile = regionServiceInstance.GetTileStr((int)point.Row, (int)point.Column);
                    tempObj.Type = RecordType.Area;
                    tempObj.OriginalPolygon = "";
                    tempObj.Points = null;

                    areas.Add(tempObj);
                }
            }

            List<AreaMaster> areaListings = new List<AreaMaster>();
            foreach (AreaMaster obj in areaListingMigration)
            {
                obj.Points = obj.OriginalPolygon.Replace("MULTIPOLYGON", "").Replace("POLYGON", "").Replace("(", "").Replace(")", "").Split(",").Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => new LocationPoint() { Lng = Convert.ToDecimal(x.Substring(0, x.IndexOf(" ")).Trim()), Lat = Convert.ToDecimal(x.Substring(x.IndexOf(" "), x.Length - x.IndexOf(" ")).Trim()) }).ToList();
                obj.OriginalPolygon = "";
                areaListings.Add(obj);
            }


            List<AttributeDefinition> areaAttributeDefinition = new List<AttributeDefinition>()
            {
                new AttributeDefinition { AttributeName = "Tile", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S }
            };

            Projection projection = new Projection() { ProjectionType = "INCLUDE" };

            var nonKeyAttributes = new List<string>()
            {
                "AreaName"
            };
            projection.NonKeyAttributes = nonKeyAttributes;

            List<LocalSecondaryIndex> localSecondaryIndexes = new List<LocalSecondaryIndex>();

            List<KeySchemaElement> areaIDKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "Tile", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.RANGE }
            };
            localSecondaryIndexes.Add(new LocalSecondaryIndex()
            {
                IndexName = "AreaIDIndex",
                Projection = projection,
                KeySchema = areaIDKeySchema
            });

            regionServiceInstance.CreateTempTable("tile_area_v2", areaAttributeDefinition, null, localSecondaryIndexes, "Tile", "AreaID").Wait();





            Projection areaListingProjection = new Projection() { ProjectionType = "INCLUDE", NonKeyAttributes = new List<string> { "IsPredefine" } };

            List<LocalSecondaryIndex> areaListingLocalSecondaryIndexes = new List<LocalSecondaryIndex>();

            List<KeySchemaElement> areaListingKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "AreaName", KeyType = KeyType.RANGE }
            };
            areaListingLocalSecondaryIndexes.Add(new LocalSecondaryIndex()
            {
                IndexName = "AreaIDIndex",
                Projection = areaListingProjection,
                KeySchema = areaListingKeySchema
            });

            List<AttributeDefinition> areaListingAttributeDefinition = new List<AttributeDefinition>()
            {
                new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "AreaName", AttributeType = ScalarAttributeType.S }
            };

            regionServiceInstance.CreateTempTable("tile_area_master_v2", areaListingAttributeDefinition, null, areaListingLocalSecondaryIndexes, "AreaID", "AreaName").Wait();

            foreach (var obj in areaListings.ToList().ChunkBy(100))
            {
                try
                {

                    System.Console.WriteLine("adding Area Master chunk");
                    var batch = regionServiceInstance.context.CreateBatchWrite<AreaMaster>();
                    batch.AddPutItems(obj);
                    batch.ExecuteAsync().Wait();

                    System.Console.WriteLine("Area Master Chunk added");
                    //regionServiceInstance.context.SaveAsync(obj).Wait();
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(obj);
                    System.Console.WriteLine(e);
                }
            }

            int count = 1;
            foreach (var obj in areas.ToList().ChunkBy(100))
            {
                try
                {
                    System.Console.WriteLine("adding Area chunk. index: " + count);

                    //Parallel.ForEach(obj, obj2 =>
                    //{
                    //    regionServiceInstance.context.SaveAsync<Area>(obj2).Wait();
                    //});


                    var batch = regionServiceInstance.context.CreateBatchWrite<Area>();
                    batch.AddPutItems(obj);
                    batch.ExecuteAsync().Wait();

                    System.Console.WriteLine("Area Chunk added. index: " + count);

                    count += 1;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(obj);
                    System.Console.WriteLine(e);
                }
            }

        }

        public static void migrateProperty(DataRow[] dtProperties, DataRow[] dtPropertyAddresses)
        {
            var propertyMigration = from propertyAddress in dtPropertyAddresses.AsEnumerable()
                                    join property in dtProperties.AsEnumerable() on propertyAddress["PropertyAddressID"] equals property["PropertyAddressID"]
                                    select new Property
                                    {
                                        PropertyID = !property.Table.Columns.Contains("PropertyID") ? string.Empty : property["PropertyID"].ToString(),
                                        PropertyAddressID = !property.Table.Columns.Contains("PropertyAddressID") ? string.Empty : property["PropertyAddressID"].ToString(),
                                        APN = !property.Table.Columns.Contains("APN") ? string.Empty : property["APN"].ToString(),
                                        UnitNumber = !property.Table.Columns.Contains("UnitNumber") ? string.Empty : property["UnitNumber"].ToString(),
                                        UnitNumberLegal = !property.Table.Columns.Contains("UnitNumberLegal") ? string.Empty : property["UnitNumberLegal"].ToString(),
                                        UnitType = !property.Table.Columns.Contains("UnitType") ? string.Empty : property["UnitType"].ToString(),
                                        BathsFull = !property.Table.Columns.Contains("BathsFull") ? string.Empty : property["BathsFull"].ToString(),
                                        BathsHalf = !property.Table.Columns.Contains("BathsHalf") ? string.Empty : property["BathsHalf"].ToString(),
                                        Beds = !property.Table.Columns.Contains("Beds") ? string.Empty : property["Beds"].ToString(),
                                        SqFt = !property.Table.Columns.Contains("SqFt") ? string.Empty : property["SqFt"].ToString(),
                                        YearBuilt = !property.Table.Columns.Contains("YearBuilt") ? string.Empty : property["YearBuilt"].ToString(),
                                        Rooms = !property.Table.Columns.Contains("Rooms") ? string.Empty : property["Rooms"].ToString(),
                                        Units = !property.Table.Columns.Contains("Units") ? string.Empty : property["Units"].ToString(),
                                        Buildings = !property.Table.Columns.Contains("Buildings") ? string.Empty : property["Buildings"].ToString(),
                                        PropertyStories = !property.Table.Columns.Contains("Stories") ? string.Empty : property["Stories"].ToString(),
                                        ParkingSpaces = !property.Table.Columns.Contains("ParkingSpaces") ? string.Empty : property["ParkingSpaces"].ToString(),
                                        TaxRate = !property.Table.Columns.Contains("TaxRate") ? string.Empty : property["TaxRate"].ToString(),
                                        LastSaleDate = !property.Table.Columns.Contains("LastSaleDate") ? string.Empty : property["LastSaleDate"].ToString(),
                                        LastSalePrice = !property.Table.Columns.Contains("LastSalePrice") ? string.Empty : property["LastSalePrice"].ToString(),
                                        AssessedValueLand = !property.Table.Columns.Contains("AssessedValueLand") ? string.Empty : property["AssessedValueLand"].ToString(),
                                        AssessedValueImprovements = !property.Table.Columns.Contains("AssessedValueImprovements") ? string.Empty : property["AssessedValueImprovements"].ToString(),
                                        StoriesType = !property.Table.Columns.Contains("StoriesType") ? string.Empty : property["StoriesType"].ToString(),
                                        PropertyType = !property.Table.Columns.Contains("PropertyType") ? string.Empty : property["PropertyType"].ToString(),
                                        Construction = !property.Table.Columns.Contains("Construction") ? string.Empty : property["Construction"].ToString(),
                                        ParkingType = !property.Table.Columns.Contains("ParkingType") ? string.Empty : property["ParkingType"].ToString(),
                                        Pool = !property.Table.Columns.Contains("Pool") ? string.Empty : property["Pool"].ToString(),
                                        Style = !property.Table.Columns.Contains("Style") ? string.Empty : property["Style"].ToString(),
                                        Exterior = !property.Table.Columns.Contains("Exterior") ? string.Empty : property["Exterior"].ToString(),
                                        Foundation = !property.Table.Columns.Contains("Foundation") ? string.Empty : property["Foundation"].ToString(),
                                        Roof = !property.Table.Columns.Contains("Roof") ? string.Empty : property["Roof"].ToString(),
                                        Heating = !property.Table.Columns.Contains("Heating") ? string.Empty : property["Heating"].ToString(),
                                        AC = !property.Table.Columns.Contains("AC") ? string.Empty : property["AC"].ToString(),
                                        Elevator = !property.Table.Columns.Contains("Elevator") ? string.Empty : property["Elevator"].ToString(),
                                        Fireplaces = !property.Table.Columns.Contains("Fireplaces") ? string.Empty : property["Fireplaces"].ToString(),
                                        Basement = !property.Table.Columns.Contains("Basement") ? string.Empty : property["Basement"].ToString(),
                                        PrimaryListingInstanceID = !property.Table.Columns.Contains("PrimaryListingInstanceID") ? string.Empty : property["PrimaryListingInstanceID"].ToString(),
                                        PropertyStatus = !property.Table.Columns.Contains("Status") ? string.Empty : property["Status"].ToString(),

                                        StreetNumber = !propertyAddress.Table.Columns.Contains("streetnumber") ? string.Empty : propertyAddress["streetnumber"].ToString(),
                                        StreetDirPrefixID = !propertyAddress.Table.Columns.Contains("streetdirprefixid") ? string.Empty : propertyAddress["streetdirprefixid"].ToString(),
                                        StreetNameID = !propertyAddress.Table.Columns.Contains("streetnameid") ? string.Empty : propertyAddress["streetnameid"].ToString(),
                                        StreetDirSuffixID = !propertyAddress.Table.Columns.Contains("streetdirsuffixid") ? string.Empty : propertyAddress["streetdirsuffixid"].ToString(),
                                        StreetSuffixID = !propertyAddress.Table.Columns.Contains("streetsuffixid") ? string.Empty : propertyAddress["streetsuffixid"].ToString(),
                                        CityID = !propertyAddress.Table.Columns.Contains("cityid") ? string.Empty : propertyAddress["cityid"].ToString(),
                                        Zip = !propertyAddress.Table.Columns.Contains("zip") ? string.Empty : propertyAddress["zip"].ToString(),
                                        CountyID = !propertyAddress.Table.Columns.Contains("countyid") ? string.Empty : propertyAddress["countyid"].ToString(),
                                        Latitude = !propertyAddress.Table.Columns.Contains("latitude") ? 0 : Convert.ToDecimal(propertyAddress["latitude"].ToString()),
                                        Longitude = !propertyAddress.Table.Columns.Contains("longitude") ? 0 : Convert.ToDecimal(propertyAddress["longitude"].ToString()),
                                        PixelX = !propertyAddress.Table.Columns.Contains("pixelx") ? string.Empty : propertyAddress["pixelx"].ToString(),
                                        PixelY = !propertyAddress.Table.Columns.Contains("pixely") ? string.Empty : propertyAddress["pixely"].ToString(),
                                        LotSize = !propertyAddress.Table.Columns.Contains("lotsize") ? string.Empty : propertyAddress["lotsize"].ToString(),
                                        PropertyAddressName = !propertyAddress.Table.Columns.Contains("Status") ? string.Empty : propertyAddress["name"].ToString(),
                                        PropertyAddressStories = !propertyAddress.Table.Columns.Contains("name") ? string.Empty : propertyAddress["stories"].ToString(),
                                        PropertyAddressStatus = !propertyAddress.Table.Columns.Contains("status") ? string.Empty : propertyAddress["status"].ToString(),
                                        propertyAddressCount = !propertyAddress.Table.Columns.Contains("propertyAddresscount") ? string.Empty : propertyAddress["propertyAddresscount"].ToString(),
                                        YearBuiltMin = !propertyAddress.Table.Columns.Contains("yearbuiltmin") ? string.Empty : propertyAddress["yearbuiltmin"].ToString(),
                                        YearBuiltMax = !propertyAddress.Table.Columns.Contains("yearbuiltmax") ? string.Empty : propertyAddress["yearbuiltmax"].ToString(),
                                        AverageValue = !propertyAddress.Table.Columns.Contains("averagevalue") ? string.Empty : propertyAddress["averagevalue"].ToString(),
                                        AverageValueLow = !propertyAddress.Table.Columns.Contains("averagevaluelow") ? string.Empty : propertyAddress["averagevaluelow"].ToString(),
                                        AverageValueHigh = !propertyAddress.Table.Columns.Contains("averagevaluehigh") ? string.Empty : propertyAddress["averagevaluehigh"].ToString(),
                                        AverageRent = !propertyAddress.Table.Columns.Contains("averagerent") ? string.Empty : propertyAddress["averagerent"].ToString(),
                                        AverageSqFt = !propertyAddress.Table.Columns.Contains("averagesqft") ? string.Empty : propertyAddress["averagesqft"].ToString(),
                                        AverageValuePerSqFt = !propertyAddress.Table.Columns.Contains("averagevaluepersqft") ? string.Empty : propertyAddress["averagevaluepersqft"].ToString(),
                                        AreaID = !propertyAddress.Table.Columns.Contains("defaultparentareaid") ? string.Empty : propertyAddress["defaultparentareaid"].ToString(),
                                        Url = !propertyAddress.Table.Columns.Contains("url") ? string.Empty : propertyAddress["url"].ToString(),
                                        FullStreetAddress = !propertyAddress.Table.Columns.Contains("fullstreetaddress") ? string.Empty : propertyAddress["fullstreetaddress"].ToString()
                                    };

            List<PointF> points = propertyMigration.Select(x => new PointF((float)Convert.ToDecimal(x.Latitude), (float)Convert.ToDecimal(x.Longitude))).ToList();
            List<Tile> tiles = regionServiceInstance.GetCoordinateTile(points, false);

            Projection projection = new Projection() { ProjectionType = "INCLUDE" };

            List<string> nonKeyAttributes = new List<string>();

            // nonKeyAttributes.Add("AreaID");
            nonKeyAttributes.Add("IsPredefine");
            nonKeyAttributes.Add("PropertyID");
            nonKeyAttributes.Add("PropertyAddressID");
            nonKeyAttributes.Add("PropertyAddressName");
            nonKeyAttributes.Add("BathsFull");
            nonKeyAttributes.Add("BathsHalf");
            nonKeyAttributes.Add("Beds");
            nonKeyAttributes.Add("AverageValue");
            nonKeyAttributes.Add("AverageRent");
            nonKeyAttributes.Add("Latitude");
            nonKeyAttributes.Add("Longitude");
            // nonKeyAttributes.Add("GeoHash");

            projection.NonKeyAttributes = nonKeyAttributes;

            List<LocalSecondaryIndex> localSecondaryIndexes = new List<LocalSecondaryIndex>();
            List<GlobalSecondaryIndex> globalSecondaryIndexes = new List<GlobalSecondaryIndex>();

            List<KeySchemaElement> propertyAddressIDKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "GeoHash", KeyType = KeyType.RANGE }
            };
            globalSecondaryIndexes.Add(new GlobalSecondaryIndex()
            {
                IndexName = "AreaIDIndex",
                Projection = projection,
                KeySchema = propertyAddressIDKeySchema,
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = (long)100,
                    WriteCapacityUnits = (long)100
                },
            });

            List<AttributeDefinition> attributeDefinition = new List<AttributeDefinition>()
                {
                    new AttributeDefinition { AttributeName = "Tile", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "PropertyID", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "GeoHash", AttributeType = ScalarAttributeType.S }
                };

            regionServiceInstance.CreateTempTable("tile_property_v2_geohash", attributeDefinition, globalSecondaryIndexes, localSecondaryIndexes, "Tile", "PropertyID").Wait();

            List<Property> properties = new List<Property>();
            Random rnd = new Random();
            foreach (var obj in propertyMigration)
            {
                Tile tempTile = tiles.FirstOrDefault(x => x.Lat == (float)obj.Latitude && x.Lng == (float)obj.Longitude);

                Property tempObj = (Property)obj.Clone();

                //tempObj.AreaID += "-" + rnd.Next(1, 10);
                tempObj.Tile = regionServiceInstance.GetTileStr((int)tempTile.Row, (int)tempTile.Column);
                tempObj.Type = RecordType.Listing;
                tempObj.Guid = rnd.Next(1, 16);
                tempObj.IsPredefine = true;
                tempObj.Name = obj.PropertyAddressName;
                tempObj.GeoHash = geoHashHelper.Encode((double)obj.Latitude, (double)obj.Longitude, 10);
                properties.Add(tempObj);
            };


            int count = 1;
            foreach (var obj in properties.ToList().ChunkBy(100))
            {
                try
                {
                    System.Console.WriteLine("Initiating a new chunk. Index: " + count);
                    var batch = regionServiceInstance.context.CreateBatchWrite<Property>();
                    batch.AddPutItems(obj);
                    batch.ExecuteAsync().Wait();
                    System.Console.WriteLine("Chunk inserted successfully. Index: " + count);

                    count += 1;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(obj);
                    System.Console.WriteLine(e);
                }
            }
        }
    }
}
