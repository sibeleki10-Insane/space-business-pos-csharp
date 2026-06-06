using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace SpaceBusinessPOS.Data
{
    public static class Database
    {
        private static string _connectionString = "";

        public static void Initialize()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SpaceBusinessPOS");
            Directory.CreateDirectory(folder);
            var dbPath = Path.Combine(folder, "pos.db");
            _connectionString = $"Data Source={dbPath}";
            RunMigrations();
            SeedDefaults();
        }

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var pragma1 = conn.CreateCommand();
            pragma1.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            pragma1.ExecuteNonQuery();
            return conn;
        }

        private static void RunMigrations()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER PRIMARY KEY,
                    applied_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS locations (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    address TEXT,
                    tax_rate REAL NOT NULL DEFAULT 0,
                    currency TEXT NOT NULL DEFAULT 'USD',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    email TEXT,
                    role TEXT NOT NULL DEFAULT 'cashier',
                    pin TEXT NOT NULL,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS categories (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    color TEXT NOT NULL DEFAULT '#3b82f6',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    deleted_at TEXT,
                    synced INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS products (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    sku TEXT NOT NULL UNIQUE,
                    barcode TEXT,
                    price REAL NOT NULL DEFAULT 0,
                    cost REAL NOT NULL DEFAULT 0,
                    category_id TEXT,
                    stock_quantity REAL NOT NULL DEFAULT 0,
                    min_stock REAL NOT NULL DEFAULT 0,
                    image_url TEXT,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    deleted_at TEXT,
                    synced INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS customers (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    email TEXT,
                    phone TEXT,
                    address TEXT,
                    loyalty_points INTEGER NOT NULL DEFAULT 0,
                    notes TEXT,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    deleted_at TEXT,
                    synced INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS transactions (
                    id TEXT PRIMARY KEY,
                    transaction_number TEXT NOT NULL UNIQUE,
                    customer_id TEXT,
                    subtotal REAL NOT NULL DEFAULT 0,
                    tax_rate REAL NOT NULL DEFAULT 0,
                    tax_amount REAL NOT NULL DEFAULT 0,
                    discount_amount REAL NOT NULL DEFAULT 0,
                    total REAL NOT NULL DEFAULT 0,
                    payment_method TEXT NOT NULL DEFAULT 'cash',
                    cash_given REAL NOT NULL DEFAULT 0,
                    change_given REAL NOT NULL DEFAULT 0,
                    status TEXT NOT NULL DEFAULT 'completed',
                    notes TEXT,
                    cashier_id TEXT NOT NULL,
                    location_id TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    synced INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS transaction_items (
                    id TEXT PRIMARY KEY,
                    transaction_id TEXT NOT NULL,
                    product_id TEXT NOT NULL,
                    product_name TEXT NOT NULL,
                    sku TEXT NOT NULL,
                    quantity REAL NOT NULL,
                    unit_price REAL NOT NULL,
                    discount_amount REAL NOT NULL DEFAULT 0,
                    total REAL NOT NULL
                );
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS expenses (
                    id TEXT PRIMARY KEY,
                    description TEXT NOT NULL,
                    category TEXT NOT NULL DEFAULT 'General',
                    amount REAL NOT NULL DEFAULT 0,
                    payment_method TEXT NOT NULL DEFAULT 'cash',
                    notes TEXT,
                    recorded_by TEXT NOT NULL DEFAULT '',
                    expense_date TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS receipt_templates (
                    id TEXT PRIMARY KEY,
                    header TEXT NOT NULL,
                    footer TEXT NOT NULL,
                    business_name TEXT NOT NULL,
                    business_address TEXT NOT NULL DEFAULT '',
                    business_phone TEXT NOT NULL DEFAULT '',
                    show_logo INTEGER NOT NULL DEFAULT 1,
                    show_tax_breakdown INTEGER NOT NULL DEFAULT 1,
                    show_customer_info INTEGER NOT NULL DEFAULT 1,
                    font_size TEXT NOT NULL DEFAULT 'Normal',
                    updated_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id);
                CREATE INDEX IF NOT EXISTS idx_transactions_created ON transactions(created_at);
                CREATE INDEX IF NOT EXISTS idx_transaction_items_txn ON transaction_items(transaction_id);
                CREATE INDEX IF NOT EXISTS idx_expenses_date ON expenses(expense_date);
            ";
            cmd.ExecuteNonQuery();
        }

        private static void SeedDefaults()
        {
            using var conn = GetConnection();

            // Seed location
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM locations";
            var count = (long)(check.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var insert = conn.CreateCommand();
                insert.CommandText = "INSERT INTO locations (id, name, address, tax_rate, currency, created_at, updated_at) VALUES (@id, @name, null, 10, 'USD', @now, @now)";
                insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                insert.Parameters.AddWithValue("@name", "Main Store");
                insert.Parameters.AddWithValue("@now", now);
                insert.ExecuteNonQuery();
            }

            // Seed admin user
            check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM users WHERE role='admin'";
            count = (long)(check.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var insert = conn.CreateCommand();
                insert.CommandText = "INSERT INTO users (id, name, email, role, pin, is_active, created_at, updated_at) VALUES (@id, 'Admin', 'admin@pos.local', 'admin', '1234', 1, @now, @now)";
                insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                insert.Parameters.AddWithValue("@now", now);
                insert.ExecuteNonQuery();
            }

            // Seed categories
            check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM categories";
            count = (long)(check.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                string[][] cats = {
                    ["General", "#6b7280"],
                    ["Food & Drinks", "#f59e0b"],
                    ["Electronics", "#3b82f6"],
                    ["Clothing", "#8b5cf6"]
                };
                foreach (var cat in cats)
                {
                    var insert = conn.CreateCommand();
                    insert.CommandText = "INSERT INTO categories (id, name, color, created_at, updated_at, synced) VALUES (@id, @name, @color, @now, @now, 0)";
                    insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    insert.Parameters.AddWithValue("@name", cat[0]);
                    insert.Parameters.AddWithValue("@color", cat[1]);
                    insert.Parameters.AddWithValue("@now", now);
                    insert.ExecuteNonQuery();
                }
            }

            // Seed settings
            check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM app_settings";
            count = (long)(check.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var defaults = new Dictionary<string, string>
                {
                    ["tax_rate"] = "10",
                    ["currency"] = "USD",
                    ["currency_symbol"] = "$",
                    ["location_name"] = "Main Store",
                    ["supabase_url"] = "",
                    ["supabase_anon_key"] = "",
                    ["receipt_header"] = "Thank you for shopping with us!",
                    ["receipt_footer"] = "Please come again.",
                    ["auto_print_receipt"] = "false",
                    ["theme"] = "dark",
                    ["printer_name"] = "",
                    ["low_stock_alert"] = "true"
                };
                foreach (var kv in defaults)
                {
                    var insert = conn.CreateCommand();
                    insert.CommandText = "INSERT INTO app_settings (key, value, updated_at) VALUES (@k, @v, @now)";
                    insert.Parameters.AddWithValue("@k", kv.Key);
                    insert.Parameters.AddWithValue("@v", kv.Value);
                    insert.Parameters.AddWithValue("@now", now);
                    insert.ExecuteNonQuery();
                }
            }

            // Seed receipt template
            check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM receipt_templates";
            count = (long)(check.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var insert = conn.CreateCommand();
                insert.CommandText = @"INSERT INTO receipt_templates (id, header, footer, business_name, business_address, business_phone, show_logo, show_tax_breakdown, show_customer_info, font_size, updated_at)
                    VALUES ('default', 'Thank you for shopping with us!', 'Please come again.', 'Space-Business POS', '', '', 1, 1, 1, 'Normal', @now)";
                insert.Parameters.AddWithValue("@now", now);
                insert.ExecuteNonQuery();
            }
        }

        public static string GenerateTransactionNumber()
        {
            using var conn = GetConnection();
            var today = DateTime.Today;
            var datePart = today.ToString("yyyyMMdd");
            var likePattern = today.ToString("yyyy-MM-dd") + "%";
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM transactions WHERE created_at LIKE @pattern";
            cmd.Parameters.AddWithValue("@pattern", likePattern);
            var count = (long)(cmd.ExecuteScalar() ?? 0L);
            return $"TXN-{datePart}-{(count + 1):D4}";
        }
    }
}
