using CustomRegionPOC.Common.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CustomRegionPOC.Common.Service
{
    public interface IRegionService
    {
        Task Create(Area region);

        Task<List<AreaMaster>> Get(decimal lat, decimal lng);

        Task SaveListing(Listing listing);

        Task<List<Listing>> GetListing(Area area, string north = null, string east = null, string south = null, string west = null, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null);

        Task<List<AreaMaster>> GetArea();

        Task<dynamic> GetArea(string id, string north, string east, string south, string west, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null);
    }
}
