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

namespace CustomRegionPOC.Service
{
    public class RegionService : IRegionService
    {
        private string areaTableName = "tile_area_v2";
        private string areaMasterTableName = "tile_area_master_v2";
        private string propertyTableName = "tile_property_v2";

        private AmazonDynamoDBClient dynamoDBClient;
        private string tilebaseURL;
        public DynamoDBContext context;

        public RegionService(IConfiguration configuration)
        {
            var credentials = new BasicAWSCredentials(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"]);
            this.dynamoDBClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);

            CreateTempTable("tile_listing_region_v2", null, null, null).Wait();

            context = new DynamoDBContext(this.dynamoDBClient);

            this.tilebaseURL = configuration["AWS:Tilebase-URL"];
        }

        public async Task Create(Area region)
        {
            string areaId = Guid.NewGuid().ToString();
            region.AreaID = areaId;
            AreaMaster areaMaster = new AreaMaster()
            {
                AreaID = areaId,
                AreaName = region.AreaName,
                GUID = areaId,
                IsPredefine = false,
                OriginalPolygon = "",
                Points = region.Points
            };

            List<Task> tasks = new List<Task>();
            tasks.Add(Task.Factory.StartNew(() => { this.context.SaveAsync<AreaMaster>(areaMaster).Wait(); }));

            List<Tuple<PointF, PointF>> tuples = GenerateTileTuples(region.Points);
            List<Area> areas = this.transformRegion(region, this.Rasterize(tuples));
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

            List<Tile> coordinates = GetCoordinateTile(points);

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

        public async Task<List<Listing>> GetListing(Area area, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null)
        {
            List<Listing> listings = new List<Listing>();

            List<Tuple<PointF, PointF>> tuples = GenerateTileTuples(area.Points);
            List<Point> tiles = this.Rasterize(tuples);

            List<Property> listing = getRegionByProperty(tiles, north, east, south, west, beds, bathsFull, bathsHalf, propertyAddressId, averageValue, averageRent).Result;
            Parallel.ForEach(listing, item =>
            {
                if (this.isPointInPolygon(area.Points, item.Latitude, item.Longitude))
                {
                    listings.Add(new Listing
                    {
                        Name = item.PropertyAddressName,
                        Lat = item.Latitude,
                        Lng = item.Longitude
                    });
                }
            });

            return listings;
        }

        public async Task<List<AreaMaster>> GetArea()
        {
            var request = new ScanRequest
            {
                TableName = areaMasterTableName,
                IndexName = "AreaIDIndex"
            };

            var response = await dynamoDBClient.ScanAsync(request);

            List<AreaMaster> listings = AreaMaster.ConvertToEntity(response.Items);

            return listings;
        }

        public async Task<dynamic> GetArea(string id, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null)
        {
            List<Task> tasks = new List<Task>();

            List<AreaMaster> listingArea = new List<AreaMaster>();
            List<List<Property>> areaProperties = new List<List<Property>>();

            tasks.Add(new TaskFactory().StartNew(() =>
            {
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
            }));

            DateTime startDate = DateTime.Now;
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

            if (!string.IsNullOrEmpty(south) && !string.IsNullOrEmpty(north) && !string.IsNullOrEmpty(east) && !string.IsNullOrEmpty(west))
            {
                queryFilter.Add("Latitude", new Condition() { ComparisonOperator = "Between", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = south }, new AttributeValue() { N = north } } });
                queryFilter.Add("Longitude", new Condition() { ComparisonOperator = "Between", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = west }, new AttributeValue() { N = east } } });
            }


            //Parallel.For(0, 11, segment =>
            //{
            //    string innerId = id + "-" + segment;
            Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
            keyConditions.Add("AreaID", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(id) } });
            keyConditions.Add("IsPredefine", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = "1" } } });

            var request = new QueryRequest
            {
                TableName = propertyTableName,
                IndexName = "AreaIDIndex",
                ReturnConsumedCapacity = "TOTAL",
                KeyConditions = keyConditions,
                QueryFilter = queryFilter,
                AttributesToGet = new List<string> { "Latitude", "Longitude" },
                Select = "SPECIFIC_ATTRIBUTES"

            };
            var response = dynamoDBClient.QueryAsync(request).Result;

            areaProperties.Add(Property.ConvertToEntity(response.Items));
            //});

            Task.WaitAll(tasks.ToArray());

            DateTime endDate = DateTime.Now;

            return new
            {
                Area = listingArea,
                PropertyCount = areaProperties.SelectMany(x => x).ToList().Count(),
                Properties = areaProperties.SelectMany(x => x).Select(x => new { Latitude = x.Latitude, Longitude = x.Longitude }).ToList(),
                ScannedCount = response.ScannedCount,
                ReturnConsumedCapacity = response.ConsumedCapacity,
                TotalQueryExecutionTime = (endDate - startDate).TotalMilliseconds
            };
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

        public List<Tuple<PointF, PointF>> GenerateTileTuples(List<LocationPoint> points)
        {
            List<Tuple<PointF, PointF>> tuple = new List<Tuple<PointF, PointF>>();

            List<Tile> actualTiles = this.GetCoordinateTile(points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList());

            for (int i = 1; i < actualTiles.Count(); i++)
            {
                tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[i - 1].Row, actualTiles[i - 1].Column), new PointF(actualTiles[i].Row, actualTiles[i].Column)));
            }

            tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[actualTiles.Count() - 1].Row, actualTiles[actualTiles.Count() - 1].Column), new PointF(actualTiles[0].Row, actualTiles[0].Column)));

            return tuple;
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

        public List<Tile> GetCoordinateTile(List<PointF> points, int zoomlevel = 14)
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

            try
            {
                List<Tile> tiles = new List<Tile>();
                object lockObj = new object();

                Parallel.ForEach(tilesCoordinates.ChunkBy(200), tilesCoordinate =>
                {
                    WebRequest request = WebRequest.Create(tilebaseURL);
                    request.Method = "POST";
                    string postData = JSONHelper.GetString(tilesCoordinate);
                    byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = byteArray.Length;
                    Stream dataStream = request.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();
                    WebResponse response = request.GetResponse();
                    dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    reader.Close();
                    dataStream.Close();
                    response.Close();

                    lock (lockObj)
                    {
                        tiles.AddRange(JSONHelper.GetObject<List<Tile>>(responseFromServer));
                    }
                });

                return tiles;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public List<Point> Rasterize(List<Tuple<PointF, PointF>> lines)
        {
            var list = new List<Point>();
            var innerList = new List<Point>();
            foreach (var line in lines)
            {
                var points = getPointsOnLine(line.Item1, line.Item2);
                foreach (var point in points)
                {
                    list.Add(point);
                }
            };

            var topY = list.Max(x => x.Y);
            var bottomY = list.Min(x => x.Y);
            for (int y = bottomY + 1; y < topY; y++)
            {
                var edgeCoords = list.Where(i => i.Y == y).ToList();
                edgeCoords = edgeCoords.OrderBy(x => x.X).ToList();
                for (int i = 1; i < edgeCoords.Count(); i = i + 2)
                {
                    for (int x = edgeCoords.ElementAt(i - 1).X + 1; x < edgeCoords.ElementAt(i).X; x++)
                    {
                        innerList.Add(new Point(x, y));
                    }

                }
            }
            list.AddRange(innerList);
            list = list.Distinct().ToList();

            return list;
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

        private List<Area> transformRegion(Area area, List<Point> tiles)
        {
            List<Area> areas = new List<Area>();

            List<string> locationPoints = new List<string>();
            foreach (LocationPoint point in area.Points)
            {
                locationPoints.Add(point.Lat + " " + point.Lng);
            }

            foreach (Point tile in tiles)
            {
                Area tempObj = new Area();

                tempObj = (Area)area.Clone();

                tempObj.Tile = GetTileStr(tile.X, tile.Y);
                tempObj.Type = RecordType.Area;
                tempObj.OriginalPolygon = "((" + string.Join(", ", locationPoints) + "))";
                tempObj.Tiles = null;
                tempObj.OriginalPolygon = "";
                tempObj.Points = null;

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

            List<Tile> coordinates = GetCoordinateTile(points);

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
                    KeyConditions = keyConditions,
                };

                var result = dynamoDBClient.QueryAsync(queryRequest).Result;
                allAreasMaster.Add(AreaMaster.ConvertToEntity(result.Items));
            });

            return allAreasMaster.SelectMany(x => x).ToList();
        }

        private async Task<List<Property>> getRegionByProperty(List<Point> points, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null)
        {
            List<List<Property>> property = new List<List<Property>>();
            try
            {
                Parallel.ForEach(points, obj =>
                {
                    Dictionary<string, Condition> keyConditions = new Dictionary<string, Condition>();
                    keyConditions.Add("Tile", new Condition() { ComparisonOperator = "EQ", AttributeValueList = new List<AttributeValue>() { new AttributeValue(GetTileStr(obj.X, obj.Y)) } });

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

                    if (!string.IsNullOrEmpty(south) && !string.IsNullOrEmpty(north) && !string.IsNullOrEmpty(east) && !string.IsNullOrEmpty(west))
                    {
                        queryFilter.Add("Latitude", new Condition() { ComparisonOperator = "Between", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = south }, new AttributeValue() { N = north } } });
                        queryFilter.Add("Longitude", new Condition() { ComparisonOperator = "Between", AttributeValueList = new List<AttributeValue>() { new AttributeValue() { N = west }, new AttributeValue() { N = east } } });
                    }

                    var request = new QueryRequest
                    {
                        TableName = propertyTableName,
                        ReturnConsumedCapacity = "TOTAL",
                        KeyConditions = keyConditions,
                        QueryFilter = queryFilter,
                        AttributesToGet = new List<string> { "Latitude", "Longitude" },
                        Select = "SPECIFIC_ATTRIBUTES"

                    };
                    var response = dynamoDBClient.QueryAsync(request).Result;

                    property.Add(Property.ConvertToEntity(response.Items));
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return property.SelectMany(x => x).ToList();
        }

        private List<AreaMaster> filterRegionList(List<AreaMaster> areas, decimal lat, decimal lng)
        {
            List<AreaMaster> filteredAreas = new List<AreaMaster>();

            Parallel.ForEach(areas, area =>
            {
                if (isPointInPolygon(area.Points, lat, lng))
                {
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