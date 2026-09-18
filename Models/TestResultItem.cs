using System;
using System.ComponentModel;

namespace LabReportApp.Models
{
    // Represents one row in the test-results table (Category, Test Name, Result, Unit, Normal Range, Price, Discount).
    // Implements INotifyPropertyChanged so that when the autocomplete fills Unit/NormalRange/Category/Price
    // automatically, or the Lab Attendant types a per-test discount, the DataGrid cells (and the running
    // Total) update immediately on screen.
    public class TestResultItem : INotifyPropertyChanged
    {
        // 0 = not saved to the database yet (still being entered on the billing screen).
        // Non-zero = the database row Id, used to update this exact row once results are entered.
        private int _id = 0;
        private string _categoryHeading = string.Empty;
        private string _testName = string.Empty;
        private string _result = string.Empty;
        private string _unit = string.Empty;
        private string _normalRange = string.Empty;
        private decimal _price = 0;
        private decimal _discount = 0; // per-test discount, as a percentage (0-100)

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string CategoryHeading
        {
            get => _categoryHeading;
            set { _categoryHeading = value; OnPropertyChanged(nameof(CategoryHeading)); }
        }

        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(nameof(TestName)); }
        }

        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(nameof(Result)); }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(nameof(Unit)); }
        }

        public string NormalRange
        {
            get => _normalRange;
            set { _normalRange = value; OnPropertyChanged(nameof(NormalRange)); }
        }

        // Original (full) price of this single test in PKR, taken from the predefined Test Definitions list.
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
                OnPropertyChanged(nameof(NetPrice));
            }
        }

        // Per-test discount percentage (0-100), e.g. a doctor's referral discount on one specific test.
        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(NetPrice));
            }
        }

        // Price after this row's own discount is applied. Not stored directly in the database -
        // it's always recalculated from Price and Discount so it's never out of sync.
        public decimal NetPrice => Math.Round(Price - (Price * Discount / 100m), 2);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}