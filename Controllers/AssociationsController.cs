using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GolfAssociationCommunity.Services;
using GolfAssociationCommunity.Models;

namespace GolfAssociationCommunity.Controllers
{
    /// <summary>
    /// API controller for managing golf associations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AssociationsController : ControllerBase
    {
        private readonly IAssociationService _associationService;
        private readonly ILogger<AssociationsController> _logger;

        public AssociationsController(
            IAssociationService associationService,
            ILogger<AssociationsController> logger)
        {
            _associationService = associationService;
            _logger = logger;
        }

        /// <summary>
        /// Get all active golf associations
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<GolfAssociation>>> GetAllAssociations()
        {
            try
            {
                var associations = await _associationService.GetAllActiveAssociationsAsync();
                return Ok(associations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving associations");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a specific golf association by ID
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<GolfAssociation>> GetAssociationById(int id)
        {
            try
            {
                var association = await _associationService.GetAssociationByIdAsync(id);
                if (association == null)
                {
                    return NotFound();
                }
                return Ok(association);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving association with ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new golf association (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GolfAssociation>> CreateAssociation(
            [FromBody] GolfAssociation association)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var createdAssociation = await _associationService.CreateAssociationAsync(association);
                return CreatedAtAction(nameof(GetAssociationById), new { id = createdAssociation.Id }, createdAssociation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating association");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update a golf association (Admin or Association Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,AssociationAdmin")]
        public async Task<ActionResult<GolfAssociation>> UpdateAssociation(
            int id,
            [FromBody] GolfAssociation association)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var updatedAssociation = await _associationService.UpdateAssociationAsync(id, association);
                if (updatedAssociation == null)
                {
                    return NotFound();
                }
                return Ok(updatedAssociation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating association with ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete a golf association (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteAssociation(int id)
        {
            try
            {
                var result = await _associationService.DeleteAssociationAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting association with ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all members of an association
        /// </summary>
        [HttpGet("{id}/members")]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetAssociationMembers(int id)
        {
            try
            {
                var members = await _associationService.GetAssociationMembersAsync(id);
                return Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members for association ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get member count for an association
        /// </summary>
        [HttpGet("{id}/member-count")]
        [AllowAnonymous]
        public async Task<ActionResult<int>> GetMemberCount(int id)
        {
            try
            {
                var count = await _associationService.GetMemberCountAsync(id);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving member count for association ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Add a member to an association
        /// </summary>
        [HttpPost("{id}/members/{userId}")]
        [Authorize(Roles = "Admin,AssociationAdmin")]
        public async Task<ActionResult> AddMemberToAssociation(int id, string userId)
        {
            try
            {
                var result = await _associationService.AddMemberToAssociationAsync(id, userId);
                if (!result)
                {
                    return NotFound("Association or user not found");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member to association");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Remove a member from an association
        /// </summary>
        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = "Admin,AssociationAdmin")]
        public async Task<ActionResult> RemoveMemberFromAssociation(int id, string userId)
        {
            try
            {
                var result = await _associationService.RemoveMemberFromAssociationAsync(id, userId);
                if (!result)
                {
                    return NotFound("Association or user not found");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member from association");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all tournaments for an association
        /// </summary>
        [HttpGet("{id}/tournaments")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Tournament>>> GetAssociationTournaments(int id)
        {
            try
            {
                var tournaments = await _associationService.GetAssociationTournamentsAsync(id);
                return Ok(tournaments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tournaments for association ID: {AssociationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
