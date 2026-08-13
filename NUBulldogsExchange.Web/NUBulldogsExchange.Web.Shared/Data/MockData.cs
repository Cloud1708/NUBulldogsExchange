namespace NUBulldogsExchange.Web.Shared.Data;

public static class MockData
{
    public const string PlaceholderImage =
        "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22800%22 height=%22800%22 viewBox=%220 0 800 800%22%3E%3Crect fill=%22%23F1F4F8%22 width=%22800%22 height=%22800%22/%3E%3Crect x=%22280%22 y=%22280%22 width=%22240%22 height=%22240%22 rx=%2224%22 fill=%22%23123A63%22/%3E%3Ctext x=%22400%22 y=%22420%22 text-anchor=%22middle%22 fill=%22%23F9C424%22 font-family=%22Arial%22 font-size=%2248%22 font-weight=%22700%22%3ENU%3C/text%3E%3C/svg%3E";

    public static readonly List<CategoryItem> Categories =
    [
        new() { Name = "T-Shirts", ItemCount = "12 items", Count = 12, Slug = "t-shirts", ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Polo Shirts", ItemCount = "8 items", Count = 8, Slug = "polo-shirts", ImageUrl = "https://images.unsplash.com/photo-1627225924765-552d49cf47ad?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Hoodies", ItemCount = "6 items", Count = 6, Slug = "hoodies", ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f95609a7?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Jackets", ItemCount = "5 items", Count = 5, Slug = "jackets", ImageUrl = "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Caps", ItemCount = "7 items", Count = 7, Slug = "caps", ImageUrl = "https://images.unsplash.com/photo-1588850561407-ed78c282e89b?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Bags", ItemCount = "4 items", Count = 4, Slug = "bags", ImageUrl = "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Tumblers", ItemCount = "3 items", Count = 3, Slug = "tumblers", ImageUrl = "https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "Accessories", ItemCount = "9 items", Count = 9, Slug = "accessories", ImageUrl = "https://images.unsplash.com/photo-1611652022419-a9419f74343d?auto=format&fit=crop&w=400&q=80" },
        new() { Name = "School Supplies", ItemCount = "6 items", Count = 6, Slug = "school-supplies", ImageUrl = "https://images.unsplash.com/photo-1531346878377-a5be20888e57?auto=format&fit=crop&w=400&q=80" },
    ];

    public static readonly string[] ApparelSizes = ["XS", "S", "M", "L", "XL", "XXL"];
    public static readonly string[] OneSize = ["One Size"];

    public static readonly List<Product> Products =
    [
        new()
        {
            Id = 1,
            Name = "NU Bulldogs Lanyard",
            Category = "Accessories",
            Section = "accessories",
            Price = 149,
            Rating = 4.3,
            Reviews = 167,
            Sold = 891,
            Stock = 200,
            Colors = ["navy", "gold"],
            Material = "Polyester",
            Sku = "NUBE-AC-001",
            IsFeatured = true,
            ImageUrl = "https://images.unsplash.com/photo-1611652022419-a9419f74343d?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1611652022419-a9419f74343d?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1611652022419-a9419f74343d?auto=format&fit=crop&w=800&h=800&q=85&crop=entropy",
                "https://images.unsplash.com/photo-1611652022419-a9419f74343d?auto=format&fit=crop&w=800&h=800&q=90&crop=edges"
            ],
            Description = "Polyester lanyard with safety breakaway clasp. Features NU Bulldogs logo print and includes a detachable ID card holder. A campus essential.",
            FullDescription = "Polyester lanyard with safety breakaway clasp. Features NU Bulldogs logo print and includes a detachable ID card holder. A campus essential designed for everyday student use — from lectures to org events.",
            Features =
            [
                "Premium university-grade materials",
                "Official NU Bulldogs branding",
                "Comfortable everyday wear",
                "Safety breakaway clasp",
                "Detachable ID card holder included"
            ],
            RatingBreakdown = [40, 40, 12, 5, 3]
        },
        new()
        {
            Id = 2,
            Name = "NU Bulldogs Cap",
            Category = "Caps",
            Section = "accessories",
            Price = 299,
            Rating = 5.0,
            Reviews = 201,
            Sold = 543,
            Stock = 85,
            Badge = "Best Seller",
            Colors = ["navy", "black", "white"],
            Material = "Cotton Twill",
            Sku = "NUBE-CP-002",
            IsFeatured = true,
            IsBestSeller = true,
            IsFavorite = true,
            ImageUrl = "https://images.unsplash.com/photo-1588850561407-ed78c282e89b?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1588850561407-ed78c282e89b?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1575428652377-a2d80e2277fc?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1521369909029-2afed882baee?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Structured NU Bulldogs cap with embroidered logo. Adjustable fit for everyday campus wear.",
            FullDescription = "Structured NU Bulldogs cap with embroidered logo and an adjustable strap. Built for sunny campus walks, game-day energy, and everyday Bulldog pride.",
            Features =
            [
                "Embroidered NU Bulldogs logo",
                "Adjustable strap fit",
                "Breathable cotton twill",
                "Structured crown",
                "Suitable for campus events and daily use"
            ],
            RatingBreakdown = [72, 18, 6, 3, 1]
        },
        new()
        {
            Id = 3,
            Name = "NU Bulldogs Classic Shirt",
            Category = "T-Shirts",
            Section = "apparel",
            Price = 499,
            Rating = 5.0,
            Reviews = 124,
            Sold = 342,
            Stock = 140,
            Badge = "Best Seller",
            Colors = ["navy", "white", "black", "gold"],
            Material = "Cotton Blend",
            Sku = "NUBE-TS-001",
            IsFeatured = true,
            IsBestSeller = true,
            IsFavorite = true,
            IsFreshDrop = true,
            IsNewArrival = true,
            ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1583743814966-8936f5b7be1a?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1562157873-818bc0726f68?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Soft cotton classic tee with bold Bulldog pride branding. A wardrobe essential for every NU supporter.",
            FullDescription = "Soft cotton classic tee with bold Bulldog pride branding. Lightweight, breathable, and built for class days, org meetings, and weekend hangouts.",
            Features =
            [
                "Premium university-grade materials",
                "Official NU Bulldogs branding",
                "Comfortable everyday wear",
                "Machine washable",
                "Suitable for campus events and daily use"
            ],
            RatingBreakdown = [68, 22, 7, 2, 1]
        },
        new()
        {
            Id = 4,
            Name = "NU Bulldogs Notebook",
            Category = "School Supplies",
            Section = "essentials",
            Price = 129,
            Rating = 4.5,
            Reviews = 55,
            Sold = 310,
            Stock = 320,
            Colors = ["navy", "white"],
            Material = "Paper / Cardstock",
            Sku = "NUBE-SS-004",
            IsFeatured = true,
            ImageUrl = "https://images.unsplash.com/photo-1531346878377-a5be20888e57?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1531346878377-a5be20888e57?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1517842645767-c639042777db?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Lined notebook with NU Bulldogs cover art. Perfect for lectures, notes, and campus planning.",
            FullDescription = "Lined notebook with NU Bulldogs cover art and sturdy binding. Perfect for lectures, project notes, and staying organized through the semester.",
            Features =
            [
                "College-ruled lined pages",
                "Official NU Bulldogs cover design",
                "Durable binding",
                "Compact campus-ready size",
                "Ideal for lectures and planning"
            ],
            RatingBreakdown = [48, 32, 12, 5, 3]
        },
        new()
        {
            Id = 5,
            Name = "NU Bulldogs Tumbler",
            Category = "Tumblers",
            Section = "essentials",
            Price = 349,
            Rating = 4.8,
            Reviews = 78,
            Sold = 215,
            Stock = 96,
            Colors = ["navy", "gold", "white"],
            Material = "Stainless Steel",
            Sku = "NUBE-TM-005",
            IsFeatured = true,
            IsFreshDrop = true,
            IsNewArrival = true,
            ImageUrl = "https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1577937927133-66ef06acdf18?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1610824352934-c10d87b700cc?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Insulated tumbler that keeps drinks cold or hot through long campus days.",
            FullDescription = "Double-wall insulated tumbler that keeps drinks cold or hot through long campus days. Features leak-resistant lid and official NU Bulldogs branding.",
            Features =
            [
                "Double-wall insulation",
                "Official NU Bulldogs branding",
                "Leak-resistant lid",
                "Fits most cup holders",
                "Easy to clean"
            ],
            RatingBreakdown = [62, 26, 8, 3, 1]
        },
        new()
        {
            Id = 6,
            Name = "NU Bulldogs Varsity Hoodie",
            Category = "Hoodies",
            Section = "apparel",
            Price = 999,
            OriginalPrice = 1299,
            Rating = 4.9,
            Reviews = 89,
            Sold = 201,
            Stock = 48,
            Badge = "Sale",
            Colors = ["navy", "black"],
            Material = "Fleece Cotton Blend",
            Sku = "NUBE-HD-006",
            IsFeatured = true,
            IsBestSeller = true,
            IsFavorite = true,
            ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f95609a7?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1556821840-3a63f95609a7?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1578768079052-aa76e505fd27?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1620799140408-edc6dcb6d633?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Cozy varsity hoodie with premium fleece lining and NU Bulldogs crest detailing.",
            FullDescription = "Cozy varsity hoodie with premium fleece lining and NU Bulldogs crest detailing. Soft, warm, and made for late-night study sessions and cool campus mornings.",
            Features =
            [
                "Premium fleece lining",
                "Official NU Bulldogs crest",
                "Kangaroo pocket",
                "Ribbed cuffs and hem",
                "Machine washable"
            ],
            RatingBreakdown = [70, 20, 6, 3, 1]
        },
        new()
        {
            Id = 7,
            Name = "NU Bulldogs Premium Polo",
            Category = "Polo Shirts",
            Section = "apparel",
            Price = 699,
            Rating = 4.7,
            Reviews = 56,
            Sold = 178,
            Stock = 72,
            Badge = "New",
            Colors = ["navy", "white", "gold"],
            Material = "Pique Cotton",
            Sku = "NUBE-PL-007",
            IsFeatured = true,
            IsNewArrival = true,
            IsFreshDrop = true,
            IsFavorite = true,
            ImageUrl = "https://images.unsplash.com/photo-1627225924765-552d49cf47ad?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1627225924765-552d49cf47ad?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1586790170083-2f9ceadc561d?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1618354691373-d851c5c3a990?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Breathable premium polo with subtle NU embroidery. Ideal for class, events, and casual Fridays.",
            FullDescription = "Breathable premium polo with subtle NU embroidery. A polished everyday option for class presentations, org events, and casual Fridays.",
            Features =
            [
                "Breathable pique cotton",
                "Subtle NU embroidery",
                "Classic collar fit",
                "Comfortable everyday wear",
                "Machine washable"
            ],
            RatingBreakdown = [55, 30, 10, 3, 2]
        },
        new()
        {
            Id = 8,
            Name = "NU Bulldogs Tote Bag",
            Category = "Bags",
            Section = "accessories",
            Price = 249,
            Rating = 4.6,
            Reviews = 43,
            Sold = 132,
            Stock = 110,
            Badge = "New",
            Colors = ["navy", "white"],
            Material = "Canvas",
            Sku = "NUBE-BG-008",
            IsFeatured = true,
            IsNewArrival = true,
            IsFreshDrop = true,
            ImageUrl = "https://images.unsplash.com/photo-1590874103328-eac38a67478a?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1590874103328-eac38a67478a?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1544816155-12df9643f363?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1622560480605-d83c853bc5c3?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Lightweight canvas tote for books, gadgets, and everyday campus essentials.",
            FullDescription = "Lightweight canvas tote for books, gadgets, and everyday campus essentials. Spacious main compartment with reinforced handles and NU Bulldogs print.",
            Features =
            [
                "Durable canvas construction",
                "Official NU Bulldogs print",
                "Reinforced handles",
                "Spacious main compartment",
                "Lightweight everyday carry"
            ],
            RatingBreakdown = [50, 30, 12, 5, 3]
        },
        new()
        {
            Id = 9,
            Name = "NU Bulldogs Campus Jacket",
            Category = "Jackets",
            Section = "apparel",
            Price = 1499,
            Rating = 4.8,
            Reviews = 67,
            Sold = 98,
            Stock = 5,
            Badge = "New",
            Colors = ["navy", "black"],
            Material = "Polyester Shell",
            Sku = "NUBE-JK-009",
            IsNewArrival = true,
            ImageUrl = "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1544022613-e87ca75a784a?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1591047139829-d91aecb6caea?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Weather-ready campus jacket with NU branding and a clean athletic silhouette.",
            FullDescription = "Weather-ready campus jacket with NU branding and a clean athletic silhouette. Layer it over hoodies or tees for cool mornings between buildings.",
            Features =
            [
                "Weather-ready shell",
                "Official NU branding",
                "Clean athletic silhouette",
                "Zip front closure",
                "Suitable for campus commute"
            ],
            RatingBreakdown = [60, 25, 10, 3, 2]
        },
        new()
        {
            Id = 10,
            Name = "NU Bulldogs Backpack",
            Category = "Bags",
            Section = "essentials",
            Price = 899,
            OriginalPrice = 1099,
            Rating = 4.9,
            Reviews = 112,
            Sold = 256,
            Stock = 64,
            Badge = "Sale",
            Colors = ["navy", "black"],
            Material = "Ballistic Nylon",
            Sku = "NUBE-BP-010",
            IsBestSeller = true,
            ImageUrl = "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1581605405669-fcdf81165afa?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1622560480654-d96214fdc887?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Spacious backpack with padded laptop sleeve and signature Bulldogs detailing.",
            FullDescription = "Spacious backpack with padded laptop sleeve and signature Bulldogs detailing. Built to carry books, gadgets, and everything you need between classes.",
            Features =
            [
                "Padded laptop sleeve",
                "Official Bulldogs detailing",
                "Multiple organizer pockets",
                "Padded shoulder straps",
                "Durable everyday construction"
            ],
            RatingBreakdown = [66, 24, 7, 2, 1]
        },
        new()
        {
            Id = 11,
            Name = "NU Bulldogs Pen Set",
            Category = "School Supplies",
            Section = "essentials",
            Price = 99,
            Rating = 4.3,
            Reviews = 34,
            Sold = 420,
            Stock = 0,
            InStock = false,
            Colors = ["navy", "gold"],
            Material = "Plastic / Metal Accents",
            Sku = "NUBE-SS-011",
            ImageUrl = "https://images.unsplash.com/photo-1583485088034-697b0256157d?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1583485088034-697b0256157d?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1563089145-599997674d42?auto=format&fit=crop&w=800&q=85"
            ],
            Description = "Smooth-writing pen set with NU accents — a practical desk essential.",
            FullDescription = "Smooth-writing pen set with NU accents. A practical desk essential for exams, note-taking, and signing org forms.",
            Features =
            [
                "Smooth-writing ink",
                "Official NU accents",
                "Includes multiple pens",
                "Comfortable grip",
                "Campus desk essential"
            ],
            RatingBreakdown = [42, 35, 15, 5, 3]
        },
        new()
        {
            Id = 12,
            Name = "NU Bulldogs Keychain",
            Category = "Accessories",
            Section = "accessories",
            Price = 79,
            Rating = 4.4,
            Reviews = 88,
            Sold = 640,
            Stock = 250,
            Badge = "Best Seller",
            Colors = ["navy", "gold"],
            Material = "Metal Alloy",
            Sku = "NUBE-AC-012",
            IsBestSeller = true,
            ImageUrl = "https://images.unsplash.com/photo-1606760227091-3dd870d97f1d?auto=format&fit=crop&w=800&q=85",
            Images =
            [
                "https://images.unsplash.com/photo-1606760227091-3dd870d97f1d?auto=format&fit=crop&w=800&q=85",
                "https://images.unsplash.com/photo-1606760227091-3dd870d97f1d?auto=format&fit=crop&w=800&h=800&q=90&crop=entropy"
            ],
            Description = "Compact metal keychain featuring the NU Bulldogs mark.",
            FullDescription = "Compact metal keychain featuring the NU Bulldogs mark. A small, durable accessory that shows school pride on every keyring.",
            Features =
            [
                "Durable metal alloy",
                "Official NU Bulldogs mark",
                "Compact everyday carry",
                "Secure split ring",
                "Great gift for Bulldogs"
            ],
            RatingBreakdown = [45, 35, 12, 5, 3]
        }
    ];

    public static Product? GetById(int id) => Products.FirstOrDefault(p => p.Id == id);

    public static IEnumerable<Product> Search(string? query) => Search(Products, query);

    public static IEnumerable<Product> Search(IEnumerable<Product> source, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return source;

        var normalized = query.Trim();
        return source.Where(p => MatchesSearch(p, normalized));
    }

    public static bool MatchesSearch(Product product, string query)
    {
        if (product.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(product.Description)
            && product.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(product.Section)
            && product.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
            && query.Length >= 4)
            return true;

        foreach (var word in product.Category.Split([' ', '-', '/', '&'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return true;

            if (query.Length >= 4 && word.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static IEnumerable<Product> GetRelated(Product product, int take = 4)
    {
        var related = Products
            .Where(p => p.Id != product.Id &&
                        (p.Category == product.Category || p.Section == product.Section))
            .Take(take)
            .ToList();

        if (related.Count >= take)
            return related;

        var fillers = Products
            .Where(p => p.Id != product.Id &&
                        related.All(r => r.Id != p.Id) &&
                        (p.IsFeatured || p.IsBestSeller || p.IsNewArrival))
            .OrderByDescending(p => p.Sold)
            .Take(take - related.Count);

        related.AddRange(fillers);
        return related;
    }

    public static string FormatColorName(string color) =>
        string.IsNullOrWhiteSpace(color)
            ? color
            : char.ToUpperInvariant(color[0]) + color[1..].ToLowerInvariant();

    public static IEnumerable<Product> Featured => Products.Where(p => p.IsFeatured).Take(8);
    public static IEnumerable<Product> FreshDrops => Products.Where(p => p.IsFreshDrop).Take(4);
    public static IEnumerable<Product> Favorites => Products.Where(p => p.IsFavorite).Take(4);
    public static IEnumerable<Product> BestSellers => Products.Where(p => p.IsBestSeller || p.Badge == "Best Seller");
    public static IEnumerable<Product> NewArrivals => Products.Where(p => p.IsNewArrival || p.Badge == "New" || p.IsFreshDrop);
    public static IEnumerable<Product> Apparel => Products.Where(p =>
        p.Section == "apparel" ||
        p.Category is "T-Shirts" or "Polo Shirts" or "Hoodies" or "Jackets");
    public static IEnumerable<Product> Accessories => Products.Where(p =>
        p.Section == "accessories" ||
        p.Category is "Accessories" or "Caps" or "Bags");
    public static IEnumerable<Product> Essentials => Products.Where(p =>
        p.Section == "essentials" ||
        p.Category is "School Supplies" or "Tumblers" or "Bags");

    static MockData()
    {
        foreach (var product in Products)
        {
            if (product.Sizes.Count == 0)
            {
                product.Sizes = product.Section == "apparel"
                    ? [.. ApparelSizes]
                    : [.. OneSize];
            }

            if (product.Images.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
                product.Images = [product.ImageUrl];

            if (string.IsNullOrWhiteSpace(product.FullDescription))
                product.FullDescription = product.Description;

            if (product.Features.Count == 0)
            {
                product.Features =
                [
                    "Premium university-grade materials",
                    "Official NU Bulldogs branding",
                    "Comfortable everyday wear",
                    "Machine washable",
                    "Suitable for campus events and daily use"
                ];
            }

            if (product.ProductReviews.Count == 0)
                product.ProductReviews = CreateDefaultReviews(product);

            if (product.Stock <= 0 && !product.InStock)
                product.Stock = 0;
            else if (product.Stock <= 0)
                product.Stock = 100;

            product.InStock = product.Stock > 0;
        }
    }

    private static List<ProductReview> CreateDefaultReviews(Product product)
    {
        var baseDate = new DateTime(2026, 8, 3);
        return
        [
            new ProductReview
            {
                Author = "Maria S.",
                Initials = "M",
                Rating = 5,
                Date = baseDate,
                Comment = $"Absolutely love this! The quality is amazing and the fit is perfect. Definitely worth every peso. The {product.Name} exceeded my expectations."
            },
            new ProductReview
            {
                Author = "Juan D.",
                Initials = "J",
                Rating = 5,
                Date = baseDate.AddDays(-6),
                Comment = "Great product. The NU logo looks crisp and the material is comfortable. Already bought two."
            },
            new ProductReview
            {
                Author = "Ana R.",
                Initials = "A",
                Rating = 4,
                Date = baseDate.AddDays(-14),
                Comment = "Nice quality. Color is slightly different from the photo but still looks great."
            }
        ];
    }
}
