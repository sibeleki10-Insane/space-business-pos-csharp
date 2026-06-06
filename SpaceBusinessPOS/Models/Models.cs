using System;
using System.Collections.Generic;

namespace SpaceBusinessPOS.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "cashier"; // admin, manager, cashier
        public string Pin { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Category
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#3b82f6";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }
        public bool Synced { get; set; } = false;
    }

    public class Product
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Sku { get; set; } = "";
        public string? Barcode { get; set; }
        public decimal Price { get; set; } = 0;
        public decimal Cost { get; set; } = 0;
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal StockQuantity { get; set; } = 0;
        public decimal MinStock { get; set; } = 0;
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }
        public bool Synced { get; set; } = false;
    }

    public class Customer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int LoyaltyPoints { get; set; } = 0;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeletedAt { get; set; }
        public bool Synced { get; set; } = false;
    }

    public class Transaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TransactionNumber { get; set; } = "";
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public decimal Subtotal { get; set; } = 0;
        public decimal TaxRate { get; set; } = 0;
        public decimal TaxAmount { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public decimal Total { get; set; } = 0;
        public string PaymentMethod { get; set; } = "cash";
        public decimal CashGiven { get; set; } = 0;
        public decimal ChangeGiven { get; set; } = 0;
        public string Status { get; set; } = "completed";
        public string? Notes { get; set; }
        public string CashierId { get; set; } = "";
        public string CashierName { get; set; } = "";
        public string LocationId { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool Synced { get; set; } = false;
        public List<TransactionItem> Items { get; set; } = new();
    }

    public class TransactionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TransactionId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Sku { get; set; } = "";
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public decimal Total { get; set; } = 0;
    }

    public class CartItem
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Sku { get; set; } = "";
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public decimal Total => (UnitPrice * Quantity) - DiscountAmount;
    }

    public class Expense
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = "";
        public string Category { get; set; } = "General";
        public decimal Amount { get; set; } = 0;
        public string PaymentMethod { get; set; } = "cash";
        public string? Notes { get; set; }
        public string RecordedBy { get; set; } = "";
        public DateTime ExpenseDate { get; set; } = DateTime.Today;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReceiptTemplate
    {
        public string Id { get; set; } = "default";
        public string Header { get; set; } = "Thank you for shopping with us!";
        public string Footer { get; set; } = "Please come again.";
        public string BusinessName { get; set; } = "Space-Business POS";
        public string BusinessAddress { get; set; } = "";
        public string BusinessPhone { get; set; } = "";
        public bool ShowLogo { get; set; } = true;
        public bool ShowTaxBreakdown { get; set; } = true;
        public bool ShowCustomerInfo { get; set; } = true;
        public string FontSize { get; set; } = "Normal";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReportSummary
    {
        public decimal TotalSales { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageTransaction { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalSales - TotalExpenses;
        public List<ProductSalesSummary> TopProducts { get; set; } = new();
        public List<DailySales> DailySalesData { get; set; } = new();
    }

    public class ProductSalesSummary
    {
        public string ProductName { get; set; } = "";
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DailySales
    {
        public string Date { get; set; } = "";
        public decimal Sales { get; set; }
        public int Transactions { get; set; }
    }
}
