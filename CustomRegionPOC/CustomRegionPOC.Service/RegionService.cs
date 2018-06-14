using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using CustomRegionPOC.Common.Model;
using CustomRegionPOC.Common.Service;
using IronPython.Hosting;
using Microsoft.Extensions.Configuration;
using System;
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
using CustomRegionPOC.Common.Extension;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using NetTopologySuite.IO;
using System.Numerics;
using CustomRegionPOC.Common.Enum;
using System.Diagnostics;

namespace CustomRegionPOC.Service
{
    public class RegionService : IRegionService
    {
        public string areaTableName = "tile_area_v2";
        public string areaMasterTableName = "tile_area_master_v2";
        public string propertyTableName = "tile_property_v2";
        public string RasterizeLambdaName = "rasterizer";

        private BasicAWSCredentials credentials;
        public AmazonDynamoDBClient dynamoDBClient;
        public AmazonLambdaClient lambdaClient;
        private string tilebaseURL;
        private string tilebaseURLWithRasterize;
        public DynamoDBContext context;

        public RegionService(IConfiguration configuration)
        {
            RegionEndpoint regionEndpoint = RegionEndpoint.USEast1;

            this.lambdaClient = new AmazonLambdaClient(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"], regionEndpoint);
            this.credentials = new BasicAWSCredentials(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"]);
            this.dynamoDBClient = new AmazonDynamoDBClient(credentials, regionEndpoint);

            CreateTempTable("tile_listing_region_v2", null, null, null).Wait();

            context = new DynamoDBContext(this.dynamoDBClient);

            this.tilebaseURL = configuration["AWS:Tilebase-URL"];
            this.tilebaseURLWithRasterize = configuration["AWS:Tilebase-URL-With-Rasterize"];
        }

        public async Task Create(Area region)
        {
            string areaId = Guid.NewGuid().ToString();
            region.AreaID = areaId;
            region.Points = GooglePoints.Decode(region.EncodedPolygon).Select(x => new LocationPoint(x.Latitude, x.Longitude)).ToList();
            region.EncodedPolygon = null;
            List<Tile> tiles = this.GetCoordinateTile(region.Points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList(), true).tiles;

            AreaMaster areaMaster = new AreaMaster()
            {
                AreaID = areaId,
                AreaName = region.AreaName,
                IsPredefine = false,
                EncodedPolygon = GooglePoints.EncodeBase64(region.Points.Select(x => new CoordinateEntity(Convert.ToDouble(x.Lat), Convert.ToDouble(x.Lng)))),
                EncodedCompletedTiles = GooglePoints.EncodeBase64(tiles.Where(x => !x.IsPartialTile).Select(x => new CoordinateEntity(x.Row, x.Column))),
                EncodedPartialTiles = GooglePoints.EncodeBase64(tiles.Where(x => x.IsPartialTile).Select(x => new CoordinateEntity(x.Row, x.Column)))
            };

            List<Task> tasks = new List<Task>();
            tasks.Add(Task.Factory.StartNew(() =>
            {
                this.context.SaveAsync<AreaMaster>(areaMaster).Wait();
            }));


            List<Area> areas = this.transformRegion(region, tiles);
            SaveAreas(areas);

            Task.WaitAll(tasks.ToArray());
        }

        public async Task<List<AreaMaster>> Get(decimal lat, decimal lng)
        {
            return this.filterRegionList(await this.getRegionByArea(lat, lng), lat, lng);
        }

        public async Task SaveListing(Listing listing)
        {
            List<PointF> points = new List<PointF>();
            points.Add(new PointF
            {
                X = (float)listing.Lat,
                Y = (float)listing.Lng
            });

            List<Tile> coordinates = GetCoordinateTile(points, false).tiles;

            Parallel.ForEach(coordinates, coordinate =>
            {
                context.SaveAsync<Property>(new Property
                {
                    PropertyID = Guid.NewGuid().ToString(),
                    PropertyAddressName = listing.Name,
                    Beds = listing.Beds,
                    BathsFull = listing.BathsFull,
                    BathsHalf = listing.BathsHalf,
                    PropertyAddressID = listing.PropertyAddressId,
                    AverageValue = listing.AverageValue,
                    AverageRent = listing.AverageRent,
                    Latitude = Convert.ToDecimal(coordinate.Lat),
                    Longitude = Convert.ToDecimal(coordinate.Lng),
                    Tile = this.GetTileStr((int)coordinate.Row, (int)coordinate.Column)
                }).Wait();
            });
        }

        public async Task<GetListingWrapper> GetListing(Area area, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null, string encodedCompletedTiles = null, string encodedPartialTiles = null)
        {
            List<Listing> listings = new List<Listing>();

            List<PointF> boundingBox = null;
            if (!string.IsNullOrEmpty(north) && !string.IsNullOrEmpty(east) && !string.IsNullOrEmpty(south) && !string.IsNullOrEmpty(west))
            {
                boundingBox = new List<PointF>();
                boundingBox.Add(new PointF((float)Convert.ToDouble(north), (float)Convert.ToDouble(east)));
                boundingBox.Add(new PointF((float)Convert.ToDouble(north), (float)Convert.ToDouble(west)));
                boundingBox.Add(new PointF((float)Convert.ToDouble(south), (float)Convert.ToDouble(west)));
                boundingBox.Add(new PointF((float)Convert.ToDouble(south), (float)Convert.ToDouble(east)));
            }

            DateTime startLambdaTime = DateTime.Now;
            List<Tile> tiles = new List<Tile>();
            if (!string.IsNullOrEmpty(encodedCompletedTiles) || !string.IsNullOrEmpty(encodedPartialTiles))
            {
                if (!string.IsNullOrEmpty(area.EncodedPolygon))
                {
                    area.Points = GooglePoints.DecodeBase64(area.EncodedPolygon).Select(x => new LocationPoint(x.Latitude, x.Longitude)).ToList();
                }

                if (boundingBox == null || boundingBox.Count() == 0)
                {
                    tiles.AddRange(GooglePoints.DecodeBase64(encodedCompletedTiles).Select(x => new Tile() { Row = (int)x.Latitude, Column = (int)x.Longitude, IsPartialTile = false }).ToList());
                    tiles.AddRange(GooglePoints.DecodeBase64(encodedPartialTiles).Select(x => new Tile() { Row = (int)x.Latitude, Column = (int)x.Longitude, IsPartialTile = true }).ToList());
                }
                else
                {
                    RasterizeObject rasterizeObject = this.GetCoordinateTile(area.Points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList(), true, boundingBox);
                    tiles = rasterizeObject.tiles;
                    area.Points = new WKTReader().Read(rasterizeObject.intersectionPolygon).Coordinates.Select(x => new LocationPoint(x.X, x.Y)).ToList();
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(area.EncodedPolygon))
                {
                    area.Points = GooglePoints.Decode(area.EncodedPolygon).Select(x => new LocationPoint(x.Latitude, x.Longitude)).ToList();
                }

                RasterizeObject rasterizeObject = this.GetCoordinateTile(area.Points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList(), true, boundingBox);
                tiles = rasterizeObject.tiles;

                if (boundingBox != null && boundingBox.Count() > 0)
                {
                    area.Points = new WKTReader().Read(rasterizeObject.intersectionPolygon).Coordinates.Select(x => new LocationPoint(x.X, x.Y)).ToList();
                }
            }
            DateTime endLambdaTime = DateTime.Now;

            //if (tiles == null || tiles.Count() == 0)
            //{
            //    throw new Exception("Unable To Calculate Tiles");
            //}

            DateTime startQueryTime = DateTime.Now;
            GetRegionByPropertyWrapper listing = getRegionByProperty(tiles, beds, bathsFull, bathsHalf, propertyAddressId, averageValue, averageRent).Result;
            DateTime endQueryTime = DateTime.Now;

            foreach (var item in listing.CompleteProperties)
            {
                if (item != null)
                {
                    listings.Add(new Listing
                    {
                        Name = item.PropertyAddressName,
                        Lat = item.Latitude,
                        Lng = item.Longitude
                    });
                }
            }

            Stopwatch containsStopwatch = Stopwatch.StartNew();
            Parallel.ForEach(listing.PartialProperties, item =>
            {
                if (item != null && this.polyCheck(new Vector2((float)item.Latitude, (float)item.Longitude), area.Points.Select(x => new Vector2((float)x.Lat, (float)x.Lng)).ToArray()))
                {
                    listings.Add(new Listing
                    {
                        Name = item.PropertyAddressName,
                        Lat = item.Latitude,
                        Lng = item.Longitude
                    });
                }
            });
            containsStopwatch.Stop();

            List<ListingMaster> customProperties = listings.Select(x => new ListingMaster()
            {
                Lat = x.Lat,
                Lng = x.Lng,
                Name = x.Name
            }).ToList();

            return new GetListingWrapper
            {
                PropertyCount = listing.TotalRecordCount,
                ScanCount = listing.ScanCount,
                ConsumedCapacityCount = listing.ConsumedCapacityCount,
                TotalQueryExecutionTime = (endQueryTime - startQueryTime).TotalMilliseconds,
                TotalLambdaExecutionTime = (endLambdaTime - startLambdaTime).TotalMilliseconds,
                TotalContainsExecutionTime = containsStopwatch.ElapsedMilliseconds,
                Properties = customProperties,
            };
        }

        public async Task<List<AreaMaster>> GetArea()
        {
            Dictionary<string, Condition> queryCondition = new Dictionary<string, Condition>();
            //queryCondition.Add("IsPredefine", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = "1" } } });

            var request = new ScanRequest
            {
                TableName = areaMasterTableName,
                IndexName = "AreaIDIndex",
                ScanFilter = queryCondition
            };

            var response = await dynamoDBClient.ScanAsync(request);

            List<AreaMaster> listings = AreaMaster.ConvertToEntity(response.Items);

            return listings;
        }

        public async Task<GetAreaListingWrapper> GetArea(string id, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null)
        {
            DateTime startQueryTime = DateTime.Now;
            List<AreaMaster> listingArea = new List<AreaMaster>();
            List<List<Property>> areaProperties = new List<List<Property>>();

            Dictionary<string, Condition> areaKeyConditions = new Dictionary<string, Condition>();
            Dictionary<string, Condition> areaQueryFilter = new Dictionary<string, Condition>();
            areaKeyConditions.Add("AreaID", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(id) } });
            areaQueryFilter.Add("IsPredefine", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = "1" } } });

            QueryRequest queryRequest = new QueryRequest()
            {
                TableName = areaMasterTableName,
                ReturnConsumedCapacity = "TOTAL",
                KeyConditions = areaKeyConditions,
                QueryFilter = areaQueryFilter
            };

            var result = dynamoDBClient.QueryAsync(queryRequest).Result;
            listingArea = AreaMaster.ConvertToEntity(result.Items);
            DateTime endQueryTime = DateTime.Now;


            if (listingArea.Count() > 0)
            {
                var tempArea = new Area()
                {
                    EncodedPolygon = listingArea.First().EncodedPolygon,
                    Points = GooglePoints.DecodeBase64(listingArea.First().EncodedPolygon).Select(x => new LocationPoint(x.Latitude, x.Longitude)).ToList()
                };

                GetListingWrapper output = await GetListing(tempArea, north, east, south, west, beds, bathsFull, bathsHalf, propertyAddressId, averageValue, averageRent, listingArea.First().EncodedCompletedTiles, listingArea.First().EncodedPartialTiles);


                foreach (var area in listingArea)
                {
                    area.EncodedPolygon = ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(area.EncodedPolygon));
                    area.EncodedCompletedTiles = null;
                    area.EncodedPartialTiles = null;

                }
                return new GetAreaListingWrapper
                {
                    PropertyCount = output.PropertyCount,
                    ScanCount = output.ScanCount,
                    ConsumedCapacityCount = output.ConsumedCapacityCount,
                    TotalQueryExecutionTime = output.TotalQueryExecutionTime,
                    TotalLambdaExecutionTime = output.TotalLambdaExecutionTime,
                    TotalContainsExecutionTime = output.TotalContainsExecutionTime,
                    TotalAreaQueryTime = (endQueryTime - startQueryTime).TotalMilliseconds,
                    Properties = output.Properties,
                    Area = listingArea,
                };
            }
            else
            {
                return null;
            }
        }

        #region Public Function
        public async Task CreateTempTable(string tableName, List<AttributeDefinition> attributeDefinition, List<GlobalSecondaryIndex> globalSecondaryIndexes, List<LocalSecondaryIndex> localSecondaryIndexes, string hashKey = "Tile", string SortKey = "Guid")
        {
            List<KeySchemaElement> keySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = hashKey, KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = SortKey, KeyType = KeyType.RANGE }
            };

            if (globalSecondaryIndexes == null)
            {
                globalSecondaryIndexes = new List<GlobalSecondaryIndex>();
            }

            if (localSecondaryIndexes == null)
            {
                localSecondaryIndexes = new List<LocalSecondaryIndex>();
            }

            if (attributeDefinition == null || attributeDefinition.Count() == 0)
            {
                attributeDefinition = new List<AttributeDefinition>()
                {
                    new AttributeDefinition { AttributeName = hashKey, AttributeType = ScalarAttributeType.S },
                    new AttributeDefinition { AttributeName = SortKey, AttributeType = ScalarAttributeType.S }
                };
            }

            var tableResponse = await this.dynamoDBClient.ListTablesAsync();
            if (!tableResponse.TableNames.Contains(tableName))
            {
                await this.dynamoDBClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tableName,
                    ProvisionedThroughput = new ProvisionedThroughput
                    {

                        ReadCapacityUnits = 3,
                        WriteCapacityUnits = 100
                    },
                    KeySchema = keySchema,
                    AttributeDefinitions = attributeDefinition,
                    LocalSecondaryIndexes = localSecondaryIndexes,
                    GlobalSecondaryIndexes = globalSecondaryIndexes
                });


                await waitUntilTableReady(tableName);
            }
        }

        public void SaveAreas(List<Area> areas)
        {
            Parallel.ForEach(areas.ChunkBy<Area>(200), (item, state, index) =>
            {
                Console.WriteLine("Initiating a new chunk. Index: " + index);
                var bulkInsert = context.CreateBatchWrite<Area>();
                bulkInsert.AddPutItems(item);
                bulkInsert.ExecuteAsync().Wait();
                Console.WriteLine("Chunk inserted successfully. Index" + index);
            });
        }

        public void SaveAreas(List<AreaMaster> areas)
        {
            Parallel.ForEach(areas.ChunkBy<AreaMaster>(200), (item, state, index) =>
            {
                Console.WriteLine("Initiating a new chunk. Index: " + index);
                var bulkInsert = context.CreateBatchWrite<AreaMaster>();
                bulkInsert.AddPutItems(item);
                bulkInsert.ExecuteAsync().Wait();
                Console.WriteLine("Chunk inserted successfully. Index" + index);
            });
        }

        public RasterizeObject GetCoordinateTile(List<PointF> points, bool withRasterize, List<PointF> boundingBox = null, int zoomlevel = 14, string encodedTiles = null)
        {
            try
            {
                RasterizeObject rasterizeObject = new RasterizeObject();
                rasterizeObject.tiles = new List<Tile>();
                object lockObj = new object();

                if (withRasterize)
                {
                    string postData = @"{""zoom"": " + zoomlevel + @",";
                    if (!string.IsNullOrEmpty(encodedTiles))
                    {
                        postData += postData = @"""encodedTile"": """ + encodedTiles + @"""";
                    }
                    else if (points != null && points.Count() > 0)
                    {
                        string encodedString = GooglePoints.EncodeBase64(points.Select(x => new CoordinateEntity(x.X, x.Y)));
                        postData += @"""encodedPolygon"": """ + encodedString + @"""";
                    }

                    if (boundingBox != null && boundingBox.Count() > 0)
                    {
                        string boundingBoxPostData = JSONHelper.GetString(boundingBox.Select(x => new LocationPoint { Lat = Convert.ToDecimal(x.X), Lng = Convert.ToDecimal(x.Y) }).ToList());
                        postData += @",""boundingBox"": " + boundingBoxPostData;
                    }

                    postData += "}";

                    string responseFromServer = responseFromServer = this.PostData(LambdaResourceType.areas, postData);
                    if (boundingBox != null && boundingBox.Count() > 0)
                    {
                        return JSONHelper.GetObject<RasterizeObject>(responseFromServer);
                    }
                    else
                    {
                        RasterizeObject obj = new RasterizeObject();
                        obj.tiles = JSONHelper.GetObject<List<Tile>>(responseFromServer);
                        return obj;
                    }
                }
                else
                {
                    List<Tile> tilesCoordinates = new List<Tile>();

                    foreach (var point in points)
                    {
                        tilesCoordinates.Add(new Tile()
                        {
                            Zoom = zoomlevel,
                            Lat = point.X,
                            Lng = point.Y
                        });
                    };

                    Parallel.ForEach(tilesCoordinates.ChunkBy(200), tilesCoordinate =>
                    {
                        string postData = JSONHelper.GetString(tilesCoordinate);
                        string responseFromServer = this.PostData(LambdaResourceType.listings, postData);
                        //string responseFromServer = this.CallRasterizationLambda(postData);

                        lock (lockObj)
                        {
                            rasterizeObject.tiles.AddRange(JSONHelper.GetObject<List<Tile>>(responseFromServer));
                        }
                    });
                }

                return rasterizeObject;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string CallRasterizationLambda(string postData)
        {
            postData = @"{""resource"": ""/areas"",""body"": " + postData + "}";

            InvokeRequest ir = new InvokeRequest
            {
                FunctionName = this.RasterizeLambdaName,
                InvocationType = InvocationType.RequestResponse,
                Payload = postData
            };

            InvokeResponse response = lambdaClient.InvokeAsync(ir).Result;

            var sr = new StreamReader(response.Payload);
            JsonReader reader = new JsonTextReader(sr);

            var serilizer = new JsonSerializer();
            return serilizer.Deserialize(reader).ToString();
        }

        public string PostData(LambdaResourceType lambdaResourceType, string postData)
        {
            postData = @"{
              ""resource"": ""/" + (lambdaResourceType == LambdaResourceType.listings ? "listings" : "areas") + @""",
              ""body"": " + postData + @"
            }";

            InvokeRequest ir = new InvokeRequest
            {
                FunctionName = "rasterizer",
                InvocationType = InvocationType.RequestResponse,
                Payload = postData
            };

            InvokeResponse response = this.lambdaClient.InvokeAsync(ir).Result;

            string sr = new StreamReader(response.Payload).ReadToEnd();

            return sr.Substring(sr.IndexOf("\"") + 1).Substring(0, sr.LastIndexOf("\"") - 1).Replace("\\", "");


            //WebRequest request = WebRequest.Create(url);
            //request.Method = "POST";
            //byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            //request.ContentType = "application/x-www-form-urlencoded";
            //request.ContentLength = byteArray.Length;
            //Stream dataStream = request.GetRequestStream();
            //dataStream.Write(byteArray, 0, byteArray.Length);
            //dataStream.Close();
            //WebResponse response = request.GetResponse();
            //dataStream = response.GetResponseStream();
            //StreamReader reader = new StreamReader(dataStream);
            //string responseFromServer = reader.ReadToEnd();
            //reader.Close();
            //dataStream.Close();
            //response.Close();

            //return responseFromServer;
        }

        public string GetTileStr(int row, int column)
        {
            return "(" + row + "," + column + ")";
        }

        #endregion

        #region Private Function

        private async Task waitUntilTableReady(string tableName)
        {
            bool isTableAvailable = false;
            while (!isTableAvailable)
            {
                Console.WriteLine("Waiting for table to be active...");
                Thread.Sleep(5000);
                var tableStatus = await this.dynamoDBClient.DescribeTableAsync(tableName);
                isTableAvailable = tableStatus.Table.TableStatus == "ACTIVE";
            }
        }

        private List<Area> transformRegion(Area area, List<Tile> tiles)
        {
            List<Area> areas = new List<Area>();

            List<string> locationPoints = new List<string>();
            foreach (LocationPoint point in area.Points)
            {
                locationPoints.Add(point.Lat + " " + point.Lng);
            }

            foreach (Tile tile in tiles)
            {
                Area tempObj = new Area();

                tempObj = (Area)area.Clone();

                tempObj.Tile = GetTileStr((int)tile.Row, (int)tile.Column);
                tempObj.Type = RecordType.Area;
                tempObj.OriginalPolygon = "((" + string.Join(", ", locationPoints) + "))";
                tempObj.Tiles = null;
                tempObj.OriginalPolygon = "";
                tempObj.Points = null;
                tempObj.IsPartialTiles = tile.IsPartialTile;

                areas.Add(tempObj);
            }

            return areas;
        }

        private async Task<List<AreaMaster>> getRegionByArea(decimal lat, decimal lng)
        {
            List<PointF> points = new List<PointF>();
            points.Add(new PointF
            {
                X = (float)lat,
                Y = (float)lng
            });

            List<Tile> coordinates = GetCoordinateTile(points, false).tiles;

            return await getRegionByArea(coordinates.Select(x => new Point((int)x.Row, (int)x.Column)).ToList());
        }

        private async Task<List<AreaMaster>> getRegionByArea(List<Point> points)
        {
            List<List<Area>> allAreas = new List<List<Area>>();
            List<List<AreaMaster>> allAreasMaster = new List<List<AreaMaster>>();
            Parallel.ForEach(points, point =>
            {
                Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
                keyConditions.Add("Tile", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(GetTileStr(point.X, point.Y)) } });

                QueryRequest queryRequest = new QueryRequest()
                {
                    TableName = areaTableName,
                    IndexName = "AreaIDIndex",
                    KeyConditions = keyConditions,
                };

                var result = dynamoDBClient.QueryAsync(queryRequest).Result;
                allAreas.Add(Area.ConvertToEntity(result.Items));
            });


            Parallel.ForEach(allAreas.SelectMany(x => x).Select(x => x.AreaID).Distinct(), areaId =>
            {
                Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
                keyConditions.Add("AreaID", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(areaId) } });

                QueryRequest queryRequest = new QueryRequest()
                {
                    TableName = areaMasterTableName,
                    IndexName = "AreaPolygonIndex",
                    KeyConditions = keyConditions,
                };

                var result = dynamoDBClient.QueryAsync(queryRequest).Result;
                allAreasMaster.Add(AreaMaster.ConvertToEntity(result.Items));
            });

            return allAreasMaster.SelectMany(x => x).ToList();
        }

        private async Task<GetRegionByPropertyWrapper> getRegionByProperty(List<Tile> points, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null)
        {
            int TotalRecordCount = 0;
            int ScanCount = 0;
            double ConsumedCapacityCount = 0;
            List<List<Property>> partialProperty = new List<List<Property>>();
            List<List<Property>> completeProperty = new List<List<Property>>();
            try
            {
                foreach (var obj in points)
                {
                    string currentTile = GetTileStr((int)obj.Row, (int)obj.Column);

                    Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
                    keyConditions.Add("Tile", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(currentTile) } });

                    Dictionary<string, Condition> queryFilter = new Dictionary<string, Condition>();

                    if (!string.IsNullOrEmpty(beds))
                    {
                        queryFilter.Add("Beds", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = beds } } });
                    }
                    if (!string.IsNullOrEmpty(bathsFull))
                    {
                        queryFilter.Add("BathsFull", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = bathsFull } } });
                    }
                    if (!string.IsNullOrEmpty(bathsHalf))
                    {
                        queryFilter.Add("BathsHalf", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = bathsHalf } } });
                    }
                    if (!string.IsNullOrEmpty(propertyAddressId))
                    {
                        queryFilter.Add("PropertyAddressID", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = propertyAddressId } } });
                    }
                    if (!string.IsNullOrEmpty(averageValue))
                    {
                        queryFilter.Add("AverageValue", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = averageValue } } });
                    }
                    if (!string.IsNullOrEmpty(averageRent))
                    {
                        queryFilter.Add("AverageRent", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { S = averageRent } } });
                    }

                    var request = new QueryRequest
                    {
                        TableName = propertyTableName,
                        ReturnConsumedCapacity = "TOTAL",
                        KeyConditions = keyConditions,
                        QueryFilter = queryFilter,
                        AttributesToGet = new List<string> { "Latitude", "Longitude", "Tile", "PropertyAddressName" },
                        Select = "SPECIFIC_ATTRIBUTES"

                    };
                    try
                    {
                        QueryResponse response = this.dynamoDBClient.QueryAsync(request).Result;

                        TotalRecordCount += response.Count;
                        ScanCount += response.ScannedCount;
                        ConsumedCapacityCount += response.ConsumedCapacity.CapacityUnits;

                        if (obj.IsPartialTile)
                        {
                            partialProperty.Add(Property.ConvertToEntity(response.Items));
                        }
                        else
                        {
                            completeProperty.Add(Property.ConvertToEntity(response.Items));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new GetRegionByPropertyWrapper
            {
                PartialProperties = partialProperty.SelectMany(x => x).ToList(),
                CompleteProperties = completeProperty.SelectMany(x => x).ToList(),
                TotalRecordCount = TotalRecordCount,
                ConsumedCapacityCount = ConsumedCapacityCount,
                ScanCount = ScanCount
            };
        }

        private List<AreaMaster> filterRegionList(List<AreaMaster> areas, decimal lat, decimal lng)
        {
            List<AreaMaster> filteredAreas = new List<AreaMaster>();

            Parallel.ForEach(areas, area =>
            {
                if (isPointInPolygon(GooglePoints.DecodeBase64(area.EncodedPolygon).Select(x => new LocationPoint(x.Latitude, x.Longitude)).ToList(), lat, lng))
                {
                    area.EncodedPolygon = ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(area.EncodedPolygon));
                    filteredAreas.Add(area);
                }
            });

            return filteredAreas;
        }

        private bool isPointInPolygon(List<LocationPoint> points, decimal lat, decimal lng)
        {
            decimal[] polyX = points.Select(a => a.Lat).ToArray();
            decimal[] polyY = points.Select(a => a.Lng).ToArray();

            decimal x = lat;
            decimal y = lng;

            int polyCorners = points.Count;
            int i, j = polyCorners - 1;
            bool oddNodes = false;

            for (i = 0; i < polyCorners; i++)
            {
                if (polyY[i] < y && polyY[j] >= y || polyY[j] < y && polyY[i] >= y)
                {
                    if (polyX[i] + (y - polyY[i]) / (polyY[j] - polyY[i]) * (polyX[j] - polyX[i]) < x)
                    {
                        oddNodes = !oddNodes;
                    }
                }
                j = i;
            }

            return oddNodes;
        }

        public bool polyCheck(Vector2 v, Vector2[] p)
        {
            int j = p.Length - 1;
            bool c = false;
            for (int i = 0; i < p.Length; j = i++) c ^= p[i].Y > v.Y ^ p[j].Y > v.Y && v.X < (p[j].X - p[i].X) * (v.Y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X;
            return c;
        }

        private IEnumerable<Point> getPointsOnLine(PointF initalPoint, PointF finalPoint)

        {

            float x0 = initalPoint.X;

            float y0 = initalPoint.Y;

            float x1 = finalPoint.X;

            float y1 = finalPoint.Y;



            float dy = Math.Abs(y0 - y1);

            float dx = Math.Abs(x0 - x1);

            bool steep = dy > dx;

            if (steep)
            {

                bool directionDown = y0 > y1;

                float m = dx / dy;

                //return the point tile as it is

                var initialTile = new Point(Convert.ToInt32(Math.Floor(x0)), Convert.ToInt32(Math.Floor(y0)));

                var finalTile = new Point(Convert.ToInt32(Math.Floor(x1)), Convert.ToInt32(Math.Floor(y1)));

                yield return initialTile;

                if (initialTile == finalTile || initialTile.Y == finalTile.Y + 1)

                {

                    yield return finalTile;

                    yield break;

                }

                float xTemp = directionDown ? (x0 - (y0 % 1) * m) - m : (1 - (y0 % 1)) * m + x0; //x-coordinates for second tile

                if (directionDown)

                {

                    for (int y = initialTile.Y - 1; y > finalTile.Y; y--)

                    {

                        int x = Convert.ToInt32(Math.Floor(xTemp));

                        yield return new Point(x, y);

                        xTemp -= m;

                    }

                }

                else

                {

                    for (int y = initialTile.Y + 1; y < finalTile.Y; y++)

                    {

                        int x = Convert.ToInt32(Math.Floor(xTemp));

                        yield return new Point(x, y);

                        xTemp += m;

                    }

                }

                yield return finalTile;

            }

            else

            {

                bool directionLeft = x0 > x1;

                float m = dy / dx;



                //return the point tile as it is

                var initialTile = new Point(Convert.ToInt32(Math.Floor(x0)), Convert.ToInt32(Math.Floor(y0)));

                var finalTile = new Point(Convert.ToInt32(Math.Floor(x1)), Convert.ToInt32(Math.Floor(y1)));

                yield return initialTile;

                if (initialTile == finalTile || initialTile.X == finalTile.X + 1)

                {

                    yield return finalTile;

                    yield break;

                }

                float yTemp = directionLeft ? (y0 - (x0 % 1) * m) - m : (1 - (x0 % 1)) * m + y0; //x-coordinates for second tile

                if (directionLeft)

                {

                    for (int x = initialTile.X - 1; x > finalTile.X; x--)

                    {

                        int y = Convert.ToInt32(Math.Floor(yTemp));

                        yield return new Point(x, y);

                        yTemp -= m;

                    }

                }

                else

                {

                    for (int x = initialTile.X + 1; x < finalTile.X; x++)

                    {

                        int y = Convert.ToInt32(Math.Floor(yTemp));

                        yield return new Point(x, y);

                        yTemp += m;

                    }

                }

                yield return finalTile;

            }

            yield break;

        }

        #endregion
    }
}