namespace ADHealthChecker.Models
{
    public class HealthResult
    {
        public string CheckName { get; set; } = "";
        public string Status { get; set; } = "";
        public int Score { get; set; }
        public string Recommendation { get; set; } = "";
    }
}