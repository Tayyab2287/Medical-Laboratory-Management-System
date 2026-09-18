namespace LabReportApp.Models
{
    // Represents one predefined test in the master list that the Lab Attendant maintains.
    // Used to power the autocomplete suggestions and auto-fill Unit/NormalRange/Category/Price.
    public class TestDefinition
    {
        public int Id { get; set; }
        public string CategoryHeading { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string NormalRange { get; set; } = string.Empty;

        // Price of this test in PKR, used to auto-fill the bill when this test is selected.
        public decimal Price { get; set; } = 0;
    }
}