using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookHiveStudentModule.Pages
{
    public class BookItem
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string PublishedYear { get; set; } = "—";
        public string Category { get; set; } = "—";
        public string Status { get; set; } = "Available";
        public string Qty { get; set; } = "—";
        public string Location { get; set; } = "—";
        public string Synopsis { get; set; } = "";
    }

    public class BookViewingModel : PageModel
    {
        public List<BookItem> Books { get; set; } = new();

        public void OnGet()
        {
            Books = new List<BookItem>
            {
                new BookItem
                {
                    Title = "The Great Gatsby",
                    Author = "F. Scott Fitzgerald",
                    PublishedYear = "1925",
                    Category = "Fiction",
                    Status = "Available",
                    Qty = "3",
                    Location = "Aisle 1 / Shelf A",
                    Synopsis = "A story of wealth, love, and the American Dream in the 1920s."
                },
                new BookItem
                {
                    Title = "A Brief History of Time",
                    Author = "Stephen Hawking",
                    PublishedYear = "1988",
                    Category = "Science",
                    Status = "Available",
                    Qty = "2",
                    Location = "Aisle 3 / Shelf B",
                    Synopsis = "An exploration of cosmology, black holes, and the nature of time."
                },
                new BookItem
                {
                    Title = "Sapiens",
                    Author = "Yuval Noah Harari",
                    PublishedYear = "2011",
                    Category = "History",
                    Status = "Not Available",
                    Qty = "0",
                    Location = "Aisle 2 / Shelf C",
                    Synopsis = "A narrative of humankind from the Stone Age to the modern era."
                },
                new BookItem
                {
                    Title = "Clean Code",
                    Author = "Robert C. Martin",
                    PublishedYear = "2008",
                    Category = "Technology",
                    Status = "Available",
                    Qty = "1",
                    Location = "Aisle 4 / Shelf D",
                    Synopsis = "Best practices and principles for writing readable, maintainable code."
                },

            };
        }
    }
}
