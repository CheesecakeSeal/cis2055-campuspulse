using CampusPulse.Data;
using CampusPulse.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusPulse.Services
{
    public class InvestigatorRoleClaimsTransformation : IClaimsTransformation
    {
        private readonly AppDbContext _context;

        public InvestigatorRoleClaimsTransformation(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                return principal;
            }

            if (identity.HasClaim(identity.RoleClaimType, UserRoles.Investigator))
            {
                return principal;
            }

            var email =
                principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(ClaimTypes.Name)
                ?? identity.Name;

            if (string.IsNullOrWhiteSpace(email))
            {
                return principal;
            }

            var normalizedEmail = email.Trim().ToUpperInvariant();

            var isInvestigator = await _context.InvestigatorEmails
                .AnyAsync(i => i.NormalizedEmail == normalizedEmail);

            if (isInvestigator)
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, UserRoles.Investigator));
            }

            return principal;
        }
    }
}