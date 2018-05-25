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
        private AmazonDynamoDBClient dynamoDBClient;
        private string tilebaseURL;
        public DynamoDBContext context;

        public RegionService(IConfiguration configuration)
        {
            var credentials = new BasicAWSCredentials(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"]);
            this.dynamoDBClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);

            CreateTempTable("tile_listing_region_v2", null, null).Wait();

            context = new DynamoDBContext(this.dynamoDBClient);

            this.tilebaseURL = configuration["AWS:Tilebase-URL"];
        }

        public async Task Create(Area region)
        {
            List<Tuple<PointF, PointF>> tuples = GenerateTileTuples(region.Points);
            List<Area> areas = this.transformRegion(region, this.Rasterize(tuples));
            SaveAreas(areas);
        }

        public async Task<List<Area>> Get(decimal lat, decimal lng)
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
                    Latitude = Convert.ToDecimal(coordinate.Lat),
                    Longitude = Convert.ToDecimal(coordinate.Lng),
                    Tile = this.GetTileStr((int)coordinate.Row, (int)coordinate.Column)
                }).Wait();
            });
        }

        public async Task<List<Listing>> GetListing(Area area)
        {
            List<Listing> listings = new List<Listing>();

            List<Tuple<PointF, PointF>> tuples = GenerateTileTuples(area.Points);
            List<Point> tiles = this.Rasterize(tuples);

            List<Property> listing = getRegionByProperty(tiles).Result;
            Parallel.ForEach(listing, item =>
            {
                if (this.isPointInPolygon(area, item.Latitude, item.Longitude))
                {
                    listings.Add(new Listing
                    {
                        Name = item.Name,
                        Lat = item.Latitude,
                        Lng = item.Longitude
                    });
                }
            });

            return listings;
        }


        public async Task<List<AreaListing>> GetAllAreas()
        {

            List<AreaListing> areaListings = new List<AreaListing>();
            List<ScanCondition> conditions = new List<ScanCondition>();
            return await context.ScanAsync<AreaListing>(conditions).GetRemainingAsync();
        }

        #region Public Function
        public async Task CreateTempTable(string tableName, List<LocalSecondaryIndex> localSecondaryIndexes, List<AttributeDefinition> attributeDefinition, string hashKey = "Tile", string SortKey = "Guid")
        {
            List<KeySchemaElement> keySchema = new List<KeySchemaElement>() {
                new KeySchemaElement { AttributeName = hashKey, KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = SortKey, KeyType = KeyType.RANGE }
            };

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
                    LocalSecondaryIndexes = localSecondaryIndexes
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
                    //Console.WriteLine(responseFromServer);
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
                areas.Add(new Area
                {
                    Tile = GetTileStr(tile.X, tile.Y),
                    Name = area.Name,
                    Type = RecordType.Area,
                    Points = area.Points,
                    OriginalPolygon = "((" + string.Join(", ", locationPoints) + "))",
                    Tiles = area.Tiles,
                    Guid = Guid.NewGuid().ToString(),
                });
            }

            return areas;
        }

        private async Task<List<Area>> getRegionByArea(decimal lat, decimal lng)
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

        private async Task<List<Area>> getRegionByArea(List<Point> points)
        {
            List<Area> area = new List<Area>();

            List<ScanCondition> conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("Tile", ScanOperator.In, points.Select(x => GetTileStr(x.X, x.Y)).ToArray()));

            area = await context.ScanAsync<Area>(conditions).GetRemainingAsync();

            return area;
        }

        private async Task<List<Property>> getRegionByProperty(List<Point> points)
        {
            List<Property> property = new List<Property>();
            try
            {
                foreach (var obj in points)
                {

                    List<ScanCondition> conditions = new List<ScanCondition>();
                    //conditions.Add(new ScanCondition("Tile", ScanOperator.In, obj.Select(x => GetTileStr(x.X, x.Y)).ToArray()));
                    conditions.Add(new ScanCondition("Tile", ScanOperator.Equal, GetTileStr(obj.X, obj.Y)));

                    property.AddRange(await context.ScanAsync<Property>(conditions).GetRemainingAsync());


                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return property;
        }

        private List<Area> filterRegionList(List<Area> areas, decimal lat, decimal lng)
        {
            List<Area> filteredAreas = new List<Area>();

            Parallel.ForEach(areas, area =>
            {
                if (isPointInPolygon(area, lat, lng))
                {
                    filteredAreas.Add(area);
                }
            });

            return filteredAreas;
        }

        private bool isPointInPolygon(Area area, decimal lat, decimal lng)
        {
            decimal[] polyX = area.Points.Select(a => a.Lat).ToArray();
            decimal[] polyY = area.Points.Select(a => a.Lng).ToArray();

            decimal x = lat;
            decimal y = lng;

            int polyCorners = area.Points.Count;
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