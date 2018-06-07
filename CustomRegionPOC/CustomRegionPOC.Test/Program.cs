using System;

namespace CustomRegionPOC.Test
{
    class Program
    {
        static void Main(string[] args)
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
    }
}
