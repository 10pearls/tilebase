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
using System.Diagnostics;
using System.Text;

namespace CustomRegionPOC.Console
{
    public static class Program
    {
        public static IConfiguration Configuration { get; set; }

        public static RegionService regionServiceInstance;

        public static void Main(string[] args)
        {
            var tempStr = "YXtfbUZmfHx1TXRDWGhDSGpLU0pBaFNpQmZAQkhBfkBLVkNWQ3ZEZ0FgRGFAQkF8RFBqQ1ZqQ2JAfEFoQHZBfEB6RHpDdEBHfkBTXEV6QF1GQ3JAcUB8QnVBaEFhQX5AeUBSUWRBfUF0Q3VEdEljTWpJa0xwQ2dEYkNvQWRBcUBiRH1BekFXbEJVYkNPeEBPWkVuQElBYUBBVU1hSUZ3QlRhQmBAb0JUdUFSfUBMZ0BaYUFeeUB+QF9CaEFlQWpEbUN+QW9BZkBtQFZhQHRAaUJuQHdCSG9AfkB5RGBBbUFfQFljQFNTTWNAaUBzQXlBVURda0BTY0BTdUBJcUBDX0BAX0FCYUBSX0FQZ0B2QGVCVl9BSG9ARG9AQXVAQW1AXWlDR2NCQH1CRnFCdkBvRmJBX0NyQmVGckRrSnhAX0JgQV9CYER7RXBEbUZiRWVHakJxQ2JEWmhOdEFiTGhBdEBIekBCXj9wQEduQ2NAdlBvQ2ZIa0FwQ2VAYEVjQG5KY0FlQHNHP1dEaUBSd0BYZUBuQntBWk1qQEtkQEBsQExuQFp4QGJAWkpiQEpmQEBiQENgQEt+QXNAckFLZkA/YkJ5Q0pTaEJ9Q35Ae0FoRnFJP3NFYkVPbEBlQERlQHJAT1ljQENDbU1jUmNMfU9rRWVHd0FrQmFHX0p3Q2dGeUBxQWlAX0F5QGdBcUBnQWFAaUBlQ2tEZ0JlQ3dBb0J9QmFEX0FvQW1Dd0RzRGVGbUFlQndAYUFHR2FAaUBTYUBnQXlBaUNvRFdlQHNAfUB7QGtBY0BtQFFVYUdpSVtfQF1lQEtNZUBvQG9BZUJNVXNCdUNrRH1FUVtTVXdAY0FHRWVAbUBfQG9AaUNvRHVDZERIXktwQGdBakNhRGBCckFwREhWdkpfRVJwQUZsQEJmQVV4Q1toQ0N0QEdeUWhDcUBqR1l6QmFAbENJYkFFfkFYYklOfkBMWF9BbEBDdkBFWENOT3pAWnBEeER6QGFAfkF8QGhCY0FiQHJAeEFfQG5AbUF2QmNAeEBtQXRCX0dgQHdCY0B1QmNAd0NmRGNGXk9Cb0I/e0M/a0poQH5Ad0dWcUB2QmVrQHJBeVFUZ0ZAVXFCUHVEWmVEWHNEWn1CUHtDWGNFYkBvRF5rQlppQVZbSF9DfkBBY0Z3QlRPP2NEXGFEWkx+QmNEXnFHakBKckJjRGpCSU5iQHBFaUBScUBrRkNNdUFjSEFBZ0hnV2lKeUhiQmRJekBkRW1EWnFDVG1EWllGfkBqVGRAbEpjQ2BAY0NYe0JYe0RMZ0NEaUNIeUNESGBHR2xDc0BCeUFSXUR5Q1pfQ1J9QFZfREV7Q1BFeUBBaUFEY0JBd0NDZ0U/cUFDZ0RNaUNPZ0FRaUF5QHVDZUB1QWtAbUFlQG9Ab0F3QVlfQGFAeUBPa0BLXUVZRXdBZ0J1S31Bc0hfQGNCU2FBaURGcURAcVNhSEx5QUB5QmlAeUpPc0JvQXpAeUBqQHdBdkFdVFtaXFBGQkpSfEB8QkR+QEVsQFtgQGNAXlleW2xAU2pAQ2xAR1JnQWRBaUByQUdGU0RJRV9AQlNBZ0FqQEtQRVhSfkBMWmpAZEBgQFRqQnBARkZKZEA/TkFOS2hAZ0ByQFlSUUJTP2tAT1FDSz9VRD9GSmJAQE5DVFFeVUpFRlc/XUhnQGBAQ0hZZEBLVkV4QFN0QFVaR0JFSkdEbUBMeUBca0BkQENGQ3JAUWhBWW5AZ0B4Qm1BfkNlQW5BUXBBQ0JDS0VAQUJDZkBGXkBIQXZAP0pESkFMRURFR3FAakNTYEBrQGRBZ0ByQFFSYUBcV0xVQE1FYUJ7QXdAY0BxQFdvQmVAU0JZUElKRUhVdkE/ckNBSEtYT1JZUGtDYkFPVkFOTGJAVHhBT25AR0BLUG1AdkJzQHZBQUZETj9GRz9HS09kQF9AeEBnQHhATUxTREU/fUBPTUJVUEtMUWBAQFZLWmFAXF9BVkc/Z0BRWT9LTkVURnRBQ1RNSF1Ea0BBY0BJT0JHQk1ST25AY0BsQUdIfUBQc0BQVUhNSkVAa0BwQElOR2BAQnRAQVJNYkBZZkBbTmVBSmlARWdATU1YU2hAQ0ZCYEVsTGtDaEFfQHhFbUJ2Q2FBcEFpQGJFfUFhQHJCb0B4Q2lBdkNzRHhIX0B+QVV2QVNkQ010QVBmRE5qQVJsQVZ8QFp6QGhAakFsQHJBTFZ2QWZCakNuRnZBckdeekVMdENAbkFIZkNiQHRGaEBySV1+Q3RAclR0QGpGYkNmT3xAYEd+QHhGbEB2Rl50R3JAYEpCaGdBRWZFP2pESWBDSHBDYEJ+RGhFdkV2QGRAbkd6RGJEcEJuTXJHcEZsQmRCYEFkQn5AcEVkQnREaEN6Q2JDbkNgQnhDYEJqR2pCdEBKckFW";

            tempStr = ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(tempStr));

            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true);

            Configuration = builder.Build();

            regionServiceInstance = new RegionService(Configuration);

            System.Console.WriteLine("Initiating Data Migration");

            string csvPath = string.Empty;

            //migrate(Directory.GetCurrentDirectory() + "/AreasFinal.csv", Directory.GetCurrentDirectory() + "/Properties.csv", Directory.GetCurrentDirectory() + "/PropertyAddresses.csv");


            //getMetrics("20943", "", "", "", "", 1000);

        }

        public static void getMetrics(string id, string propertyType, string priceRange, string beds, string baths, int rowsLimit)
        {
            Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
            keyConditions.Add("AreaID", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(id) } });

            Dictionary<string, Condition> queryFilter = new Dictionary<string, Condition>();

            if (!string.IsNullOrEmpty(beds))
            {
                queryFilter.Add("Beds", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = beds } } });
            }
            if (!string.IsNullOrEmpty(baths))
            {
                queryFilter.Add("BathsFull", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = baths } } });
            }

            var request = new QueryRequest
            {
                TableName = regionServiceInstance.propertyTableName,
                ReturnConsumedCapacity = "TOTAL",
                Limit = rowsLimit,
                IndexName = "AreaIDIndex",
                KeyConditions = keyConditions,
                QueryFilter = queryFilter,
                AttributesToGet = new List<string> { "PropertyID", "Latitude", "Longitude", "PropertyAddressName" },
                Select = "SPECIFIC_ATTRIBUTES"

            };
            Stopwatch stopwatch = Stopwatch.StartNew();
            QueryResponse response = regionServiceInstance.dynamoDBClient.QueryAsync(request).Result;
            stopwatch.Stop();
            System.Console.WriteLine(stopwatch.ElapsedMilliseconds);
        }

        public static void migrate(string areasPath, string propertiesPath, string propertyAddressPath)
        {
            try
            {
                DataRow[] dtAreas = csvHelper.parseAreaCsv(areasPath).Rows.Cast<DataRow>().ToArray();
                DataRow[] dtProperties = csvHelper.parseAreaCsv(propertiesPath).Rows.Cast<DataRow>().ToArray();
                DataRow[] dtPropertyAddresses = csvHelper.parseAreaCsv(propertyAddressPath).Rows.Cast<DataRow>().ToArray();

                migrateAreas(dtAreas);

                //migrateProperty(dtProperties, dtPropertyAddresses);
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


            List<AreaMaster> areaMaster = new List<AreaMaster>();
            List<Area> areas = new List<Area>();
            foreach (Area obj in areaMigration)
            {
                var tempPoints = obj.OriginalPolygon.Replace("MULTIPOLYGON", "").Replace("POLYGON", "").Replace("(", "").Replace(")", "").Split(",").Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => new LocationPoint() { Lng = Convert.ToDecimal(x.Substring(0, x.IndexOf(" ")).Trim()), Lat = Convert.ToDecimal(x.Substring(x.IndexOf(" "), x.Length - x.IndexOf(" ")).Trim()) }).ToList();

                obj.Points = tempPoints;

                List<Tile> rasterizePoints = regionServiceInstance.GetCoordinateTile(obj.Points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList(), true);

                AreaMaster areaMasterObj = new AreaMaster();
                areaMasterObj.AreaID = obj.AreaID;
                areaMasterObj.AreaName = obj.AreaName;
                areaMasterObj.EncodedPolygon = GooglePoints.EncodeBase64(tempPoints.Select(x => new CoordinateEntity(Convert.ToDouble(x.Lat), Convert.ToDouble(x.Lng))));
                areaMasterObj.EncodedTiles = GooglePoints.EncodeBase64(rasterizePoints.Select(x => new CoordinateEntity(Convert.ToDouble(x.Row), Convert.ToDouble(x.Column))));
                areaMasterObj.IsPredefine = true;
                areaMaster.Add(areaMasterObj);

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


            List<AttributeDefinition> areaAttributeDefinition = new List<AttributeDefinition>()
            {
                new AttributeDefinition { AttributeName = "Tile", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S }
            };

            Projection projection = new Projection() { ProjectionType = "INCLUDE", NonKeyAttributes = new List<string> { "AreaName" } };
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

            regionServiceInstance.CreateTempTable(regionServiceInstance.areaTableName, areaAttributeDefinition, null, localSecondaryIndexes, "Tile", "AreaID").Wait();





            List<LocalSecondaryIndex> areaMasterLocalSecondaryIndexes = new List<LocalSecondaryIndex>();

            List<KeySchemaElement> areaMasterAreaIDKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "AreaName", KeyType = KeyType.RANGE }
            };
            areaMasterLocalSecondaryIndexes.Add(new LocalSecondaryIndex()
            {
                IndexName = "AreaIDIndex",
                Projection = new Projection() { ProjectionType = "INCLUDE", NonKeyAttributes = new List<string> { "IsPredefine" } },
                KeySchema = areaMasterAreaIDKeySchema
            });

            List<KeySchemaElement> areaMasterAreaTileKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "IsPredefine", KeyType = KeyType.RANGE }
            };
            areaMasterLocalSecondaryIndexes.Add(new LocalSecondaryIndex()
            {
                IndexName = "AreaTileIndex",
                Projection = new Projection() { ProjectionType = "INCLUDE", NonKeyAttributes = new List<string> { "EncodedTiles" } },
                KeySchema = areaMasterAreaTileKeySchema
            });

            areaMasterLocalSecondaryIndexes.Add(new LocalSecondaryIndex()
            {
                IndexName = "AreaPolygonIndex",
                Projection = new Projection() { ProjectionType = "INCLUDE", NonKeyAttributes = new List<string> { "EncodedPolygon" } },
                KeySchema = areaMasterAreaTileKeySchema
            });

            List<AttributeDefinition> areaMasterAttributeDefinition = new List<AttributeDefinition>()
            {
                new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "AreaName", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "IsPredefine", AttributeType = ScalarAttributeType.N },
            };

            regionServiceInstance.CreateTempTable(regionServiceInstance.areaMasterTableName, areaMasterAttributeDefinition, null, areaMasterLocalSecondaryIndexes, "AreaID", "AreaName").Wait();

            foreach (var obj in areaMaster.ToList().ChunkBy(100))
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

            nonKeyAttributes.Add("AreaID");
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

            projection.NonKeyAttributes = nonKeyAttributes;

            List<LocalSecondaryIndex> localSecondaryIndexes = new List<LocalSecondaryIndex>();
            List<GlobalSecondaryIndex> globalSecondaryIndexes = new List<GlobalSecondaryIndex>();

            List<KeySchemaElement> propertyAddressIDKeySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = "AreaID", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "IsPredefine", KeyType = KeyType.RANGE }
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
                    new AttributeDefinition { AttributeName = "IsPredefine", AttributeType = ScalarAttributeType.N },
                    new AttributeDefinition { AttributeName = "PropertyID", AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = "AreaID", AttributeType = ScalarAttributeType.S },
                };

            regionServiceInstance.CreateTempTable(regionServiceInstance.propertyTableName, attributeDefinition, globalSecondaryIndexes, localSecondaryIndexes, "Tile", "PropertyID").Wait();

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
