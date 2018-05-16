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

namespace CustomRegionPOC.Service
{
    public class RegionService : IRegionService
    {
        private AmazonDynamoDBClient dynamoDBClient;
        private DynamoDBContext context;
        private string stSortKey;

        public RegionService(IConfiguration configuration)
        {
            var credentials = new BasicAWSCredentials(configuration["AWS:AccessKey"], configuration["AWS:SecretKey"]);
            this.dynamoDBClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);

            createTempTable("tile_listing_region").Wait();

            context = new DynamoDBContext(this.dynamoDBClient);
        }

        public async Task Create(Region region)
        {
            List<Tuple<PointF, PointF>> tuples = generateTileTuples(region.Points);
            List<Region> regions = this.transformRegion(region, this.rasterize(tuples));

            var chunkRegion = regions.ChunkBy<Region>(200);

            Parallel.ForEach(chunkRegion, item =>
            {
                var bulkInsert = context.CreateBatchWrite<Region>();
                bulkInsert.AddPutItems(item);
                bulkInsert.ExecuteAsync();
            });
        }

        public async Task<List<Region>> Get(decimal lat, decimal lng)
        {
            return this.filterRegionList(await this.getRegion(lat, lng, RecordType.Region), lat, lng);
        }

        public async Task SaveListing(Listing listing)
        {
            dynamic coordinates = getCoordinateTile(listing.Lat, listing.Lng);

            await context.SaveAsync<Region>(new Region
            {
                Type = RecordType.Listing,
                CreateDate = DateTime.UtcNow,
                Name = listing.Name,
                Latitude = listing.Lat,
                Longitude = listing.Lng,
                Tile = this.getTileStr((int)coordinates["tileX"], (int)coordinates["tileY"])
            });
        }

        public async Task<List<Listing>> GetListing(Region region)
        {
            List<Listing> listings = new List<Listing>();

            List<Tuple<PointF, PointF>> tuples = generateTileTuples(region.Points);
            List<Point> tiles = this.rasterize(tuples);

            Parallel.ForEach(tiles, tile =>
            {
                List<Region> listing = getRegion(tile.X, tile.Y, RecordType.Listing).Result;
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
                    CreateDate = DateTime.UtcNow,
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
                    Type = RecordType.Region,
                    Points = region.Points,
                    LocationPoints = "((" + string.Join(", ", locationPoints) + "))",
                    Tiles = region.Tiles,
                    CreateDate = DateTime.UtcNow,
                });
            }

            return regions;
        }

        private async Task createTempTable(string tableName)
        {
            string hashKey = "Tile";
            string SortKey1 = "CreateDate";
            string SortKey2 = "Type";

            var tableResponse = await this.dynamoDBClient.ListTablesAsync();
            if (!tableResponse.TableNames.Contains(tableName))
            {
                await this.dynamoDBClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tableName,
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 3,
                        WriteCapacityUnits = 1
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

        private List<Tuple<PointF, PointF>> generateTileTuples(List<LocationPoint> points)
        {
            List<Tuple<PointF, PointF>> tuple = new List<Tuple<PointF, PointF>>();

            Tile[] actualTiles = new Tile[points.Count()];

            Parallel.ForEach(points, (point, state, index) =>
            {
                dynamic coordinates = getCoordinateTile(point.Lat, point.Lng);

                List<decimal> bound = new List<decimal>();

                actualTiles[index] = new Tile
                {
                    Row = (float)coordinates["tileX"],
                    Column = (float)coordinates["tileY"],
                    Bound1 = coordinates["bound1"],
                    Bound2 = coordinates["bound2"],
                    Bound3 = coordinates["bound3"],
                    Bound4 = coordinates["bound4"],
                };
            });

            for (int i = 1; i < actualTiles.Count(); i++)
            {
                tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[i - 1].Row, actualTiles[i - 1].Column), new PointF(actualTiles[i].Row, actualTiles[i].Column)));
            }

            tuple.Add(new Tuple<PointF, PointF>(new PointF(actualTiles[actualTiles.Count() - 1].Row, actualTiles[actualTiles.Count() - 1].Column), new PointF(actualTiles[0].Row, actualTiles[0].Column)));

            return tuple;
        }

        private dynamic getCoordinateTile(decimal lat, decimal lng, int zoomlevel = 10)
        {
            #region Python Script
            string script = @"#!/usr/bin/env python

import math

class GlobalMercator(object):
	def __init__(self, tileSize=256):
		'Initialize the TMS Global Mercator pyramid'
		self.tileSize = tileSize
		self.initialResolution = 2 * math.pi * 6378137 / self.tileSize
		# 156543.03392804062 for tileSize 256 pixels
		self.originShift = 2 * math.pi * 6378137 / 2.0
		# 20037508.342789244

	def LatLonToMeters(self, lat, lon ):
		'Converts given lat/lon in WGS84 Datum to XY in Spherical Mercator EPSG:900913'

		mx = lon * self.originShift / 180.0
		my = math.log( math.tan((90 + lat) * math.pi / 360.0 )) / (math.pi / 180.0)

		my = my * self.originShift / 180.0
		return mx, my

	def MetersToLatLon(self, mx, my ):
		'Converts XY point from Spherical Mercator EPSG:900913 to lat/lon in WGS84 Datum'

		lon = (mx / self.originShift) * 180.0
		lat = (my / self.originShift) * 180.0

		lat = 180 / math.pi * (2 * math.atan( math.exp( lat * math.pi / 180.0)) - math.pi / 2.0)
		return lat, lon

	def PixelsToMeters(self, px, py, zoom):
		'Converts pixel coordinates in given zoom level of pyramid to EPSG:900913'

		res = self.Resolution( zoom )
		mx = px * res - self.originShift
		my = py * res - self.originShift
		return mx, my
		
	def MetersToPixels(self, mx, my, zoom):
		'Converts EPSG:900913 to pyramid pixel coordinates in given zoom level'
				
		res = self.Resolution( zoom )
		px = (mx + self.originShift) / res
		py = (my + self.originShift) / res
		return px, py
	
	def PixelsToTile(self, px, py):
		'Returns a tile covering region in given pixel coordinates'

		tx = int( math.ceil( px / float(self.tileSize) ) - 1 )
		ty = int( math.ceil( py / float(self.tileSize) ) - 1 )
		return tx, ty

	def PixelsToRaster(self, px, py, zoom):
		'Move the origin of pixel coordinates to top-left corner'
		
		mapSize = self.tileSize << zoom
		return px, mapSize - py
		
	def MetersToTile(self, mx, my, zoom):
		'Returns tile for given mercator coordinates'
		
		px, py = self.MetersToPixels( mx, my, zoom)
		return self.PixelsToTile( px, py)

	def TileBounds(self, tx, ty, zoom):
		'Returns bounds of the given tile in EPSG:900913 coordinates'
		
		minx, miny = self.PixelsToMeters( tx*self.tileSize, ty*self.tileSize, zoom )
		maxx, maxy = self.PixelsToMeters( (tx+1)*self.tileSize, (ty+1)*self.tileSize, zoom )
		return ( minx, miny, maxx, maxy )

	def TileLatLonBounds(self, tx, ty, zoom ):
		'Returns bounds of the given tile in latutude/longitude using WGS84 datum'

		bounds = self.TileBounds( tx, ty, zoom)
		minLat, minLon = self.MetersToLatLon(bounds[0], bounds[1])
		maxLat, maxLon = self.MetersToLatLon(bounds[2], bounds[3])
		 
		return ( minLat, minLon, maxLat, maxLon )
		
	def Resolution(self, zoom ):
		'Resolution (meters/pixel) for given zoom level (measured at Equator)'
		
		# return (2 * math.pi * 6378137) / (self.tileSize * 2**zoom)
		return self.initialResolution / (2**zoom)
		
	def ZoomForPixelSize(self, pixelSize ):
		'Maximal scaledown zoom of the pyramid closest to the pixelSize.'
		
		for i in range(30):
			if pixelSize > self.Resolution(i):
				return i-1 if i!=0 else 0 # We don't want to scale up

	def GoogleTile(self, tx, ty, zoom):
		'Converts TMS tile coordinates to Google Tile coordinates'
		
		# coordinate origin is moved from bottom-left to top-left corner of the extent
		return tx, (2**zoom - 1) - ty

	def QuadTree(self, tx, ty, zoom ):
		'Converts TMS tile coordinates to Microsoft QuadTree'
		
		quadKey = ''
		ty = (2**zoom - 1) - ty
		for i in range(zoom, 0, -1):
			digit = 0
			mask = 1 << (i-1)
			if (tx & mask) != 0:
				digit += 1
			if (ty & mask) != 0:
				digit += 2
			quadKey += str(digit)
			
		return quadKey



class GlobalGeodetic(object):
	def __init__(self, tileSize = 256):
		self.tileSize = tileSize

	def LatLonToPixels(self, lat, lon, zoom):
		'Converts lat/lon to pixel coordinates in given zoom of the EPSG:4326 pyramid'

		res = 180 / 256.0 / 2**zoom
		px = (180 + lat) / res
		py = (90 + lon) / res
		return px, py

	def PixelsToTile(self, px, py):
		'Returns coordinates of the tile covering region in pixel coordinates'

		tx = int( math.ceil( px / float(self.tileSize) ) - 1 )
		ty = int( math.ceil( py / float(self.tileSize) ) - 1 )
		return tx, ty

	def Resolution(self, zoom ):
		'Resolution (arc/pixel) for given zoom level (measured at Equator)'
		
		return 180 / 256.0 / 2**zoom
		#return 180 / float( 1 << (8+zoom) )

	def TileBounds(tx, ty, zoom):
		'Returns bounds of the given tile'
		res = 180 / 256.0 / 2**zoom
		return (
			tx*256*res - 180,
			ty*256*res - 90,
			(tx+1)*256*res - 180,
			(ty+1)*256*res - 90
		)

class MyClass:
	def go(self, zoomlevel, lat, lon):
		profile = 'mercator'
		latmax = lat
		lonmax = lon
		boundingbox = False
		
		if latmax != None and lonmax != None:
			boundingbox = (lon, lat, lonmax, latmax)
		
		tz = zoomlevel
		mercator = GlobalMercator()

		mx, my = mercator.LatLonToMeters( lat, lon )
		print 'Spherical Mercator (ESPG:900913) coordinates for lat/lon: '
		print (mx, my)
		tminx, tminy = mercator.MetersToTile( mx, my, tz )
		
		if boundingbox:
			mx, my = mercator.LatLonToMeters( latmax, lonmax )
			print 'Spherical Mercator (ESPG:900913) cooridnate for maxlat/maxlon: '
			print (mx, my)
			tmaxx, tmaxy = mercator.MetersToTile( mx, my, tz )
		else:
			tmaxx, tmaxy = tminx, tminy
		
		output = {
			'tileX' : 0,
			'tileY' : 0,
			'bound1' : 0,
			'bound2' : 0,
			'bound3' : 0,
			'bound4' : 0
		}
		
		for ty in range(tminy, tmaxy+1):
			for tx in range(tminx, tmaxx+1):
				tilefilename = '%s/%s/%s' % (tz, tx, ty)
				print tilefilename, '( TileMapService: z / x / y )'
			
				gx, gy = mercator.GoogleTile(tx, ty, tz)
				bounds = mercator.TileLatLonBounds( tx, ty, tz)
				
				output = {
					'tileX' : gx,
					'tileY' : gy,
					'bound1' : bounds[0],
					'bound2' : bounds[1],
					'bound3' : bounds[2],
					'bound4' : bounds[3]
				}
				
		return output";

            #endregion

            try
            {
                var engine = Python.CreateEngine();
                var scope = engine.CreateScope();
                var ops = engine.Operations;

                engine.Execute(script, scope);
                var pythonType = scope.GetVariable("MyClass");
                dynamic instance = ops.CreateInstance(pythonType);
                var value = instance.go(zoomlevel, lat, lng);

                decimal diffX = ((lat - Convert.ToDecimal(value["bound1"])) / (Convert.ToDecimal(value["bound3"]) - Convert.ToDecimal(value["bound1"])));
                decimal diffY = ((lng - Convert.ToDecimal(value["bound2"])) / (Convert.ToDecimal(value["bound4"]) - Convert.ToDecimal(value["bound2"])));

                value["tileX"] = value["tileX"] + diffX;
                value["tileY"] = value["tileY"] + diffY;

                return value;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<List<Region>> getRegion(decimal lat, decimal lng, RecordType type)
        {
            dynamic coordinates = getCoordinateTile(lat, lng);

            return await getRegion((int)coordinates["tileX"], (int)coordinates["tileY"], type);
        }

        private async Task<List<Region>> getRegion(int row, int column, RecordType type)
        {
            List<Region> region = new List<Region>();

            List<ScanCondition> conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("Tile", ScanOperator.Equal, getTileStr(row, column)));
            conditions.Add(new ScanCondition("Type", ScanOperator.Equal, type));

            region = await context.ScanAsync<Region>(conditions).GetRemainingAsync();

            return region;
        }

        private string getTileStr(int row, int column)
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

        private List<Point> rasterize(List<Tuple<PointF, PointF>> lines)
        {
            var list = new List<Point>();
            var innerList = new List<Point>();
            Parallel.ForEach(lines, (line) =>
            {
                var points = GetPointsOnLine(line.Item1, line.Item2);
                foreach (var point in points)
                {
                    list.Add(point);
                }
            });

            var topY = list.Max(x => x.Y);
            var bottomY = list.Min(x => x.Y);
            for (int y = bottomY + 1; y < topY; y++)
            {
                var edgeCoords = list.Where(i => i.Y == y).ToList();
                edgeCoords = edgeCoords.OrderBy(x => x.X).ToList();
                for (int i = 1; i < edgeCoords.Count(); i = i + 2)
                {
                    for (int x = edgeCoords.ElementAt(i - 1).X + 1; i < edgeCoords.Last().X; i++)
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