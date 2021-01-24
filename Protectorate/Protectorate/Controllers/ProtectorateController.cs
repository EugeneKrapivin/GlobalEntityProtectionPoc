using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using Protectorate.Interfaces;

namespace Protectorate.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProtectorateController : ControllerBase
    {
        private readonly ILogger<ProtectorateController> _logger;
        private readonly IClusterClient _clusterClient;

        public ProtectorateController(ILogger<ProtectorateController> logger, IClusterClient clusterClient)
        {
            _logger = logger;
            _clusterClient = clusterClient;
        }

        [HttpGet, Route("{id}", Name = "GetProtectionById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<ProtectedModel>>> Get([FromRoute]string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest();
            id = WebUtility.UrlDecode(id);//.Replace('/', '_');
            using var scope = _logger.BeginScope(new { id });

            var grain = _clusterClient.GetGrain<IResourceProtector>(id);
            
            if (await grain.IsInitialized() == false)
            {
                return NotFound();
            }
            
            var protections = await grain.GetResourceProtections();

            return Ok(protections);
        }

        //[HttpDelete]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        //[ProducesResponseType(StatusCodes.Status404NotFound)]
        //[ProducesResponseType(StatusCodes.Status204NoContent)]
        //public async Task<IActionResult> Unprotect(
        //    [FromRoute] Uri source,
        //    [FromRoute] Uri? target)
        //{
        //    if (source == null)
        //        return BadRequest("you should provide the source uri of the protecting entity");

        //    using var scope = _logger.BeginScope(new { id, uri = slipId, type });

        //    var grain = _clusterClient.GetGrain<IResourceProtector>(id);

        //    if (await grain.IsInitialized() == false)
        //        return NotFound();

        //    await grain.UnprotectResource(new ProtectedModel
        //    {
        //        Uri = slipId,
        //    });

        //    return NoContent();
        //}


        [HttpDelete, Route("{id}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteResource(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { error = "Id parameter should never be null or empty"});

            id = WebUtility.UrlDecode(id).Replace('/', '_');

            using var scope = _logger.BeginScope(new { id });

            var grain = _clusterClient.GetGrain<IResourceProtector>(id);

            if (await grain.IsInitialized() == false)
                return NotFound();

            return await grain.DeleteResource() switch
            {
                -1 => BadRequest(new {error = "Requested entity doesn't exist"}),
                0 => Ok(new {result = "Entity soft deleted, next delete request will force deletion."}),
                var x when x > 0=> Ok(new {result = "Entity deleted", guardianSlipsRemoved = x}),
                _ => throw new Exception("we should probably never get here really")
            };
        }
    }
}
