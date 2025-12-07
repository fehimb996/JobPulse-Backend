namespace JobPosts.DTOs.JobPosts
{
    public class FilterOptionsDTO
    {
        public List<string> ContractTypes { get; set; } = new();
        public List<string> ContractTimes { get; set; } = new();
        public List<string> WorkLocations { get; set; } = new();
        public List<string>? Companies { get; set; } = new();
        public List<string>? Locations { get; set; } = new();
        public List<string> Skills { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public List<string> Countries { get; set; } = new();

        public string? Message { get; set; }
        public bool HasData => ContractTypes.Any() || ContractTimes.Any() || WorkLocations.Any() ||
                              Companies.Any() || Locations.Any() || Skills.Any() || Languages.Any();
    }
}
