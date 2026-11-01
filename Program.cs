using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;



namespace BookShop.Models
{
   
    public class Book
    {
        private string _title;
        private string _author;
        private decimal _price;
        private int _stock;

        public Book(string title, string author, decimal price, int stock)
        {
            Title = title;
            Author = author;
            Price = price;
            Stock = stock;
        }

        public string Title
        {
            get => _title;
            set => _title = string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
        }

        public string Author
        {
            get => _author;
            set => _author = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        }

        public decimal Price
        {
            get => _price;
            set => _price = value < 0 ? 0 : value;
        }

        public int Stock
        {
            get => _stock;
            set => _stock = value < 0 ? 0 : value;
        }

        public bool InStock => _stock > 0;

        public decimal StockValue => _price * _stock;

        
        public bool Sell()
        {
            if (_stock <= 0) return false;
            _stock--;
            return true;
        }

        public override string ToString() =>
            $"{Title} - written by {Author} | Rate: Rs.{Price} | Remaining: {Stock}";
    }
}


namespace BookShop.Data
{
    using BookShop.Models;

    
   

    public class BookRepository
    {
        private readonly string _path;
        private readonly List<Book> _books = new List<Book>();

        public BookRepository(string path)
        {
            _path = path;
            Load();
        }

        public List<Book> GetAll() => _books;
        public void Add(Book book) => _books.Add(book);
        public void Remove(Book book) => _books.Remove(book);

        private void Load()
        {
            if (!File.Exists(_path))
            {
               
                _books.Add(new Book("The Hobbit", "Tolkien", 2000, 10));
                _books.Add(new Book("1984", "Orwell", 1500, 5));
                _books.Add(new Book("fire and blood", "George Martin", 3000, 8));
                return;
            }

            string[] lines = File.ReadAllLines(_path);
            if (lines.Length == 0 || !int.TryParse(lines[0], out int count)) return;

            for (int i = 0; i < count; i++)
            {
                int pos = 1 + i * 4;
                if (pos + 3 >= lines.Length) break;

                decimal.TryParse(lines[pos + 2], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price);
                int.TryParse(lines[pos + 3], out int stock);
                _books.Add(new Book(lines[pos], lines[pos + 1], price, stock));
            }
        }

        public void Save()
        {
            var lines = new List<string> { _books.Count.ToString() };
            foreach (Book b in _books)
            {
                lines.Add(b.Title);
                lines.Add(b.Author);
                lines.Add(b.Price.ToString(CultureInfo.InvariantCulture));
                lines.Add(b.Stock.ToString());
            }
            File.WriteAllLines(_path, lines);
        }
    }
}


namespace BookShop.Services
{
    using BookShop.Data;
    using BookShop.Models;

    public class ActionLog
    {
        private readonly List<string> _entries = new List<string>();
        public IReadOnlyList<string> Entries => _entries;
        public void Add(string entry) => _entries.Add(entry);
        public void Clear() => _entries.Clear();
    }

    
    public class InventoryService
    {
        private readonly BookRepository _repo;
        public const int MaxBooks = 50;

        public InventoryService(BookRepository repo)
        {
            _repo = repo;
        }

        public List<Book> GetAll() => _repo.GetAll();
        public int Count => _repo.GetAll().Count;

        public Book GetByIndex(int index)
        {
            var all = _repo.GetAll();
            return index >= 0 && index < all.Count ? all[index] : null;
        }

        public bool AddBook(Book book)
        {
            if (Count >= MaxBooks) return false;
            _repo.Add(book);
            return true;
        }

        public bool RemoveBook(int index)
        {
            Book b = GetByIndex(index);
            if (b == null) return false;
            _repo.Remove(b);
            return true;
        }

        public bool ChangePrice(int index, decimal newPrice)
        {
            Book b = GetByIndex(index);
            if (b == null) return false;
            b.Price = newPrice;
            return true;
        }

        public void SortByPrice() => SortWith(b => b.Price);
        public void SortByTitle() => SortWith(b => b.Title);

        private void SortWith<TKey>(Func<Book, TKey> key)
        {
            var sorted = _repo.GetAll().OrderBy(key).ToList();
            _repo.GetAll().Clear();
            _repo.GetAll().AddRange(sorted);
        }

        public Book FindByTitle(string title) =>
            _repo.GetAll().FirstOrDefault(b =>
                b.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

        public decimal TotalStockValue() => _repo.GetAll().Sum(b => b.StockValue);

        public Book Cheapest() => _repo.GetAll().OrderBy(b => b.Price).FirstOrDefault();
        public Book MostExpensive() => _repo.GetAll().OrderByDescending(b => b.Price).FirstOrDefault();

        public List<Book> UnderBudget(decimal max) =>
            _repo.GetAll().Where(b => b.Price <= max).ToList();

       
        public List<Book> PricedAbove(decimal amount) =>
            _repo.GetAll().Where(b => b.Price > amount).ToList();

        public void Save() => _repo.Save();
    }
}


namespace BookShop.UI
{
    using BookShop.Models;
    using BookShop.Services;

   
    public static class Input
    {
        public static int ReadInt(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int value)) return value;
                Console.WriteLine("Please enter a valid whole number.");
            }
        }

        public static decimal ReadDecimal(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (decimal.TryParse(Console.ReadLine(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
                    return value;
                Console.WriteLine("Please enter a valid number.");
            }
        }

        public static string ReadString(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine() ?? "";
        }
    }

    
    public abstract class Menu
    {
        protected readonly InventoryService Inventory;
        protected readonly ActionLog Log;

        protected Menu(InventoryService inventory, ActionLog log)
        {
            Inventory = inventory;
            Log = log;
        }

        protected abstract string Title { get; }
        protected abstract int ExitOption { get; }
        protected abstract void ShowOptions();
        protected abstract void HandleChoice(int choice);

        
        public void Run()
        {
            int choice = 0;
            while (choice != ExitOption)
            {
                Console.WriteLine($"\n {Title}");
                ShowOptions();
                choice = Input.ReadInt("Enter choice: ");
                if (choice != ExitOption) HandleChoice(choice);
            }
        }

        protected void PrintAll()
        {
            var books = Inventory.GetAll();
            for (int i = 0; i < books.Count; i++)
                Console.WriteLine($"{i + 1}. {books[i]}");
        }
    }

    public class AdminMenu : Menu
    {
        public AdminMenu(InventoryService inventory, ActionLog log) : base(inventory, log) { }

        protected override string Title => "admin menu";
        protected override int ExitOption => 11;

        protected override void ShowOptions()
        {
            Console.WriteLine("1. Add New Book");
            Console.WriteLine("2. Display Current Inventory");
            Console.WriteLine("3. Modify Book Price");
            Console.WriteLine("4. Remove Book From System");
            Console.WriteLine("5. Sort Inventory By Price");
            Console.WriteLine("6. Sort Inventory Alphabetically");
            Console.WriteLine("7. Search Book Database");
            Console.WriteLine("8. Check Total Stock Value");
            Console.WriteLine("9. Review Action History Log");
            Console.WriteLine("10. Show Books Priced Above 1000");
            Console.WriteLine("11. Log Out");
        }

        protected override void HandleChoice(int choice)
        {
            switch (choice)
            {
                case 1: AddBook(); break;
                case 2:
                    Console.WriteLine("\n--- BOOK CATALOG DETAILS ---");
                    PrintAll();
                    break;
                case 3: ModifyPrice(); break;
                case 4: RemoveBook(); break;
                case 5:
                    Inventory.SortByPrice();
                    Log.Add("Admin: sorted by price");
                    Console.WriteLine("\nDatabase reorganized by ascending rates.");
                    break;
                case 6:
                    Inventory.SortByTitle();
                    Log.Add("Admin: sorted alphabetically");
                    Console.WriteLine("\nDatabase organized alphabetically by book names.");
                    break;
                case 7: Search(); break;
                case 8:
                    Console.WriteLine($"\nNet Asset Worth of Current Inventory: Rs. {Inventory.TotalStockValue()}");
                    break;
                case 9: ShowLog(); break;
                case 10: ShowAbove1000(); break;
                default: Console.WriteLine("Invalid choice."); break;
            }
        }

        private void AddBook()
        {
            if (Inventory.Count >= InventoryService.MaxBooks)
            {
                Console.WriteLine("Error: Storage full.");
                return;
            }
            string title = Input.ReadString("Enter Title: ");
            string author = Input.ReadString("Enter Author: ");
            decimal price = Input.ReadDecimal("Enter Price: ");
            int stock = Input.ReadInt("Enter Stock Units: ");

            Inventory.AddBook(new Book(title, author, price, stock));
            Log.Add($"Admin: added '{title}'");
            Console.WriteLine(" Book record updated safely.");
        }

        private void ModifyPrice()
        {
            int target = Input.ReadInt($"Select item number (1 to {Inventory.Count}): ") - 1;
            Book b = Inventory.GetByIndex(target);
            if (b == null)
            {
                Console.WriteLine("Error: Selection index out of bounds.");
                return;
            }
            Console.WriteLine($"Editing record for: {b.Title}");
            decimal price = Input.ReadDecimal("Set New Price: ");
            Inventory.ChangePrice(target, price);
            Log.Add($"Admin: changed price of '{b.Title}' to {price}");
            Console.WriteLine("Rate has been changed successfully.");
        }

        private void RemoveBook()
        {
            int target = Input.ReadInt("Select item number to delete: ") - 1;
            Book b = Inventory.GetByIndex(target);
            if (b != null && Inventory.RemoveBook(target))
            {
                Log.Add($"Admin: removed '{b.Title}'");
                Console.WriteLine("The selected book has been wiped from database.");
            }
            else
            {
                Console.WriteLine("Error in selection");
            }
        }

        private void Search()
        {
            string query = Input.ReadString("Search Term: ");
            Book b = Inventory.FindByTitle(query);
            if (b == null)
            {
                Console.WriteLine("\nCould not find any entries matching that search.");
                return;
            }
            Console.WriteLine("\nMatch Located");
            Console.WriteLine($"Title Name: {b.Title}\nAuthor Credit: {b.Author}\nPrice Rate: {b.Price}\nAvailable Stock: {b.Stock}");
        }

        private void ShowLog()
        {
            Console.WriteLine("\n--- TRANSACTION LOG DISPLAY ---");
            if (Log.Entries.Count == 0)
            {
                Console.WriteLine("No log rows written yet during this runtime.");
                return;
            }
            for (int i = 0; i < Log.Entries.Count; i++)
                Console.WriteLine($"{i + 1}. {Log.Entries[i]}");
        }

        private void ShowAbove1000()
        {
            var list = Inventory.PricedAbove(1000);
            Console.WriteLine("\n--- BOOKS PRICED ABOVE Rs. 1000 ---");
            if (list.Count == 0) Console.WriteLine("None.");
            foreach (Book b in list)
                Console.WriteLine($"* {b.Title} (Rs. {b.Price})");
        }
    }

   
    public class CustomerMenu : Menu
    {
        public CustomerMenu(InventoryService inventory, ActionLog log) : base(inventory, log) { }

        protected override string Title => "customermenu";
        protected override int ExitOption => 10;

        protected override void ShowOptions()
        {
            Console.WriteLine("1. Display Book Catalog");
            Console.WriteLine("2. Look Up Book Title");
            Console.WriteLine("3. Finalize Book Purchase");
            Console.WriteLine("4. Track My Order History");
            Console.WriteLine("5. Verify Title Availability");
            Console.WriteLine("6. View Lowest Priced Deal");
            Console.WriteLine("7. View Premium Tier Release");
            Console.WriteLine("8. Display Budget Constraints Filter");
            Console.WriteLine("9. Wipe My Order History Records");
            Console.WriteLine("10. Log Out");
        }

        protected override void HandleChoice(int choice)
        {
            switch (choice)
            {
                case 1:
                    Console.WriteLine("\n--- SHOP STORE FRONT ---");
                    var books = Inventory.GetAll();
                    for (int i = 0; i < books.Count; i++)
                        Console.WriteLine($"{i + 1}. {books[i].Title} - Rs. {books[i].Price} [Units left: {books[i].Stock}]");
                    break;
                case 2: LookUp(); break;
                case 3: Purchase(); break;
                case 4: ShowHistory(); break;
                case 5: CheckAvailability(); break;
                case 6: ShowDeal("Lowest Priced Deal", Inventory.Cheapest()); break;
                case 7: ShowDeal("Premium Tier Release", Inventory.MostExpensive()); break;
                case 8: BudgetFilter(); break;
                case 9:
                    Log.Clear();
                    Console.WriteLine("\nYour checkout logging data has been dropped.");
                    break;
                default: Console.WriteLine("Invalid choice."); break;
            }
        }

        private void LookUp()
        {
            string query = Input.ReadString("Enter title name to query: ");
            Book b = Inventory.FindByTitle(query);
            if (b == null)
                Console.WriteLine("\nSorry, that release isn't in our active listings.");
            else
                Console.WriteLine($"\nBook is in store!\nTitle: {b.Title}\nBy: {b.Author}\nPrice: Rs. {b.Price}");
        }

        private void Purchase()
        {
            int target = Input.ReadInt("Enter book choice number to purchase: ") - 1;
            Book b = Inventory.GetByIndex(target);
            if (b == null)
            {
                Console.WriteLine("\nError: Selection index out of bounds.");
                return;
            }
            if (b.Sell())
            {
                Log.Add("Purchased: " + b.Title);
                Console.WriteLine($"Order processed! Thank you for buying {b.Title}.");
            }
            else
            {
                Console.WriteLine("\nSold Out: This item is currently out of stock.");
            }
        }

        private void ShowHistory()
        {
            Console.WriteLine("\n--- PERSONAL BILL HISTORY ---");
            if (Log.Entries.Count == 0)
            {
                Console.WriteLine("Your personal receipt log is empty.");
                return;
            }
            for (int i = 0; i < Log.Entries.Count; i++)
                Console.WriteLine($"{i + 1}. {Log.Entries[i]}");
        }

        private void CheckAvailability()
        {
            int target = Input.ReadInt("Enter item index to inspect: ") - 1;
            Book b = Inventory.GetByIndex(target);
            if (b == null)
                Console.WriteLine("\nError: Selection index out of bounds.");
            else
                Console.WriteLine($"\nItem Checked: {b.Title}\nWarehouse Availability: {b.Stock} units remaining");
        }

        private void ShowDeal(string label, Book b)
        {
            if (b == null)
                Console.WriteLine("\nOur store inventory is completely bare.");
            else
                Console.WriteLine($"\n{label}: {b.Title} by {b.Author} (Rs. {b.Price})");
        }

        private void BudgetFilter()
        {
            decimal max = Input.ReadDecimal("Set Max Budget Value (Rs.): ");
            var list = Inventory.UnderBudget(max);
            Console.WriteLine($"\n--- FILTERED RESULTS UNDER RS. {max} ---");
            if (list.Count == 0) Console.WriteLine("No matches fit your input criteria parameters.");
            foreach (Book b in list)
                Console.WriteLine($"* {b.Title} (Rs. {b.Price})");
        }
    }
}


namespace BookShop
{
    using BookShop.Data;
    using BookShop.Services;
    using BookShop.UI;

    public class Program
    {
        public static void Main()
        {
            
            BookRepository repo = new BookRepository("inventory.txt");
            var inventory = new InventoryService(repo);
            var log = new ActionLog();

           
            Menu admin = new AdminMenu(inventory, log);
            Menu customer = new CustomerMenu(inventory, new ActionLog());

            int profile = 0;
            while (profile != 3)
            {
                Console.WriteLine("\n MAIN MENU");
                Console.WriteLine("1. Admin Menu");
                Console.WriteLine("2. Client Menu");
                Console.WriteLine("3. log out");
                profile = Input.ReadInt("Selection ID: ");

                if (profile == 1) admin.Run();
                else if (profile == 2) customer.Run();
            }

            inventory.Save();
            Console.WriteLine("Closing menu");
        }
    }
}
