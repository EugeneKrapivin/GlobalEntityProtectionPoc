using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;
using SiloA.Interfaces;

namespace SiloA.Host.Controllers
{
    #nullable disable
    public class WorkspaceContract
    {
        public string Id { get; set; }
        public HashSet<string> BusinessUnits { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static implicit operator WorkspaceContract(WorkspaceModel model)
            => new WorkspaceContract
            {
                Id = model.Id,
                BusinessUnits = model.BusinessUnits,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt
            };
    }
    #nullable restore
    
    [ApiController]
    [Route("[controller]")]
    public class WorkspacesController : ControllerBase
    {
        private readonly IClusterClient _cluster;
        private readonly ILogger<WorkspacesController> _logger;

        public WorkspacesController(IClusterClient cluster, ILogger<WorkspacesController> logger)
        {
            _cluster = cluster;
            _logger = logger;
        }

        [HttpGet, Route("{id}", Name = "GetWorkspace")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspaceContract))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkspaceContract>> Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new {message = "id could not be null of empty"});

            var workspaceGrain = _cluster.GetGrain<IWorkspaceGrain>(id);

            if (!await workspaceGrain.IsInitialized())
            {
                return NotFound();
            }

            return Ok(await workspaceGrain.GetWorkspace());
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(WorkspaceContract))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] WorkspaceContract contract)
        {
            if (!string.IsNullOrEmpty(contract.Id))
                return BadRequest(new
                {
                    message = "Creating a workspace doesn't allow passing external id, were to trying to update?"
                });

            var id = Guid.NewGuid().ToString();

            var grain = _cluster.GetGrain<IWorkspaceGrain>(id);
            
            try
            {
                var workspace = await grain.Create();

                if (contract.BusinessUnits?.Any() == true)
                {
                    workspace = await grain.AddBusinessUnits(contract.BusinessUnits);
                }

                return CreatedAtRoute("GetWorkspace", new { id = workspace.Id }, workspace);
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

        [HttpPut, Route("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspaceContract))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] string id,
            [FromBody] WorkspaceContract patchDoc)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { message = "id could not be null of empty" });

            var workspaceGrain = _cluster.GetGrain<IWorkspaceGrain>(id);

            if (!await workspaceGrain.IsInitialized())
            {
                return NotFound();
            }

            if (patchDoc.BusinessUnits == null || patchDoc.BusinessUnits.Any() == false)
            {
                return NoContent();
            }

            var result = await workspaceGrain.AddBusinessUnits(patchDoc.BusinessUnits);

            return Ok(result);
        }

        [HttpDelete, Route("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(DeleteRejectedResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "id could not be null of empty" });
            }

            var workspaceGrain = _cluster.GetGrain<IWorkspaceGrain>(id);

            if (!await workspaceGrain.IsInitialized())
            {
                return NotFound();
            }

            var result = await workspaceGrain.DeleteWorkspace();
            
            return result.Deleted
                ? (IActionResult) NoContent()
                : BadRequest(new DeleteRejectedResponse(id, result.EntityUri, result.Reasons.ToList()));
        }

        public class DeleteRejectedResponse
        {
            public DeleteRejectedResponse(string id, string entityResolvedUri, List<WorkspaceDeleteResult.Reason> reasons)
            {
                Id = id;
                EntityResolvedUri = entityResolvedUri;
                Reasons = reasons;
            }

            public string Id { get;  }
            public string EntityResolvedUri { get;  }
            public List<WorkspaceDeleteResult.Reason> Reasons { get; }
        }
    }
}
