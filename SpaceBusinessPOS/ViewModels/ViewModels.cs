using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceBusinessPOS.Models;
using SpaceBusinessPOS.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SpaceBusinessPOS.ViewModels
{
    // ─── App State ───────────────────────────────────────────────────────────────

    public static class AppState
    {
        public static User? CurrentUser { get; set; }
        public static bool IsAdmin => CurrentUser?.Role == "admin";
        public static bool IsManager => CurrentUser?.Role == "admin" || CurrentUser?.Role == "manager";
    }

    // ─── Login ───────────────────────────────────────────────────────────────────

    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty] private string _pin = "";
        [ObservableProperty] private string _errorMessage = "";

        public event Action? LoginSuccess;

        [RelayCommand]
        public void AppendDigit(string digit)
        {
            if (Pin.Length < 6)
                Pin += digit;
        }

        [RelayCommand]
        public void Backspace()
        {
            if (Pin.Length > 0)
                Pin = Pin[..^1];
        }

        [RelayCommand]
        public void Clear() => Pin = "";

        [RelayCommand]
        public void Login()
        {
            if (string.IsNullOrWhiteSpace(Pin)) { ErrorMessage = "Enter your PIN"; return; }
            var user = UserService.Login(Pin);
            if (user == null) { ErrorMessage = "Invalid PIN. Try again."; Pin = ""; return; }
            AppState.CurrentUser = user;
            ErrorMessage = "";
            LoginSuccess?.Invoke();
        }
    }

    // ─── Main / Navigation ───────────────────────────────────────────────────────

    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty] private string _currentPage = "pos";
        [ObservableProperty] private string _currentUserName = AppState.CurrentUser?.Name ?? "";
        [ObservableProperty] private string _currentUserRole = AppState.CurrentUser?.Role ?? "";

        public event Action? LogoutRequested;

        [RelayCommand] public void Navigate(string page) => CurrentPage = page;

        [RelayCommand]
        public void Logout()
        {
            AppState.CurrentUser = null;
            LogoutRequested?.Invoke();
        }
    }

    // ─── POS ─────────────────────────────────────────────────────────────────────

    public partial class POSViewModel : ObservableObject
    {
        [ObservableProperty] private string _searchQuery = "";
        [ObservableProperty] private ObservableCollection<Product> _searchResults = new();
        [ObservableProperty] private ObservableCollection<CartItem> _cart = new();
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private string _paymentMethod = "cash";
        [ObservableProperty] private decimal _cashGiven = 0;
        [ObservableProperty] private decimal _discountAmount = 0;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _checkoutSuccess = false;
        [ObservableProperty] private string _lastReceiptText = "";
        [ObservableProperty] private string _barcodeInput = "";

        private readonly decimal _taxRate;
        private readonly string _currencySymbol;

        public decimal Subtotal => Cart.Sum(i => i.Total);
        public decimal TaxAmount => Math.Round(Subtotal * _taxRate / 100, 2);
        public decimal Total => Subtotal + TaxAmount - DiscountAmount;
        public decimal Change => Math.Max(0, CashGiven - Total);
        public string CurrencySymbol => _currencySymbol;
        public bool CanCheckout => Cart.Count > 0 && Total > 0;

        public POSViewModel()
        {
            _taxRate = decimal.TryParse(SettingsService.Get("tax_rate", "10"), out var r) ? r : 10;
            _currencySymbol = SettingsService.Get("currency_symbol", "$");
            LoadProducts();
            Customers = new ObservableCollection<Customer>(CustomerService.GetAll());
        }

        private void LoadProducts() =>
            SearchResults = new ObservableCollection<Product>(
                string.IsNullOrWhiteSpace(SearchQuery)
                    ? ProductService.GetAll().Take(30).ToList()
                    : ProductService.Search(SearchQuery));

        partial void OnSearchQueryChanged(string value) => LoadProducts();

        [RelayCommand]
        public void ScanBarcode()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
            var p = ProductService.GetByBarcode(BarcodeInput);
            if (p != null) AddToCart(p);
            else StatusMessage = $"Product not found: {BarcodeInput}";
            BarcodeInput = "";
        }

        [RelayCommand]
        public void AddToCart(Product product)
        {
            var existing = Cart.FirstOrDefault(c => c.ProductId == product.Id);
            if (existing != null)
                existing.Quantity++;
            else
                Cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    UnitPrice = product.Price
                });
            RefreshTotals();
        }

        [RelayCommand]
        public void RemoveFromCart(CartItem item)
        {
            Cart.Remove(item);
            RefreshTotals();
        }

        [RelayCommand]
        public void IncrementQty(CartItem item) { item.Quantity++; RefreshTotals(); }

        [RelayCommand]
        public void DecrementQty(CartItem item)
        {
            if (item.Quantity > 1) { item.Quantity--; RefreshTotals(); }
            else RemoveFromCart(item);
        }

        [RelayCommand]
        public void ClearCart() { Cart.Clear(); RefreshTotals(); DiscountAmount = 0; SelectedCustomer = null; }

        [RelayCommand]
        public void Checkout()
        {
            if (!CanCheckout) { StatusMessage = "Cart is empty"; return; }
            try
            {
                var tx = new Transaction
                {
                    CustomerId = SelectedCustomer?.Id,
                    CustomerName = SelectedCustomer?.Name,
                    Subtotal = Subtotal,
                    TaxRate = _taxRate,
                    TaxAmount = TaxAmount,
                    DiscountAmount = DiscountAmount,
                    Total = Total,
                    PaymentMethod = PaymentMethod,
                    CashGiven = PaymentMethod == "cash" ? CashGiven : Total,
                    ChangeGiven = Change,
                    Status = "completed",
                    CashierId = AppState.CurrentUser?.Id ?? "",
                    CashierName = AppState.CurrentUser?.Name ?? "",
                    LocationId = "main",
                    Items = Cart.Select(c => new TransactionItem
                    {
                        ProductId = c.ProductId,
                        ProductName = c.ProductName,
                        Sku = c.Sku,
                        Quantity = c.Quantity,
                        UnitPrice = c.UnitPrice,
                        DiscountAmount = c.DiscountAmount,
                        Total = c.Total
                    }).ToList()
                };

                var txNumber = TransactionService.Checkout(tx);
                var tmpl = ReceiptService.Get();
                tx.TransactionNumber = txNumber;
                LastReceiptText = ReceiptService.FormatReceipt(tx, tmpl);

                if (SettingsService.Get("auto_print_receipt") == "true")
                {
                    var printer = SettingsService.Get("printer_name");
                    PrintService.PrintReceipt(LastReceiptText, string.IsNullOrEmpty(printer) ? null : printer);
                }

                StatusMessage = $"Sale complete! {txNumber}  Change: {_currencySymbol}{Change:F2}";
                CheckoutSuccess = true;
                ClearCart();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Checkout failed: {ex.Message}";
            }
        }

        [RelayCommand]
        public void PrintLastReceipt()
        {
            if (string.IsNullOrEmpty(LastReceiptText)) return;
            var printer = SettingsService.Get("printer_name");
            PrintService.PrintReceipt(LastReceiptText, string.IsNullOrEmpty(printer) ? null : printer);
        }

        public void RefreshTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(Change));
            OnPropertyChanged(nameof(CanCheckout));
        }

        partial void OnDiscountAmountChanged(decimal value) => RefreshTotals();
        partial void OnCashGivenChanged(decimal value) => OnPropertyChanged(nameof(Change));
    }

    // ─── Products ────────────────────────────────────────────────────────────────

    public partial class ProductsViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private string _searchQuery = "";
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private bool _isEditing = false;

        // Edit fields
        [ObservableProperty] private string _editId = "";
        [ObservableProperty] private string _editName = "";
        [ObservableProperty] private string _editSku = "";
        [ObservableProperty] private string _editBarcode = "";
        [ObservableProperty] private decimal _editPrice = 0;
        [ObservableProperty] private decimal _editCost = 0;
        [ObservableProperty] private decimal _editStock = 0;
        [ObservableProperty] private decimal _editMinStock = 0;
        [ObservableProperty] private string? _editCategoryId;
        [ObservableProperty] private bool _editIsActive = true;
        [ObservableProperty] private string _statusMessage = "";

        public ProductsViewModel() => Load();

        private void Load()
        {
            Products = new ObservableCollection<Product>(
                string.IsNullOrWhiteSpace(SearchQuery)
                    ? ProductService.GetAll(includeInactive: true)
                    : ProductService.Search(SearchQuery));
            Categories = new ObservableCollection<Category>(CategoryService.GetAll());
        }

        partial void OnSearchQueryChanged(string value) => Load();

        [RelayCommand]
        public void NewProduct()
        {
            EditId = Guid.NewGuid().ToString();
            EditName = ""; EditSku = ""; EditBarcode = ""; EditPrice = 0; EditCost = 0;
            EditStock = 0; EditMinStock = 0; EditCategoryId = null; EditIsActive = true;
            IsEditing = true;
        }

        [RelayCommand]
        public void EditProduct(Product p)
        {
            EditId = p.Id; EditName = p.Name; EditSku = p.Sku; EditBarcode = p.Barcode ?? "";
            EditPrice = p.Price; EditCost = p.Cost; EditStock = p.StockQuantity;
            EditMinStock = p.MinStock; EditCategoryId = p.CategoryId; EditIsActive = p.IsActive;
            IsEditing = true;
        }

        [RelayCommand]
        public void SaveProduct()
        {
            if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditSku))
            { StatusMessage = "Name and SKU are required"; return; }
            ProductService.Save(new Product
            {
                Id = EditId, Name = EditName, Sku = EditSku,
                Barcode = string.IsNullOrWhiteSpace(EditBarcode) ? null : EditBarcode,
                Price = EditPrice, Cost = EditCost, StockQuantity = EditStock,
                MinStock = EditMinStock, CategoryId = EditCategoryId, IsActive = EditIsActive,
                CreatedAt = DateTime.UtcNow
            });
            StatusMessage = "Product saved.";
            IsEditing = false;
            Load();
        }

        [RelayCommand]
        public void DeleteProduct(Product p)
        {
            if (MessageBox.Show($"Delete '{p.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ProductService.Delete(p.Id);
                Load();
            }
        }

        [RelayCommand] public void CancelEdit() => IsEditing = false;
    }

    // ─── Categories ──────────────────────────────────────────────────────────────

    public partial class CategoriesViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _editId = "";
        [ObservableProperty] private string _editName = "";
        [ObservableProperty] private string _editColor = "#3b82f6";
        [ObservableProperty] private string _statusMessage = "";

        public CategoriesViewModel() => Load();

        private void Load() => Categories = new ObservableCollection<Category>(CategoryService.GetAll());

        [RelayCommand]
        public void NewCategory()
        {
            EditId = Guid.NewGuid().ToString(); EditName = ""; EditColor = "#3b82f6"; IsEditing = true;
        }

        [RelayCommand]
        public void EditCategory(Category c)
        {
            EditId = c.Id; EditName = c.Name; EditColor = c.Color; IsEditing = true;
        }

        [RelayCommand]
        public void SaveCategory()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Name is required"; return; }
            CategoryService.Save(new Category { Id = EditId, Name = EditName, Color = EditColor, CreatedAt = DateTime.UtcNow });
            StatusMessage = "Saved."; IsEditing = false; Load();
        }

        [RelayCommand]
        public void DeleteCategory(Category c)
        {
            if (MessageBox.Show($"Delete '{c.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { CategoryService.Delete(c.Id); Load(); }
        }

        [RelayCommand] public void CancelEdit() => IsEditing = false;
    }

    // ─── Customers ───────────────────────────────────────────────────────────────

    public partial class CustomersViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private string _searchQuery = "";
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _editId = "";
        [ObservableProperty] private string _editName = "";
        [ObservableProperty] private string _editEmail = "";
        [ObservableProperty] private string _editPhone = "";
        [ObservableProperty] private string _editAddress = "";
        [ObservableProperty] private string _editNotes = "";
        [ObservableProperty] private int _editLoyaltyPoints = 0;
        [ObservableProperty] private string _statusMessage = "";

        public CustomersViewModel() => Load();

        private void Load() => Customers = new ObservableCollection<Customer>(
            string.IsNullOrWhiteSpace(SearchQuery) ? CustomerService.GetAll() : CustomerService.Search(SearchQuery));

        partial void OnSearchQueryChanged(string value) => Load();

        [RelayCommand]
        public void NewCustomer()
        {
            EditId = Guid.NewGuid().ToString(); EditName = ""; EditEmail = ""; EditPhone = "";
            EditAddress = ""; EditNotes = ""; EditLoyaltyPoints = 0; IsEditing = true;
        }

        [RelayCommand]
        public void EditCustomer(Customer c)
        {
            EditId = c.Id; EditName = c.Name; EditEmail = c.Email ?? ""; EditPhone = c.Phone ?? "";
            EditAddress = c.Address ?? ""; EditNotes = c.Notes ?? ""; EditLoyaltyPoints = c.LoyaltyPoints; IsEditing = true;
        }

        [RelayCommand]
        public void SaveCustomer()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Name is required"; return; }
            CustomerService.Save(new Customer
            {
                Id = EditId, Name = EditName,
                Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail,
                Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone,
                Address = string.IsNullOrWhiteSpace(EditAddress) ? null : EditAddress,
                Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes,
                LoyaltyPoints = EditLoyaltyPoints, CreatedAt = DateTime.UtcNow
            });
            StatusMessage = "Saved."; IsEditing = false; Load();
        }

        [RelayCommand]
        public void DeleteCustomer(Customer c)
        {
            if (MessageBox.Show($"Delete '{c.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { CustomerService.Delete(c.Id); Load(); }
        }

        [RelayCommand] public void CancelEdit() => IsEditing = false;
    }

    // ─── Users ───────────────────────────────────────────────────────────────────

    public partial class UsersViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<User> _users = new();
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _editId = "";
        [ObservableProperty] private string _editName = "";
        [ObservableProperty] private string _editEmail = "";
        [ObservableProperty] private string _editPin = "";
        [ObservableProperty] private string _editRole = "cashier";
        [ObservableProperty] private bool _editIsActive = true;
        [ObservableProperty] private string _statusMessage = "";

        public List<string> Roles { get; } = new() { "admin", "manager", "cashier" };

        public UsersViewModel() => Load();

        private void Load() => Users = new ObservableCollection<User>(UserService.GetAll());

        [RelayCommand]
        public void NewUser()
        {
            EditId = Guid.NewGuid().ToString(); EditName = ""; EditEmail = "";
            EditPin = ""; EditRole = "cashier"; EditIsActive = true; IsEditing = true;
        }

        [RelayCommand]
        public void EditUser(User u)
        {
            EditId = u.Id; EditName = u.Name; EditEmail = u.Email;
            EditPin = u.Pin; EditRole = u.Role; EditIsActive = u.IsActive; IsEditing = true;
        }

        [RelayCommand]
        public void SaveUser()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Name is required"; return; }
            if (string.IsNullOrWhiteSpace(EditPin) || EditPin.Length < 4) { StatusMessage = "PIN must be at least 4 digits"; return; }
            UserService.Save(new User
            {
                Id = EditId, Name = EditName, Email = EditEmail, Pin = EditPin,
                Role = EditRole, IsActive = EditIsActive, CreatedAt = DateTime.UtcNow
            });
            StatusMessage = "Saved."; IsEditing = false; Load();
        }

        [RelayCommand]
        public void DeleteUser(User u)
        {
            if (u.Id == AppState.CurrentUser?.Id) { StatusMessage = "Cannot delete yourself"; return; }
            if (MessageBox.Show($"Deactivate '{u.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { UserService.Delete(u.Id); Load(); }
        }

        [RelayCommand] public void CancelEdit() => IsEditing = false;
    }

    // ─── Transactions ────────────────────────────────────────────────────────────

    public partial class TransactionsViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Transaction> _transactions = new();
        [ObservableProperty] private Transaction? _selectedTransaction;
        [ObservableProperty] private ObservableCollection<TransactionItem> _selectedItems = new();
        [ObservableProperty] private DateTime _dateFrom = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _dateTo = DateTime.Today;
        [ObservableProperty] private string _statusMessage = "";

        public TransactionsViewModel() => Load();

        [RelayCommand]
        public void Load()
        {
            Transactions = new ObservableCollection<Transaction>(
                TransactionService.GetRecent(200, DateFrom.ToString("o"), DateTo.AddDays(1).ToString("o")));
        }

        partial void OnSelectedTransactionChanged(Transaction? value)
        {
            if (value == null) { SelectedItems.Clear(); return; }
            SelectedItems = new ObservableCollection<TransactionItem>(TransactionService.GetItems(value.Id));
        }

        [RelayCommand]
        public void Refund(Transaction tx)
        {
            if (tx.Status == "refunded") { StatusMessage = "Already refunded"; return; }
            if (MessageBox.Show($"Refund {tx.TransactionNumber}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                TransactionService.Refund(tx.Id);
                Load();
                StatusMessage = "Refunded.";
            }
        }

        [RelayCommand]
        public void PrintReceipt(Transaction tx)
        {
            tx.Items = TransactionService.GetItems(tx.Id);
            var tmpl = ReceiptService.Get();
            var text = ReceiptService.FormatReceipt(tx, tmpl);
            PrintService.PrintReceipt(text, SettingsService.Get("printer_name"));
        }
    }

    // ─── Reports ─────────────────────────────────────────────────────────────────

    public partial class ReportsViewModel : ObservableObject
    {
        [ObservableProperty] private DateTime _dateFrom = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _dateTo = DateTime.Today;
        [ObservableProperty] private ReportSummary _summary = new();
        [ObservableProperty] private string _currencySymbol = SettingsService.Get("currency_symbol", "$");

        public ReportsViewModel() => Load();

        [RelayCommand]
        public void Load() => Summary = ReportService.GetSummary(DateFrom, DateTo);

        [RelayCommand] public void SetToday() { DateFrom = DateTime.Today; DateTo = DateTime.Today; Load(); }
        [RelayCommand] public void SetThisWeek() { DateFrom = DateTime.Today.AddDays(-7); DateTo = DateTime.Today; Load(); }
        [RelayCommand] public void SetThisMonth() { DateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); DateTo = DateTime.Today; Load(); }
        [RelayCommand] public void SetThisYear() { DateFrom = new DateTime(DateTime.Today.Year, 1, 1); DateTo = DateTime.Today; Load(); }
    }

    // ─── Accounting ──────────────────────────────────────────────────────────────

    public partial class AccountingViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Expense> _expenses = new();
        [ObservableProperty] private DateTime _dateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        [ObservableProperty] private DateTime _dateTo = DateTime.Today;
        [ObservableProperty] private decimal _income = 0;
        [ObservableProperty] private decimal _totalExpenses = 0;
        [ObservableProperty] private decimal _profit = 0;
        [ObservableProperty] private bool _isEditing = false;
        [ObservableProperty] private string _editId = "";
        [ObservableProperty] private string _editDescription = "";
        [ObservableProperty] private string _editCategory = "General";
        [ObservableProperty] private decimal _editAmount = 0;
        [ObservableProperty] private string _editPaymentMethod = "cash";
        [ObservableProperty] private string _editNotes = "";
        [ObservableProperty] private DateTime _editDate = DateTime.Today;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private string _currencySymbol = SettingsService.Get("currency_symbol", "$");

        public List<string> ExpenseCategories { get; } = new()
        {
            "General", "Rent", "Utilities", "Salaries", "Supplies", "Marketing", "Maintenance", "Transport", "Other"
        };
        public List<string> PaymentMethods { get; } = new() { "cash", "card", "bank transfer", "other" };

        public AccountingViewModel() => Load();

        [RelayCommand]
        public void Load()
        {
            Expenses = new ObservableCollection<Expense>(AccountingService.GetExpenses(DateFrom, DateTo));
            var (inc, exp, profit) = AccountingService.GetPLSummary(DateFrom, DateTo);
            Income = inc; TotalExpenses = exp; Profit = profit;
        }

        [RelayCommand]
        public void NewExpense()
        {
            EditId = Guid.NewGuid().ToString(); EditDescription = ""; EditCategory = "General";
            EditAmount = 0; EditPaymentMethod = "cash"; EditNotes = ""; EditDate = DateTime.Today; IsEditing = true;
        }

        [RelayCommand]
        public void EditExpense(Expense e)
        {
            EditId = e.Id; EditDescription = e.Description; EditCategory = e.Category;
            EditAmount = e.Amount; EditPaymentMethod = e.PaymentMethod;
            EditNotes = e.Notes ?? ""; EditDate = e.ExpenseDate; IsEditing = true;
        }

        [RelayCommand]
        public void SaveExpense()
        {
            if (string.IsNullOrWhiteSpace(EditDescription)) { StatusMessage = "Description required"; return; }
            if (EditAmount <= 0) { StatusMessage = "Amount must be > 0"; return; }
            AccountingService.SaveExpense(new Expense
            {
                Id = EditId, Description = EditDescription, Category = EditCategory,
                Amount = EditAmount, PaymentMethod = EditPaymentMethod,
                Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes,
                RecordedBy = AppState.CurrentUser?.Name ?? "", ExpenseDate = EditDate, CreatedAt = DateTime.UtcNow
            });
            StatusMessage = "Saved."; IsEditing = false; Load();
        }

        [RelayCommand]
        public void DeleteExpense(Expense e)
        {
            if (MessageBox.Show($"Delete expense '{e.Description}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { AccountingService.DeleteExpense(e.Id); Load(); }
        }

        [RelayCommand] public void CancelEdit() => IsEditing = false;
        [RelayCommand] public void SetThisMonth() { DateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); DateTo = DateTime.Today; Load(); }
        [RelayCommand] public void SetLastMonth() { var d = DateTime.Today.AddMonths(-1); DateFrom = new DateTime(d.Year, d.Month, 1); DateTo = new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)); Load(); }
        [RelayCommand] public void SetThisYear() { DateFrom = new DateTime(DateTime.Today.Year, 1, 1); DateTo = DateTime.Today; Load(); }
    }

    // ─── Settings ────────────────────────────────────────────────────────────────

    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _taxRate = "";
        [ObservableProperty] private string _currency = "";
        [ObservableProperty] private string _currencySymbol = "";
        [ObservableProperty] private string _locationName = "";
        [ObservableProperty] private string _supabaseUrl = "";
        [ObservableProperty] private string _supabaseKey = "";
        [ObservableProperty] private bool _autoPrintReceipt = false;
        [ObservableProperty] private string _selectedPrinter = "";
        [ObservableProperty] private ObservableCollection<string> _printers = new();
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _lowStockAlert = true;

        public SettingsViewModel() => Load();

        private void Load()
        {
            var s = SettingsService.GetAll();
            TaxRate = s.GetValueOrDefault("tax_rate", "10");
            Currency = s.GetValueOrDefault("currency", "USD");
            CurrencySymbol = s.GetValueOrDefault("currency_symbol", "$");
            LocationName = s.GetValueOrDefault("location_name", "Main Store");
            SupabaseUrl = s.GetValueOrDefault("supabase_url", "");
            SupabaseKey = s.GetValueOrDefault("supabase_anon_key", "");
            AutoPrintReceipt = s.GetValueOrDefault("auto_print_receipt", "false") == "true";
            SelectedPrinter = s.GetValueOrDefault("printer_name", "");
            LowStockAlert = s.GetValueOrDefault("low_stock_alert", "true") == "true";
            Printers = new ObservableCollection<string>(PrintService.GetPrinters());
        }

        [RelayCommand]
        public void Save()
        {
            SettingsService.Set("tax_rate", TaxRate);
            SettingsService.Set("currency", Currency);
            SettingsService.Set("currency_symbol", CurrencySymbol);
            SettingsService.Set("location_name", LocationName);
            SettingsService.Set("supabase_url", SupabaseUrl);
            SettingsService.Set("supabase_anon_key", SupabaseKey);
            SettingsService.Set("auto_print_receipt", AutoPrintReceipt.ToString().ToLower());
            SettingsService.Set("printer_name", SelectedPrinter);
            SettingsService.Set("low_stock_alert", LowStockAlert.ToString().ToLower());
            StatusMessage = "Settings saved successfully.";
        }

        [RelayCommand]
        public void RefreshPrinters() => Printers = new ObservableCollection<string>(PrintService.GetPrinters());

        [RelayCommand]
        public void TestPrint()
        {
            try
            {
                PrintService.PrintReceipt("---- PRINTER TEST ----\nSpace-Business POS\nPrint test successful!\n----------------------", string.IsNullOrEmpty(SelectedPrinter) ? null : SelectedPrinter);
                StatusMessage = "Test print sent.";
            }
            catch (Exception ex) { StatusMessage = $"Print error: {ex.Message}"; }
        }
    }

    // ─── Receipt Designer ────────────────────────────────────────────────────────

    public partial class ReceiptDesignerViewModel : ObservableObject
    {
        [ObservableProperty] private string _header = "";
        [ObservableProperty] private string _footer = "";
        [ObservableProperty] private string _businessName = "";
        [ObservableProperty] private string _businessAddress = "";
        [ObservableProperty] private string _businessPhone = "";
        [ObservableProperty] private bool _showLogo = true;
        [ObservableProperty] private bool _showTaxBreakdown = true;
        [ObservableProperty] private bool _showCustomerInfo = true;
        [ObservableProperty] private string _fontSize = "Normal";
        [ObservableProperty] private string _previewText = "";
        [ObservableProperty] private string _statusMessage = "";

        public List<string> FontSizes { get; } = new() { "Small", "Normal", "Large" };

        public ReceiptDesignerViewModel() => Load();

        private void Load()
        {
            var t = ReceiptService.Get();
            Header = t.Header; Footer = t.Footer; BusinessName = t.BusinessName;
            BusinessAddress = t.BusinessAddress; BusinessPhone = t.BusinessPhone;
            ShowLogo = t.ShowLogo; ShowTaxBreakdown = t.ShowTaxBreakdown;
            ShowCustomerInfo = t.ShowCustomerInfo; FontSize = t.FontSize;
            UpdatePreview();
        }

        [RelayCommand]
        public void Save()
        {
            ReceiptService.Save(new ReceiptTemplate
            {
                Header = Header, Footer = Footer, BusinessName = BusinessName,
                BusinessAddress = BusinessAddress, BusinessPhone = BusinessPhone,
                ShowLogo = ShowLogo, ShowTaxBreakdown = ShowTaxBreakdown,
                ShowCustomerInfo = ShowCustomerInfo, FontSize = FontSize
            });
            StatusMessage = "Receipt template saved.";
            UpdatePreview();
        }

        [RelayCommand]
        public void UpdatePreview()
        {
            var sampleTx = new Transaction
            {
                TransactionNumber = "TXN-20260101-0001",
                CreatedAt = DateTime.Now,
                CashierName = "Admin",
                CustomerName = ShowCustomerInfo ? "John Doe" : null,
                Subtotal = 50.00m,
                TaxRate = 10,
                TaxAmount = 5.00m,
                DiscountAmount = 0,
                Total = 55.00m,
                CashGiven = 60.00m,
                ChangeGiven = 5.00m,
                Items = new List<TransactionItem>
                {
                    new() { ProductName = "Sample Item 1", Quantity = 2, UnitPrice = 15.00m, Total = 30.00m, Sku = "SKU001" },
                    new() { ProductName = "Sample Item 2", Quantity = 1, UnitPrice = 20.00m, Total = 20.00m, Sku = "SKU002" }
                }
            };
            var tmpl = new ReceiptTemplate
            {
                Header = Header, Footer = Footer, BusinessName = BusinessName,
                BusinessAddress = BusinessAddress, BusinessPhone = BusinessPhone,
                ShowTaxBreakdown = ShowTaxBreakdown, ShowCustomerInfo = ShowCustomerInfo
            };
            PreviewText = ReceiptService.FormatReceipt(sampleTx, tmpl);
        }
    }
}
