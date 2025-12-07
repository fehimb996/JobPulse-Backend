namespace JobPosts.DTOs.JobPosts
{
    public class MigrationVerificationResult
    {
        public int TotalRecords { get; set; }
        public int UnmigratedRecords { get; set; }
        public int EmptyTitleRecords { get; set; }
        public int MigratedRecords { get; set; }
        public int InconsistentRecords { get; set; }
        public double SuccessRate { get; set; }
        public override string ToString()
        {
            return $"Total: {TotalRecords}, Migrated: {MigratedRecords}, Unmigrated: {UnmigratedRecords}, " +
                   $"Empty Titles: {EmptyTitleRecords}, Inconsistent: {InconsistentRecords}, Success Rate: {SuccessRate:F2}%";
        }
    }
}
