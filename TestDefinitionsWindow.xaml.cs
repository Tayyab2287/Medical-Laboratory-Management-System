using System.Windows;
using System.Windows.Controls;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class TestDefinitionsWindow : Window
    {
        private int _editingId = 0; // 0 = adding a new test, otherwise editing an existing one

        public TestDefinitionsWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            LoadGrid();
        }

        private void LoadGrid()
        {
            dgDefinitions.ItemsSource = DatabaseHelper.GetAllTestDefinitions();
        }

        private void btnSaveDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCategory.Text) || string.IsNullOrWhiteSpace(txtTestName.Text))
            {
                MessageBox.Show("Category and Test Name are required.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrice.Text.Trim(), out decimal price) || price < 0)
            {
                MessageBox.Show("Please enter a valid Price in PKR (0 or more).", "Invalid Price",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var definition = new TestDefinition
            {
                Id = _editingId,
                CategoryHeading = txtCategory.Text.Trim(),
                TestName = txtTestName.Text.Trim(),
                Unit = txtUnit.Text.Trim(),
                NormalRange = txtNormalRange.Text.Trim(),
                Price = price
            };

            DatabaseHelper.InsertOrUpdateTestDefinition(definition);
            LoadGrid();
            ClearForm();
        }

        private void btnClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            _editingId = 0;
            txtCategory.Text = "";
            txtTestName.Text = "";
            txtUnit.Text = "";
            txtNormalRange.Text = "";
            txtPrice.Text = "";
            dgDefinitions.SelectedItem = null;
        }

        private void dgDefinitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgDefinitions.SelectedItem is TestDefinition selected)
            {
                _editingId = selected.Id;
                txtCategory.Text = selected.CategoryHeading;
                txtTestName.Text = selected.TestName;
                txtUnit.Text = selected.Unit;
                txtNormalRange.Text = selected.NormalRange;
                txtPrice.Text = selected.Price.ToString("0.##");
            }
        }

        private void btnDeleteDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (dgDefinitions.SelectedItem is TestDefinition selected)
            {
                var confirm = MessageBox.Show($"Delete test '{selected.TestName}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    DatabaseHelper.DeleteTestDefinition(selected.Id);
                    LoadGrid();
                    ClearForm();
                }
            }
            else
            {
                MessageBox.Show("Please select a test to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}