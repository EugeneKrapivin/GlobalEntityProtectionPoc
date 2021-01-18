using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;

using SiloB.Interfaces;

namespace SiloB.Host.Controllers
{
#nullable disable
    public class BusinessUnitContract
    {
        public string Id { get; set; }
        public string WorkspaceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static implicit operator BusinessUnitContract(BusinessUnitModel model)
            => new BusinessUnitContract
            {
                Id = model.Id,
                WorkspaceId = model.WorkspaceId,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt
            };
    }
#nullable restore

    [ApiController]
    [Route("[controller]")]
    public class BusinessUnitsController : ControllerBase
    {
        private readonly IClusterClient _cluster;
        private readonly ILogger<BusinessUnitsController> _logger;

        public BusinessUnitsController(IClusterClient cluster, ILogger<BusinessUnitsController> logger)
        {
            _cluster = cluster;
            _logger = logger;
        }

        [HttpGet, Route("{id}", Name = "GetBusinessUnit")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BusinessUnitContract))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BusinessUnitContract>> Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { message = "id could not be null of empty" });

            var businessUnitGrain = _cluster.GetGrain<IBusinessUnitGrain>(id);

            if (!await businessUnitGrain.IsInitialized())
            {
                return NotFound();
            }

            return Ok(await businessUnitGrain.GetBusinessUnit());
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(BusinessUnitContract))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] BusinessUnitContract contract)
        {
            if (!string.IsNullOrEmpty(contract.Id))
                return BadRequest(new
                {
                    message = "Creating a business unit doesn't allow passing external id, were to trying to update?"
                });
            if (string.IsNullOrEmpty(contract.WorkspaceId))
                return BadRequest(new
                {
                    message = "a new business unit must be created as part of some existing workspace"
                });
            
            // TODO check workspace exists - not important for POC

            var id = Guid.NewGuid().ToString();

            var grain = _cluster.GetGrain<IBusinessUnitGrain>(id);

            try
            {
                var businessUnit = await grain.Create(contract.WorkspaceId);

                return CreatedAtRoute("GetBusinessUnit", new { id = businessUnit.Id }, businessUnit);
            }
            catch (AccessViolationException)
            {
                return BadRequest(new
                {
                    message =
                        $"grain with generated id `{id}` already exists, fill a lottery ticket this shouldn't happen"
                });
            }
        }

        [HttpDelete, Route("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { message = "id could not be null of empty" });

            var businessUnitGrain = _cluster.GetGrain<IBusinessUnitGrain>(id);

            if (!await businessUnitGrain.IsInitialized())
            {
                return NotFound();
            }

            await businessUnitGrain.Delete();

            return NoContent();
        }
    }
}
