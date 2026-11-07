using Microsoft.AspNetCore.Identity;

namespace PayrollNZ.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
}
