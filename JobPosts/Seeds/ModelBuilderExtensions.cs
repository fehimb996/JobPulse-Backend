using JobPosts.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Seeds
{
    public static class ModelBuilderExtensions
    {
        public static void Seed(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContractType>().HasData(
                new ContractType { Id = 1, Type = "Permanent" },
                new ContractType { Id = 2, Type = "Contract" }
            );

            modelBuilder.Entity<ContractTime>().HasData(
                new ContractTime { Id = 1, Time = "Full time" },
                new ContractTime { Id = 2, Time = "Part time" }
            );

            modelBuilder.Entity<Country>().HasData(
                new Country { Id = 1, CountryName = "Germany", CountryCode = "DE" },
                new Country { Id = 2, CountryName = "United Kingdom", CountryCode = "GB" },
                new Country { Id = 3, CountryName = "United States of America", CountryCode = "US" },
                new Country { Id = 4, CountryName = "Netherlands", CountryCode = "NL" },
                new Country { Id = 5, CountryName = "Belgium", CountryCode = "BE" },
                new Country { Id = 6, CountryName = "Austria", CountryCode = "AT" },
                new Country { Id = 7, CountryName = "Switzerland", CountryCode = "CH" }
            );

            modelBuilder.Entity<WorkplaceModel>().HasData(
                new WorkplaceModel { Id = 1, Workplace = "Onsite" },
                new WorkplaceModel { Id = 2, Workplace = "Remote" },
                new WorkplaceModel { Id = 3, Workplace = "Hybrid" }
            );

            modelBuilder.Entity<Language>().HasData(
                new Language { Id = 1, Name = "English" },
                new Language { Id = 2, Name = "German" },
                new Language { Id = 3, Name = "Dutch" },
                new Language { Id = 4, Name = "French" }
            );
        }
    }
}