using Microsoft.Data.Sqlite;
using SpaceBusinessPOS.Data;
using SpaceBusinessPOS.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceBusinessPOS.Services
{
    // ─── Settings ────────────────────────────────────────────────────────────────

    public static class SettingsService
    {
        public static string Get(string key, string fallback = "")
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key=@k";
            cmd.Parameters.AddWithValue("@k", key);
            return cmd.ExecuteScalar()?.ToString() ?? fallback;
        }

        public static void Set(string key, string value)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO app_settings (key, value, updated_at) VALUES (@k, @v, @now)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static Dictionary<string, string> GetAll()
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM app_settings";
            var result = new Dictionary<string, string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result[reader.GetString(0)] = reader.GetString(1);
            return result;
        }
    }

    // ─── Categories ──────────────────────────────────────────────────────────────

    public static class CategoryService
    {
        public static List<Category> GetAll()
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, color, created_at, updated_at FROM categories WHERE deleted_at IS NULL ORDER BY name";
            var list = new List<Category>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Category
                {
                    Id = r.GetString(0),
                    Name = r.GetString(1),
                    Color = r.GetString(2),
                    CreatedAt = DateTime.Parse(r.GetString(3)),
                    UpdatedAt = DateTime.Parse(r.GetString(4))
                });
            return list;
        }

        public static void Save(Category cat)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO categories (id, name, color, created_at, updated_at, synced)
                VALUES (@id, @name, @color, @created, @updated, 0)";
            cmd.Parameters.AddWithValue("@id", cat.Id);
            cmd.Parameters.AddWithValue("@name", cat.Name);
            cmd.Parameters.AddWithValue("@color", cat.Color);
            cmd.Parameters.AddWithValue("@created", cat.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void Delete(string id)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE categories SET deleted_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ─── Products ────────────────────────────────────────────────────────────────

    public static class ProductService
    {
        public static List<Product> GetAll(bool includeInactive = false)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.id, p.name, p.sku, p.barcode, p.price, p.cost,
                p.category_id, c.name, p.stock_quantity, p.min_stock, p.image_url,
                p.is_active, p.created_at, p.updated_at
                FROM products p LEFT JOIN categories c ON p.category_id = c.id
                WHERE p.deleted_at IS NULL" + (includeInactive ? "" : " AND p.is_active=1") +
                " ORDER BY p.name";
            var list = new List<Product>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(Map(r));
            return list;
        }

        public static List<Product> Search(string query)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            var q = "%" + query + "%";
            cmd.CommandText = @"SELECT p.id, p.name, p.sku, p.barcode, p.price, p.cost,
                p.category_id, c.name, p.stock_quantity, p.min_stock, p.image_url,
                p.is_active, p.created_at, p.updated_at
                FROM products p LEFT JOIN categories c ON p.category_id = c.id
                WHERE p.deleted_at IS NULL AND p.is_active=1
                AND (p.name LIKE @q OR p.sku LIKE @q OR p.barcode LIKE @q)
                ORDER BY p.name";
            cmd.Parameters.AddWithValue("@q", q);
            var list = new List<Product>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(Map(r));
            return list;
        }

        public static Product? GetByBarcode(string barcode)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.id, p.name, p.sku, p.barcode, p.price, p.cost,
                p.category_id, c.name, p.stock_quantity, p.min_stock, p.image_url,
                p.is_active, p.created_at, p.updated_at
                FROM products p LEFT JOIN categories c ON p.category_id = c.id
                WHERE p.barcode=@b AND p.deleted_at IS NULL AND p.is_active=1 LIMIT 1";
            cmd.Parameters.AddWithValue("@b", barcode);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public static void Save(Product p)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO products
                (id, name, sku, barcode, price, cost, category_id, stock_quantity, min_stock, image_url, is_active, created_at, updated_at, synced)
                VALUES (@id,@name,@sku,@bc,@price,@cost,@catid,@qty,@min,@img,@active,@created,@updated,0)";
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.Parameters.AddWithValue("@name", p.Name);
            cmd.Parameters.AddWithValue("@sku", p.Sku);
            cmd.Parameters.AddWithValue("@bc", (object?)p.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@price", p.Price);
            cmd.Parameters.AddWithValue("@cost", p.Cost);
            cmd.Parameters.AddWithValue("@catid", (object?)p.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@qty", p.StockQuantity);
            cmd.Parameters.AddWithValue("@min", p.MinStock);
            cmd.Parameters.AddWithValue("@img", (object?)p.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@active", p.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@created", p.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void Delete(string id)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE products SET deleted_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static void AdjustStock(string productId, decimal delta)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @delta, updated_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@delta", delta);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", productId);
            cmd.ExecuteNonQuery();
        }

        private static Product Map(SqliteDataReader r) => new()
        {
            Id = r.GetString(0),
            Name = r.GetString(1),
            Sku = r.GetString(2),
            Barcode = r.IsDBNull(3) ? null : r.GetString(3),
            Price = r.GetDecimal(4),
            Cost = r.GetDecimal(5),
            CategoryId = r.IsDBNull(6) ? null : r.GetString(6),
            CategoryName = r.IsDBNull(7) ? null : r.GetString(7),
            StockQuantity = r.GetDecimal(8),
            MinStock = r.GetDecimal(9),
            ImageUrl = r.IsDBNull(10) ? null : r.GetString(10),
            IsActive = r.GetInt32(11) == 1,
            CreatedAt = DateTime.Parse(r.GetString(12)),
            UpdatedAt = DateTime.Parse(r.GetString(13))
        };
    }

    // ─── Customers ───────────────────────────────────────────────────────────────

    public static class CustomerService
    {
        public static List<Customer> GetAll()
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, email, phone, address, loyalty_points, notes, created_at, updated_at FROM customers WHERE deleted_at IS NULL ORDER BY name";
            var list = new List<Customer>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(Map(r));
            return list;
        }

        public static List<Customer> Search(string query)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            var q = "%" + query + "%";
            cmd.CommandText = "SELECT id, name, email, phone, address, loyalty_points, notes, created_at, updated_at FROM customers WHERE deleted_at IS NULL AND (name LIKE @q OR phone LIKE @q OR email LIKE @q) ORDER BY name";
            cmd.Parameters.AddWithValue("@q", q);
            var list = new List<Customer>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(Map(r));
            return list;
        }

        public static void Save(Customer c)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO customers (id, name, email, phone, address, loyalty_points, notes, created_at, updated_at, synced)
                VALUES (@id,@name,@email,@phone,@addr,@lp,@notes,@created,@updated,0)";
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.Parameters.AddWithValue("@name", c.Name);
            cmd.Parameters.AddWithValue("@email", (object?)c.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone", (object?)c.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@addr", (object?)c.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lp", c.LoyaltyPoints);
            cmd.Parameters.AddWithValue("@notes", (object?)c.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", c.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void Delete(string id)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE customers SET deleted_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Customer Map(SqliteDataReader r) => new()
        {
            Id = r.GetString(0),
            Name = r.GetString(1),
            Email = r.IsDBNull(2) ? null : r.GetString(2),
            Phone = r.IsDBNull(3) ? null : r.GetString(3),
            Address = r.IsDBNull(4) ? null : r.GetString(4),
            LoyaltyPoints = r.GetInt32(5),
            Notes = r.IsDBNull(6) ? null : r.GetString(6),
            CreatedAt = DateTime.Parse(r.GetString(7)),
            UpdatedAt = DateTime.Parse(r.GetString(8))
        };
    }

    // ─── Users ───────────────────────────────────────────────────────────────────

    public static class UserService
    {
        public static User? Login(string pin)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, email, role, pin, is_active, created_at, updated_at FROM users WHERE pin=@pin AND is_active=1 LIMIT 1";
            cmd.Parameters.AddWithValue("@pin", pin);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public static List<User> GetAll()
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, email, role, pin, is_active, created_at, updated_at FROM users ORDER BY name";
            var list = new List<User>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(Map(r));
            return list;
        }

        public static void Save(User u)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO users (id, name, email, role, pin, is_active, created_at, updated_at)
                VALUES (@id,@name,@email,@role,@pin,@active,@created,@updated)";
            cmd.Parameters.AddWithValue("@id", u.Id);
            cmd.Parameters.AddWithValue("@name", u.Name);
            cmd.Parameters.AddWithValue("@email", (object?)u.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@role", u.Role);
            cmd.Parameters.AddWithValue("@pin", u.Pin);
            cmd.Parameters.AddWithValue("@active", u.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@created", u.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void Delete(string id)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE users SET is_active=0, updated_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static User Map(SqliteDataReader r) => new()
        {
            Id = r.GetString(0),
            Name = r.GetString(1),
            Email = r.IsDBNull(2) ? "" : r.GetString(2),
            Role = r.GetString(3),
            Pin = r.GetString(4),
            IsActive = r.GetInt32(5) == 1,
            CreatedAt = DateTime.Parse(r.GetString(6)),
            UpdatedAt = DateTime.Parse(r.GetString(7))
        };
    }

    // ─── Transactions ────────────────────────────────────────────────────────────

    public static class TransactionService
    {
        public static string Checkout(Transaction tx)
        {
            tx.Id = Guid.NewGuid().ToString();
            tx.TransactionNumber = Database.GenerateTransactionNumber();
            tx.CreatedAt = DateTime.UtcNow;
            tx.UpdatedAt = DateTime.UtcNow;

            using var conn = Database.GetConnection();
            using var dbTx = conn.BeginTransaction();
            try
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = dbTx;
                cmd.CommandText = @"INSERT INTO transactions (id, transaction_number, customer_id, subtotal, tax_rate, tax_amount, discount_amount, total,
                    payment_method, cash_given, change_given, status, notes, cashier_id, location_id, created_at, updated_at, synced)
                    VALUES (@id,@txn,@cust,@sub,@taxr,@taxa,@disc,@total,@pmeth,@cash,@change,@status,@notes,@cashier,@loc,@created,@updated,0)";
                cmd.Parameters.AddWithValue("@id", tx.Id);
                cmd.Parameters.AddWithValue("@txn", tx.TransactionNumber);
                cmd.Parameters.AddWithValue("@cust", (object?)tx.CustomerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sub", tx.Subtotal);
                cmd.Parameters.AddWithValue("@taxr", tx.TaxRate);
                cmd.Parameters.AddWithValue("@taxa", tx.TaxAmount);
                cmd.Parameters.AddWithValue("@disc", tx.DiscountAmount);
                cmd.Parameters.AddWithValue("@total", tx.Total);
                cmd.Parameters.AddWithValue("@pmeth", tx.PaymentMethod);
                cmd.Parameters.AddWithValue("@cash", tx.CashGiven);
                cmd.Parameters.AddWithValue("@change", tx.ChangeGiven);
                cmd.Parameters.AddWithValue("@status", tx.Status);
                cmd.Parameters.AddWithValue("@notes", (object?)tx.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cashier", tx.CashierId);
                cmd.Parameters.AddWithValue("@loc", tx.LocationId);
                cmd.Parameters.AddWithValue("@created", tx.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@updated", tx.UpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();

                foreach (var item in tx.Items)
                {
                    item.Id = Guid.NewGuid().ToString();
                    item.TransactionId = tx.Id;
                    var ic = conn.CreateCommand();
                    ic.Transaction = dbTx;
                    ic.CommandText = "INSERT INTO transaction_items (id, transaction_id, product_id, product_name, sku, quantity, unit_price, discount_amount, total) VALUES (@id,@txid,@pid,@pname,@sku,@qty,@up,@disc,@total)";
                    ic.Parameters.AddWithValue("@id", item.Id);
                    ic.Parameters.AddWithValue("@txid", tx.Id);
                    ic.Parameters.AddWithValue("@pid", item.ProductId);
                    ic.Parameters.AddWithValue("@pname", item.ProductName);
                    ic.Parameters.AddWithValue("@sku", item.Sku);
                    ic.Parameters.AddWithValue("@qty", item.Quantity);
                    ic.Parameters.AddWithValue("@up", item.UnitPrice);
                    ic.Parameters.AddWithValue("@disc", item.DiscountAmount);
                    ic.Parameters.AddWithValue("@total", item.Total);
                    ic.ExecuteNonQuery();

                    // Deduct stock
                    var sc = conn.CreateCommand();
                    sc.Transaction = dbTx;
                    sc.CommandText = "UPDATE products SET stock_quantity = stock_quantity - @qty, updated_at=@now WHERE id=@id";
                    sc.Parameters.AddWithValue("@qty", item.Quantity);
                    sc.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    sc.Parameters.AddWithValue("@id", item.ProductId);
                    sc.ExecuteNonQuery();
                }

                dbTx.Commit();
                return tx.TransactionNumber;
            }
            catch
            {
                dbTx.Rollback();
                throw;
            }
        }

        public static List<Transaction> GetRecent(int limit = 50, string? dateFrom = null, string? dateTo = null)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            var where = "WHERE 1=1";
            if (dateFrom != null) { where += " AND t.created_at >= @from"; cmd.Parameters.AddWithValue("@from", dateFrom); }
            if (dateTo != null) { where += " AND t.created_at <= @to"; cmd.Parameters.AddWithValue("@to", dateTo); }
            cmd.CommandText = $@"SELECT t.id, t.transaction_number, t.customer_id, c.name, t.subtotal, t.tax_rate,
                t.tax_amount, t.discount_amount, t.total, t.payment_method, t.cash_given, t.change_given,
                t.status, t.notes, t.cashier_id, u.name, t.location_id, t.created_at
                FROM transactions t
                LEFT JOIN customers c ON t.customer_id = c.id
                LEFT JOIN users u ON t.cashier_id = u.id
                {where} ORDER BY t.created_at DESC LIMIT {limit}";
            var list = new List<Transaction>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapTx(r));
            return list;
        }

        public static List<TransactionItem> GetItems(string txId)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, transaction_id, product_id, product_name, sku, quantity, unit_price, discount_amount, total FROM transaction_items WHERE transaction_id=@id";
            cmd.Parameters.AddWithValue("@id", txId);
            var list = new List<TransactionItem>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new TransactionItem
                {
                    Id = r.GetString(0),
                    TransactionId = r.GetString(1),
                    ProductId = r.GetString(2),
                    ProductName = r.GetString(3),
                    Sku = r.GetString(4),
                    Quantity = r.GetDecimal(5),
                    UnitPrice = r.GetDecimal(6),
                    DiscountAmount = r.GetDecimal(7),
                    Total = r.GetDecimal(8)
                });
            return list;
        }

        public static void Refund(string txId)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE transactions SET status='refunded', updated_at=@now WHERE id=@id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", txId);
            cmd.ExecuteNonQuery();
        }

        private static Transaction MapTx(SqliteDataReader r) => new()
        {
            Id = r.GetString(0),
            TransactionNumber = r.GetString(1),
            CustomerId = r.IsDBNull(2) ? null : r.GetString(2),
            CustomerName = r.IsDBNull(3) ? null : r.GetString(3),
            Subtotal = r.GetDecimal(4),
            TaxRate = r.GetDecimal(5),
            TaxAmount = r.GetDecimal(6),
            DiscountAmount = r.GetDecimal(7),
            Total = r.GetDecimal(8),
            PaymentMethod = r.GetString(9),
            CashGiven = r.GetDecimal(10),
            ChangeGiven = r.GetDecimal(11),
            Status = r.GetString(12),
            Notes = r.IsDBNull(13) ? null : r.GetString(13),
            CashierId = r.GetString(14),
            CashierName = r.IsDBNull(15) ? "" : r.GetString(15),
            LocationId = r.GetString(16),
            CreatedAt = DateTime.Parse(r.GetString(17))
        };
    }

    // ─── Reports ─────────────────────────────────────────────────────────────────

    public static class ReportService
    {
        public static ReportSummary GetSummary(DateTime from, DateTime to)
        {
            using var conn = Database.GetConnection();
            var f = from.ToString("o");
            var t = to.AddDays(1).ToString("o");

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COALESCE(SUM(total),0), COUNT(*), COALESCE(AVG(total),0),
                COALESCE(SUM(tax_amount),0), COALESCE(SUM(discount_amount),0)
                FROM transactions WHERE status='completed' AND created_at >= @from AND created_at < @to";
            cmd.Parameters.AddWithValue("@from", f);
            cmd.Parameters.AddWithValue("@to", t);
            using var r = cmd.ExecuteReader();
            decimal sales = 0, avg = 0, tax = 0, disc = 0;
            int count = 0;
            if (r.Read())
            {
                sales = r.GetDecimal(0);
                count = r.GetInt32(1);
                avg = r.GetDecimal(2);
                tax = r.GetDecimal(3);
                disc = r.GetDecimal(4);
            }

            // Expenses
            cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(amount),0) FROM expenses WHERE expense_date >= @from AND expense_date < @to";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to", to.AddDays(1).ToString("yyyy-MM-dd"));
            var expTotal = (decimal)(cmd.ExecuteScalar() ?? 0m);

            // Top products
            cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT ti.product_name, SUM(ti.quantity), SUM(ti.total)
                FROM transaction_items ti
                JOIN transactions tx ON ti.transaction_id = tx.id
                WHERE tx.status='completed' AND tx.created_at >= @from AND tx.created_at < @to
                GROUP BY ti.product_id, ti.product_name ORDER BY SUM(ti.total) DESC LIMIT 10";
            cmd.Parameters.AddWithValue("@from", f);
            cmd.Parameters.AddWithValue("@to", t);
            var topProducts = new List<ProductSalesSummary>();
            using var r2 = cmd.ExecuteReader();
            while (r2.Read())
                topProducts.Add(new ProductSalesSummary
                {
                    ProductName = r2.GetString(0),
                    QuantitySold = r2.GetDecimal(1),
                    Revenue = r2.GetDecimal(2)
                });

            // Daily sales
            cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DATE(created_at), COALESCE(SUM(total),0), COUNT(*)
                FROM transactions WHERE status='completed' AND created_at >= @from AND created_at < @to
                GROUP BY DATE(created_at) ORDER BY DATE(created_at)";
            cmd.Parameters.AddWithValue("@from", f);
            cmd.Parameters.AddWithValue("@to", t);
            var daily = new List<DailySales>();
            using var r3 = cmd.ExecuteReader();
            while (r3.Read())
                daily.Add(new DailySales
                {
                    Date = r3.GetString(0),
                    Sales = r3.GetDecimal(1),
                    Transactions = r3.GetInt32(2)
                });

            return new ReportSummary
            {
                TotalSales = sales,
                TransactionCount = count,
                AverageTransaction = avg,
                TotalTax = tax,
                TotalDiscount = disc,
                TotalExpenses = expTotal,
                TopProducts = topProducts,
                DailySalesData = daily
            };
        }
    }

    // ─── Accounting / Expenses ───────────────────────────────────────────────────

    public static class AccountingService
    {
        public static List<Expense> GetExpenses(DateTime? from = null, DateTime? to = null)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            var where = "WHERE 1=1";
            if (from.HasValue) { where += " AND expense_date >= @from"; cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd")); }
            if (to.HasValue) { where += " AND expense_date <= @to"; cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd")); }
            cmd.CommandText = $"SELECT id, description, category, amount, payment_method, notes, recorded_by, expense_date, created_at FROM expenses {where} ORDER BY expense_date DESC";
            var list = new List<Expense>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Expense
                {
                    Id = r.GetString(0),
                    Description = r.GetString(1),
                    Category = r.GetString(2),
                    Amount = r.GetDecimal(3),
                    PaymentMethod = r.GetString(4),
                    Notes = r.IsDBNull(5) ? null : r.GetString(5),
                    RecordedBy = r.GetString(6),
                    ExpenseDate = DateTime.Parse(r.GetString(7)),
                    CreatedAt = DateTime.Parse(r.GetString(8))
                });
            return list;
        }

        public static void SaveExpense(Expense e)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO expenses (id, description, category, amount, payment_method, notes, recorded_by, expense_date, created_at)
                VALUES (@id,@desc,@cat,@amt,@pmeth,@notes,@by,@date,@created)";
            cmd.Parameters.AddWithValue("@id", e.Id);
            cmd.Parameters.AddWithValue("@desc", e.Description);
            cmd.Parameters.AddWithValue("@cat", e.Category);
            cmd.Parameters.AddWithValue("@amt", e.Amount);
            cmd.Parameters.AddWithValue("@pmeth", e.PaymentMethod);
            cmd.Parameters.AddWithValue("@notes", (object?)e.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@by", e.RecordedBy);
            cmd.Parameters.AddWithValue("@date", e.ExpenseDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@created", e.CreatedAt.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static void DeleteExpense(string id)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM expenses WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static (decimal income, decimal expenses, decimal profit) GetPLSummary(DateTime from, DateTime to)
        {
            using var conn = Database.GetConnection();
            var f = from.ToString("o");
            var t = to.AddDays(1).ToString("o");

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(total),0) FROM transactions WHERE status='completed' AND created_at >= @from AND created_at < @to";
            cmd.Parameters.AddWithValue("@from", f);
            cmd.Parameters.AddWithValue("@to", t);
            var income = (decimal)(cmd.ExecuteScalar() ?? 0m);

            cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(amount),0) FROM expenses WHERE expense_date >= @from AND expense_date <= @to";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));
            var expenses = (decimal)(cmd.ExecuteScalar() ?? 0m);

            return (income, expenses, income - expenses);
        }
    }

    // ─── Receipt Template ────────────────────────────────────────────────────────

    public static class ReceiptService
    {
        public static ReceiptTemplate Get()
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, header, footer, business_name, business_address, business_phone, show_logo, show_tax_breakdown, show_customer_info, font_size FROM receipt_templates WHERE id='default'";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return new ReceiptTemplate();
            return new ReceiptTemplate
            {
                Id = r.GetString(0),
                Header = r.GetString(1),
                Footer = r.GetString(2),
                BusinessName = r.GetString(3),
                BusinessAddress = r.GetString(4),
                BusinessPhone = r.GetString(5),
                ShowLogo = r.GetInt32(6) == 1,
                ShowTaxBreakdown = r.GetInt32(7) == 1,
                ShowCustomerInfo = r.GetInt32(8) == 1,
                FontSize = r.GetString(9)
            };
        }

        public static void Save(ReceiptTemplate t)
        {
            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO receipt_templates
                (id, header, footer, business_name, business_address, business_phone, show_logo, show_tax_breakdown, show_customer_info, font_size, updated_at)
                VALUES ('default',@header,@footer,@bname,@baddr,@bphone,@logo,@tax,@cust,@font,@now)";
            cmd.Parameters.AddWithValue("@header", t.Header);
            cmd.Parameters.AddWithValue("@footer", t.Footer);
            cmd.Parameters.AddWithValue("@bname", t.BusinessName);
            cmd.Parameters.AddWithValue("@baddr", t.BusinessAddress);
            cmd.Parameters.AddWithValue("@bphone", t.BusinessPhone);
            cmd.Parameters.AddWithValue("@logo", t.ShowLogo ? 1 : 0);
            cmd.Parameters.AddWithValue("@tax", t.ShowTaxBreakdown ? 1 : 0);
            cmd.Parameters.AddWithValue("@cust", t.ShowCustomerInfo ? 1 : 0);
            cmd.Parameters.AddWithValue("@font", t.FontSize);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static string FormatReceipt(Transaction tx, ReceiptTemplate tmpl)
        {
            var currency = SettingsService.Get("currency_symbol", "$");
            var lines = new System.Text.StringBuilder();
            lines.AppendLine(tmpl.BusinessName.PadLeft((40 + tmpl.BusinessName.Length) / 2).PadRight(40));
            if (!string.IsNullOrWhiteSpace(tmpl.BusinessAddress))
                lines.AppendLine(tmpl.BusinessAddress.PadLeft((40 + tmpl.BusinessAddress.Length) / 2).PadRight(40));
            if (!string.IsNullOrWhiteSpace(tmpl.BusinessPhone))
                lines.AppendLine(tmpl.BusinessPhone.PadLeft((40 + tmpl.BusinessPhone.Length) / 2).PadRight(40));
            lines.AppendLine(new string('-', 40));
            lines.AppendLine($"Receipt: {tx.TransactionNumber}");
            lines.AppendLine($"Date:    {tx.CreatedAt:yyyy-MM-dd HH:mm}");
            lines.AppendLine($"Cashier: {tx.CashierName}");
            if (tmpl.ShowCustomerInfo && !string.IsNullOrEmpty(tx.CustomerName))
                lines.AppendLine($"Customer: {tx.CustomerName}");
            lines.AppendLine(new string('-', 40));
            foreach (var item in tx.Items)
            {
                var left = $"{item.ProductName} x{item.Quantity}";
                var right = $"{currency}{item.Total:F2}";
                lines.AppendLine(left.PadRight(40 - right.Length) + right);
            }
            lines.AppendLine(new string('-', 40));
            lines.AppendLine($"{"Subtotal:",-20}{currency}{tx.Subtotal:F2,10}");
            if (tmpl.ShowTaxBreakdown)
                lines.AppendLine($"{"Tax:",-20}{currency}{tx.TaxAmount:F2,10}");
            if (tx.DiscountAmount > 0)
                lines.AppendLine($"{"Discount:",-20}-{currency}{tx.DiscountAmount:F2,10}");
            lines.AppendLine($"{"TOTAL:",-20}{currency}{tx.Total:F2,10}");
            lines.AppendLine($"{"Paid:",-20}{currency}{tx.CashGiven:F2,10}");
            lines.AppendLine($"{"Change:",-20}{currency}{tx.ChangeGiven:F2,10}");
            lines.AppendLine(new string('-', 40));
            if (!string.IsNullOrWhiteSpace(tmpl.Header))
                lines.AppendLine(tmpl.Header.PadLeft((40 + tmpl.Header.Length) / 2).PadRight(40));
            if (!string.IsNullOrWhiteSpace(tmpl.Footer))
                lines.AppendLine(tmpl.Footer.PadLeft((40 + tmpl.Footer.Length) / 2).PadRight(40));
            return lines.ToString();
        }
    }

    // ─── Print Service ───────────────────────────────────────────────────────────

    public static class PrintService
    {
        public static List<string> GetPrinters()
        {
            var printers = new List<string>();
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                printers.Add(printer);
            return printers;
        }

        public static void PrintReceipt(string receiptText, string? printerName = null)
        {
            var pd = new System.Drawing.Printing.PrintDocument();
            if (!string.IsNullOrEmpty(printerName))
                pd.PrinterSettings.PrinterName = printerName;

            pd.PrintPage += (s, e) =>
            {
                if (e.Graphics == null) return;
                var font = new System.Drawing.Font("Courier New", 9);
                float y = 10;
                foreach (var line in receiptText.Split('\n'))
                {
                    e.Graphics.DrawString(line, font, System.Drawing.Brushes.Black, 10, y);
                    y += font.GetHeight();
                }
                font.Dispose();
            };

            pd.Print();
        }
    }
}
