using BookHiveLibrary.Constants;
using Microsoft.AspNetCore.Identity;

namespace BookHiveLibrary.Seeders
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                RoleConstants.MIS,
                RoleConstants.Librarian,
                RoleConstants.Student,
                RoleConstants.Professor
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}