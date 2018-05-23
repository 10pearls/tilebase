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

namespace CustomRegionPOC.Service
{
    public class RegionService : IRegionService
    {
        private AmazonDynamoDBClient dynamoDBClient;
        public DynamoDBContext context;
        private string tilebaseURL;

        public RegionService(IConfiguration configuration)
        {
            var credentials = new BasicAWSCredentials(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"]);
            this.dynamoDBClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);

            createTempTable("tile_listing_region_v2").Wait();

            context = new DynamoDBContext(this.dynamoDBClient);

            this.tilebaseURL = configuration["AWS:Tilebase-URL"];
        }

        public async Task Create(Region region)
        {
            List<Tuple<PointF, PointF>> tuples = generateTileTuples(region.Points);
            List<Region> regions = this.transformRegion(region, this.rasterize(tuples));
            saveRegions(regions);

        }

        public async Task<List<Region>> Get(decimal lat, decimal lng)
        {
            return this.filterRegionList(await this.getRegion(lat, lng, RecordType.Area), lat, lng);
        }

        public async Task SaveListing(Listing listing)
        {
            List<PointF> points = new List<PointF>();
            points.Add(new PointF
            {
                X = (float)listing.Lat,
                Y = (float)listing.Lng
            });

            List<Tile> coordinates = getCoordinateTile(points);

            Parallel.ForEach(coordinates, coordinate =>
            {
                context.SaveAsync<Region>(new Region
                {
                    Type = RecordType.Listing,
                    Guid = Guid.NewGuid().ToString(),
                    Name = listing.Name,
                    Latitude = listing.Lat,
                    Longitude = listing.Lng,
                    Tile = this.getTileStr((int)coordinate.Row, (int)coordinate.Column)
                }).Wait();
            });
        }

        public async Task<List<Listing>> GetListing(Region region)
        {
            List<Listing> listings = new List<Listing>();

            List<Tuple<PointF, PointF>> tuples = generateTileTuples(region.Points);
            List<Point> tiles = this.rasterize(tuples);

            List<Region> listing = getRegion(tiles, RecordType.Listing).Result;
            Parallel.ForEach(listing, item =>
            {
                if (this.pointInPolygon(region, item.Latitude, item.Longitude))
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

        #region Private Function

        private async Task WaitUntilTableReady(string tableName)
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

        private List<Region> transformRegion(Region region)
        {
            List<Region> regions = new List<Region>();

            List<string> locationPoints = new List<string>();
            foreach (LocationPoint point in region.Points)
            {
                locationPoints.Add(point.Lat + " " + point.Lng);
            }

            foreach (Tile tile in region.Tiles)
            {
                regions.Add(new Region
                {
                    Tile = "(" + tile.Row + "," + tile.Column + ")",
                    Name = region.Name,
                    Points = region.Points,
                    LocationPoints = "((" + string.Join(", ", locationPoints) + "))",
                    Tiles = region.Tiles,
                    Guid = Guid.NewGuid().ToString(),
                });
            }

            return regions;
        }

        private List<Region> transformRegion(Region region, List<Point> tiles)
        {
            List<Region> regions = new List<Region>();

            List<string> locationPoints = new List<string>();
            foreach (LocationPoint point in region.Points)
            {
                locationPoints.Add(point.Lat + " " + point.Lng);
            }

            foreach (Point tile in tiles)
            {
                regions.Add(new Region
                {
                    Tile = getTileStr(tile.X, tile.Y),
                    Name = region.Name,
                    Type = RecordType.Area,
                    Points = region.Points,
                    LocationPoints = "((" + string.Join(", ", locationPoints) + "))",
                    Tiles = region.Tiles,
                    Guid = Guid.NewGuid().ToString(),
                });
            }

            return regions;
        }

        public async Task createTempTable(string tableName, string hashKey = "Tile", string SortKey1 = "Guid")
        {
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
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = hashKey, KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = SortKey1, KeyType = KeyType.RANGE }
                    },
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition { AttributeName = hashKey, AttributeType = ScalarAttributeType.S },
                        new AttributeDefinition { AttributeName = SortKey1, AttributeType = ScalarAttributeType.S }
                    },
                });


                await WaitUntilTableReady(tableName);
            }
        }

        public List<Tuple<PointF, PointF>> generateTileTuples(List<LocationPoint> points)
        {
            List<Tuple<PointF, PointF>> tuple = new List<Tuple<PointF, PointF>>();

            List<Tile> actualTiles = this.getCoordinateTile(points.Select(x => new PointF((float)x.Lat, (float)x.Lng)).ToList());

            for (int i = 1; i < actualTiles.Count(); i++)
            {
                tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[i - 1].Row, actualTiles[i - 1].Column), new PointF(actualTiles[i].Row, actualTiles[i].Column)));
            }

            tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[actualTiles.Count() - 1].Row, actualTiles[actualTiles.Count() - 1].Column), new PointF(actualTiles[0].Row, actualTiles[0].Column)));

            return tuple;
        }

        private async Task<List<Region>> getRegion(decimal lat, decimal lng, RecordType type)
        {
            List<PointF> points = new List<PointF>();
            points.Add(new PointF
            {
                X = (float)lat,
                Y = (float)lng
            });

            List<Tile> coordinates = getCoordinateTile(points);

            return await getRegion(coordinates.Select(x => new Point((int)x.Row, (int)x.Column)).ToList(), type);
        }

        private async Task<List<Region>> getRegion(List<Point> points, RecordType type)
        {
            List<Region> region = new List<Region>();

            List<ScanCondition> conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("Tile", ScanOperator.In, points.Select(x => getTileStr(x.X, x.Y)).ToArray()));
            conditions.Add(new ScanCondition("Type", ScanOperator.Equal, type));

            region = await context.ScanAsync<Region>(conditions).GetRemainingAsync();

            return region;
        }

        public string getTileStr(int row, int column)
        {
            return "(" + row + "," + column + ")";
        }

        private List<Region> filterRegionList(List<Region> regions, decimal lat, decimal lng)
        {
            List<Region> filteredRegion = new List<Region>();

            Parallel.ForEach(regions, region =>
            {
                if (pointInPolygon(region, lat, lng))
                {
                    filteredRegion.Add(region);
                }
            });

            return filteredRegion;
        }

        private bool pointInPolygon(Region region, decimal lat, decimal lng)
        {
            decimal[] polyX = region.Points.Select(a => a.Lat).ToArray();
            decimal[] polyY = region.Points.Select(a => a.Lng).ToArray();

            decimal x = lat;
            decimal y = lng;

            int polyCorners = region.Points.Count;
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

        public List<Point> rasterize(List<Tuple<PointF, PointF>> lines)
        {
            var list = new List<Point>();
            var innerList = new List<Point>();
            foreach (var line in lines)
            {
                var points = GetPointsOnLine(line.Item1, line.Item2);
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

        private IEnumerable<Point> GetPointsOnLine(PointF initalPoint, PointF finalPoint)

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

        public void saveRegions(List<Region> regions)
        {

            var chunkRegion = regions.ChunkBy<Region>(200);

            Parallel.ForEach(chunkRegion, item =>
            {
                System.Console.WriteLine("initiating a new chunk");
                var bulkInsert = context.CreateBatchWrite<Region>();
                bulkInsert.AddPutItems(item);
                bulkInsert.ExecuteAsync().Wait();
                System.Console.WriteLine("chunk inserted successfully");
            });
        }

        public List<Tile> getCoordinateTile(List<PointF> points, int zoomlevel = 10)
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
        #endregion
    }

    public static class ListExtensions
    {
        public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }
    }
}