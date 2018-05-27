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

        Task<List<Area>> Get(decimal lat, decimal lng);

        Task SaveListing(Listing listing);

        Task<List<Listing>> GetListing(Area listing);

        Task<List<AreaListing>> GetArea();

        Task<dynamic> GetArea(string id, string beds = null, string bathsFull = null, string bathsHalf = null, string propertyAddressId = null, string averageValue = null, string averageRent = null);
    }
}
