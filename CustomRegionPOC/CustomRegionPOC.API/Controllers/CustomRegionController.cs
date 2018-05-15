using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CustomRegionPOC.Common.Model;
using CustomRegionPOC.Common.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CustomRegionPOC.API.Controllers
{
    [Produces("application/json")]
    [Route("api/CustomRegion")]
    public class CustomRegionController : Controller
    {
        private IRegionService service;

        public CustomRegionController(IRegionService service)
        {
            this.service = service;
        }

        [HttpGet("{lat}/{lng}", Name = "Get")]
        public async Task<List<Region>> Get(decimal lat, decimal lng)
        {
            return await this.service.Get(lat, lng);
        }

        [HttpPost]
        public async Task Post([FromBody]Region region)
        {
            await this.service.Create(region);
        }

        [HttpPost]
        [Route("SaveListing")]
        public async Task SaveListing([FromBody]Listing listing)
        {
            await this.service.SaveListing(listing);
        }

        [HttpPost]
        [Route("GetListings")]
        public async Task<List<Listing>> GetListings([FromBody]Region region)
        {
            return await this.service.GetListing(region);
        }
    }
}
