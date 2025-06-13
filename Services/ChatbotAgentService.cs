using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace NhaNghiYenNhi.Services
{
    public class ChatbotAgentService : IChatbotAgentService
    {
        private readonly MyDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IActionService _actionService;
        private readonly string _groqApiKey;
        private readonly string _groqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

        public ChatbotAgentService(MyDbContext context, IHttpClientFactory httpClientFactory, IActionService actionService, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _actionService = actionService;
            _groqApiKey = configuration["GroqApiKey"] ?? "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";
        }

        public async Task<string> ProcessUserMessageAsync(string userMessage)
        {
            try
            {
                // Phân tích intent và thực hiện action nếu cần
                var actionResult = await AnalyzeAndExecuteActionAsync(userMessage);
                
                if (actionResult != null)
                {
                    // Nếu có action được thực hiện, trả về kết quả với icon phù hợp
                    var icon = actionResult.Success ? "✅" : "❌";
                    var prefix = actionResult.Success ? "Đã thực hiện:" : "Lỗi:";
                    return $"{icon} **{prefix}** {actionResult.Message}";
                }
                
                // Nếu không có action, trả lời bình thường với AI
                var contextData = await GatherContextDataAsync();
                var systemPrompt = CreateSystemPrompt(contextData);
                var response = await CallGroqApiAsync(systemPrompt, userMessage);
                
                return response;
            }
            catch (Exception ex)
            {
                return $"❌ **Lỗi hệ thống:** Em gặp lỗi khi xử lý yêu cầu của Cô Chủ: {ex.Message}";
            }
        }

        private async Task<ActionResult?> AnalyzeAndExecuteActionAsync(string userMessage)
        {
            var message = userMessage.ToLower().Trim();
            
            // Debug: Log message để kiểm tra
            Console.WriteLine($"🔍 Analyzing message: '{message}'");
            
            // Sử dụng LLM để phân tích intent và trích xuất thông tin
            var intentResult = await AnalyzeIntentWithLLM(userMessage);
            
            if (intentResult != null)
            {
                Console.WriteLine($"🤖 LLM Intent: {intentResult.Intent}, Room: {intentResult.RoomNumber}, Details: {intentResult.Details}");
                
                switch (intentResult.Intent.ToLower())
                {
                    case "book_room":
                        if (intentResult.RoomNumber.HasValue)
                        {
                            var customerName = !string.IsNullOrEmpty(intentResult.CustomerName) ? intentResult.CustomerName : "Khách vãng lai";
                            return await _actionService.BookRoomAsync(intentResult.RoomNumber.Value, customerName);
                        }
                        break;
                        
                    case "add_product":
                        if (intentResult.RoomNumber.HasValue && intentResult.Products?.Any() == true)
                        {
                            if (intentResult.Products.Count == 1)
                            {
                                var product = intentResult.Products.First();
                                return await _actionService.AddProductToRoomAsync(intentResult.RoomNumber.Value, product.Name, product.Quantity);
                            }
                            else
                            {
                                // Multiple products
                                return await ProcessMultipleProductsFromLLM(intentResult.RoomNumber.Value, intentResult.Products);
                            }
                        }
                        break;
                        
                    case "checkout_room":
                        if (intentResult.RoomNumber.HasValue)
                        {
                            return await _actionService.CheckoutRoomAsync(intentResult.RoomNumber.Value);
                        }
                        break;
                        
                    case "clean_room":
                        if (intentResult.RoomNumber.HasValue)
                        {
                            return await _actionService.CleanRoomAsync(intentResult.RoomNumber.Value);
                        }
                        break;
                        
                    case "check_status":
                        return await _actionService.GetRoomStatusAsync(intentResult.RoomNumber);
                        
                    case "find_product":
                        var searchTerm = !string.IsNullOrEmpty(intentResult.ProductName) ? intentResult.ProductName : "";
                        return await _actionService.FindProductAsync(searchTerm);
                }
            }

            Console.WriteLine($"❌ LLM failed to parse intent, falling back to regex patterns...");
            
            // Fallback: Sử dụng regex patterns nếu LLM không hoạt động
            return await FallbackToRegexPatterns(message);
        }

        private async Task<IntentResult?> AnalyzeIntentWithLLM(string userMessage)
        {
            try
            {
                // Lấy context về sản phẩm có sẵn
                var products = await _context.SanPhamNhaNghis.ToListAsync();
                var productList = string.Join(", ", products.Select(p => p.TenSanPham));
                
                var systemPrompt = $@"Bạn là AI phân tích ý định của tin nhắn trong hệ thống quản lý nhà nghỉ.

DANH SÁCH SẢN PHẨM CÓ SẴN: {productList}

Phân tích tin nhắn và trả về JSON với format sau:
{{
    ""intent"": ""book_room|add_product|checkout_room|clean_room|check_status|find_product|unknown"",
    ""room_number"": số phòng (nếu có),
    ""customer_name"": tên khách (nếu có),
    ""product_name"": tên sản phẩm chính (nếu có),
    ""products"": [
        {{""name"": ""tên sản phẩm"", ""quantity"": số lượng}},
        ...
    ],
    ""details"": ""thông tin bổ sung""
}}

QUY TẮC:
1. Tên sản phẩm phải map chính xác với danh sách có sẵn
2. Nếu user nói ""bò húc"", ""sting"", ""mì gói"" thì map với tên chính xác từ danh sách
3. Số lượng mặc định là 1 nếu không nói rõ
4. QUAN TRỌNG: Chỉ trả về JSON thuần túy, KHÔNG có markdown code blocks (```json), KHÔNG có text khác
5. Nếu có nhiều sản phẩm (""và"", "","") thì tách ra thành array

EXAMPLES:
- ""phòng 1 mua 2 bò húc"" → {{""intent"": ""add_product"", ""room_number"": 1, ""products"": [{{""name"": ""Bò Húc"", ""quantity"": 2}}]}}
- ""số 2 trả phòng"" → {{""intent"": ""checkout_room"", ""room_number"": 2}}
- ""đặt phòng số 3 cho Nguyễn Văn A"" → {{""intent"": ""book_room"", ""room_number"": 3, ""customer_name"": ""Nguyễn Văn A""}}";

                var response = await CallGroqApiAsync(systemPrompt, userMessage);
                
                // Parse JSON response - handle markdown code blocks
                try
                {
                    // Clean response: remove markdown code blocks if present
                    var cleanResponse = response.Trim();
                    if (cleanResponse.StartsWith("```json"))
                    {
                        cleanResponse = cleanResponse.Substring(7); // Remove "```json"
                    }
                    if (cleanResponse.StartsWith("```"))
                    {
                        cleanResponse = cleanResponse.Substring(3); // Remove "```"
                    }
                    if (cleanResponse.EndsWith("```"))
                    {
                        cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3); // Remove ending "```"
                    }
                    cleanResponse = cleanResponse.Trim();
                    
                    Console.WriteLine($"🧹 Cleaned JSON: {cleanResponse}");
                    
                    var intentResult = System.Text.Json.JsonSerializer.Deserialize<IntentResult>(cleanResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return intentResult;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to parse LLM response: {ex.Message}");
                    Console.WriteLine($"🔍 LLM Response: {response}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LLM Intent Analysis Error: {ex.Message}");
                return null;
            }
        }

        private async Task<ActionResult?> FallbackToRegexPatterns(string message)
        {
            Console.WriteLine($"🔄 Using regex fallback for: '{message}'");
            
            // Pattern cho đặt phòng - cải thiện với nhiều patterns
            var bookPatterns = new[]
            {
                @"(?:đặt|book|thuê)\s*phòng\s*(?:số\s*)?(\d+)(?:\s+(?:cho\s+)?(.+))?",
                @"(?:số\s*)?(\d+)\s*(?:đặt|book|thuê)\s*(?:phòng)?(?:\s+(?:cho\s+)?(.+))?",
                @"(?:có\s*khách|khách)\s*(?:đặt|thuê)\s*phòng\s*(?:số\s*)?(\d+)(?:\s+(.+))?",
                @"phòng\s*(?:số\s*)?(\d+)\s*(?:đặt|thuê|book)(?:\s+(.+))?",
                @"chuyển\s*(?:số\s*)?(\d+)\s*(?:thành|sang)\s*(?:trạng\s*thái\s*)?(?:đã\s*)?(?:thuê|đặt)",
                @"(\d+)\s*(?:thuê|đặt)\s*(?:phòng)?\s*(.+)?"
            };

            foreach (var pattern in bookPatterns)
            {
                var bookMatch = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                Console.WriteLine($"🔍 Testing book room pattern: {pattern} against '{message}'");
                if (bookMatch.Success)
                {
                    Console.WriteLine($"✅ Book room pattern matched: {pattern}");
                    var roomNumber = int.Parse(bookMatch.Groups[1].Value);
                    var customerName = bookMatch.Groups[2].Success ? bookMatch.Groups[2].Value.Trim() : "Khách vãng lai";
                    return await _actionService.BookRoomAsync(roomNumber, customerName);
                }
            }

            // Pattern cho thêm sản phẩm - cải thiện để dễ match hơn
            var addProductPatterns = new[]
            {
                @"phòng\s*(?:số\s*)?(\d+)\s+(?:mua|order|đặt|gọi)\s+(.+?)(?:\s+(\d+)\s*(?:cái|chai|lon|gói|suất|ly)?)?$",
                @"(?:mua|order|đặt|gọi)\s+(.+?)\s+(?:cho\s+)?phòng\s*(?:số\s*)?(\d+)(?:\s+(\d+)\s*(?:cái|chai|lon|gói|suất|ly)?)?",
                @"phòng\s*(\d+)\s*:\s*(.+?)(?:\s+(\d+)\s*(?:cái|chai|lon|gói|suất|ly)?)?$",
                @"(?:số\s*)?(\d+)\s+(?:mua|order|đặt|gọi)\s+(.+?)(?:\s+(\d+)\s*(?:cái|chai|lon|gói|suất|ly)?)?$",
                @"(?:số\s*)?(\d+)\s+(?:mua|order|đặt|gọi|lấy)\s+(.+)",
                @"(?:phòng\s*)?(?:số\s*)?(\d+)\s+(?:cần|muốn|lấy|dùng)\s+(.+)"
            };

            foreach (var pattern in addProductPatterns)
            {
                var productMatch = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                Console.WriteLine($"🔍 Testing add product pattern: {pattern} against '{message}'");
                if (productMatch.Success)
                {
                    Console.WriteLine($"✅ Add product pattern matched: {pattern}");
                    
                    int roomNumber;
                    string productName;
                    int quantity = 1;
                    
                    if (pattern.Contains("cho\\s+phòng")) // Pattern thứ 2: "mua X cho phòng Y"
                    {
                        productName = productMatch.Groups[1].Value.Trim();
                        roomNumber = int.Parse(productMatch.Groups[2].Value);
                        if (productMatch.Groups[3].Success) quantity = int.Parse(productMatch.Groups[3].Value);
                    }
                    else if (pattern.Contains("(?:số\\s*)?\\(\\d+\\)\\s+(?:mua")) // Pattern số X mua Y
                    {
                        roomNumber = int.Parse(productMatch.Groups[1].Value);
                        productName = productMatch.Groups[2].Value.Trim();
                        if (productMatch.Groups[3].Success) quantity = int.Parse(productMatch.Groups[3].Value);
                    }
                    else // Pattern thứ 1 và 3: "phòng X mua Y"
                    {
                        roomNumber = int.Parse(productMatch.Groups[1].Value);
                        productName = productMatch.Groups[2].Value.Trim();
                        if (productMatch.Groups[3].Success) quantity = int.Parse(productMatch.Groups[3].Value);
                    }

                    // Kiểm tra và xử lý sản phẩm với số lượng
                    var (extractedQuantity, cleanName) = ExtractQuantityFromProductName(productName);
                    if (extractedQuantity > 0)
                    {
                        quantity = extractedQuantity;
                        productName = cleanName;
                    }

                    // Clean product name
                    productName = CleanProductName(productName);
                    Console.WriteLine($"🧹 Clean product name: '{productMatch.Groups[2].Value.Trim()}' → '{productName}'");

                    // Kiểm tra multiple products
                    if (ContainsMultipleProducts(productName))
                    {
                        return await ProcessMultipleProducts(roomNumber, productName);
                    }
                    else
                    {
                        return await _actionService.AddProductToRoomAsync(roomNumber, productName, quantity);
                    }
                }
            }

            // Pattern cho trả phòng - cải thiện với nhiều pattern hơn
            var checkoutPatterns = new[]
            {
                @"(?:trả|checkout|check\s*out|thanh\s*toán)\s*phòng\s*(?:số\s*)?(\d+)",
                @"phòng\s*(?:số\s*)?(\d+)\s*(?:trả|checkout|check\s*out|thanh\s*toán)",
                @"(?:số\s*)?(\d+)\s*(?:trả|checkout)\s*(?:phòng)?",
                @"(?:trả|checkout)\s*(?:số\s*)?(\d+)",
                @"(\d+)\s*(?:trả|checkout|check\s*out)\s*(?:phòng)?",
                @"(?:số\s*)?(\d+)\s*(?:trả|checkout|thanh\s*toán)\s*(?:phòng)?"
            };

            foreach (var pattern in checkoutPatterns)
            {
                var checkoutMatch = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                Console.WriteLine($"🔍 Testing checkout pattern: {pattern} against '{message}'");
                if (checkoutMatch.Success)
                {
                    Console.WriteLine($"✅ Checkout pattern matched: {pattern}");
                    var roomNumber = int.Parse(checkoutMatch.Groups[1].Value);
                    return await _actionService.CheckoutRoomAsync(roomNumber);
                }
            }

            // Pattern cho dọn phòng
            var cleanPatterns = new[]
            {
                @"(?:dọn|clean|làm\s*sạch)\s*phòng\s*(?:số\s*)?(\d+)",
                @"phòng\s*(?:số\s*)?(\d+)\s*(?:dọn|clean|làm\s*sạch)",
                @"(?:số\s*)?(\d+)\s*(?:dọn|clean)\s*(?:phòng)?"
            };

            foreach (var pattern in cleanPatterns)
            {
                var cleanMatch = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (cleanMatch.Success)
                {
                    Console.WriteLine($"✅ Clean room pattern matched: {pattern}");
                    var roomNumber = int.Parse(cleanMatch.Groups[1].Value);
                    return await _actionService.CleanRoomAsync(roomNumber);
                }
            }

            // Pattern cho kiểm tra trạng thái
            var statusPatterns = new[]
            {
                @"(?:trạng\s*thái|status|tình\s*trạng)\s*(?:phòng\s*)?(?:số\s*)?(\d+)?",
                @"(?:phòng\s*)?(?:số\s*)?(\d+)\s*(?:như\s*thế\s*nào|thế\s*nào|ra\s*sao)",
                @"(?:kiểm\s*tra|check)\s*(?:phòng\s*)?(?:số\s*)?(\d+)?"
            };

            foreach (var pattern in statusPatterns)
            {
                var statusMatch = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (statusMatch.Success)
                {
                    Console.WriteLine($"✅ Status check pattern matched: {pattern}");
                    var roomNumber = statusMatch.Groups[1].Success ? int.Parse(statusMatch.Groups[1].Value) : (int?)null;
                    return await _actionService.GetRoomStatusAsync(roomNumber);
                }
            }

            // Pattern cho tìm sản phẩm
            if (ContainsProductSearchKeywords(message))
            {
                Console.WriteLine("✅ Product search triggered!");
                var searchTerm = ExtractProductSearchTerm(message);
                return await _actionService.FindProductAsync(searchTerm);
            }

            Console.WriteLine($"❌ No pattern matched for: '{message}'");
            return null;
        }

        private async Task<ActionResult> ProcessMultipleProductsFromLLM(int roomNumber, List<ProductInfo> products)
        {
            var results = new List<string>();
            var totalSuccess = 0;
            var totalFailed = 0;
            
            foreach (var product in products)
            {
                var result = await _actionService.AddProductToRoomAsync(roomNumber, product.Name, product.Quantity);
                
                if (result.Success)
                {
                    totalSuccess++;
                    results.Add($"✅ {product.Quantity} x {product.Name}");
                }
                else
                {
                    totalFailed++;
                    results.Add($"❌ {product.Quantity} x {product.Name} - {result.Message}");
                }
            }
            
            var summary = $"📦 Xử lý {products.Count} sản phẩm cho phòng {roomNumber}:\n" +
                         string.Join("\n", results) +
                         $"\n\n📊 Kết quả: {totalSuccess} thành công, {totalFailed} thất bại";
            
            return new ActionResult 
            { 
                Success = totalSuccess > 0, 
                Message = summary 
            };
        }

        // Classes for LLM response parsing
        private class IntentResult
        {
            public string Intent { get; set; } = "";
            [JsonPropertyName("room_number")]
            public int? RoomNumber { get; set; }
            [JsonPropertyName("customer_name")]
            public string? CustomerName { get; set; }
            [JsonPropertyName("product_name")]
            public string? ProductName { get; set; }
            public List<ProductInfo> Products { get; set; } = new();
            public string? Details { get; set; }
        }

        private class ProductInfo
        {
            public string Name { get; set; } = "";
            public int Quantity { get; set; } = 1;
        }

        private (int Quantity, string CleanName) ExtractQuantityFromProductName(string productName)
        {
            // Trích xuất số lượng từ đầu chuỗi: "2 mì gói" → quantity=2, name="mì gói"
            var quantityPattern = @"^\s*(\d+)\s+(.+)$";
            var match = Regex.Match(productName, quantityPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var quantity = int.Parse(match.Groups[1].Value);
                var cleanName = match.Groups[2].Value.Trim();
                Console.WriteLine($"📊 Extract quantity: '{productName}' → Quantity: {quantity}, Name: '{cleanName}'");
                return (quantity, cleanName);
            }
            
            return (0, productName);
        }

        private bool ContainsMultipleProducts(string productName)
        {
            return productName.Contains(" và ") || productName.Contains(",") || 
                   Regex.IsMatch(productName, @"\d+\s+\w+.*\d+\s+\w+", RegexOptions.IgnoreCase);
        }

        private async Task<ActionResult> ProcessMultipleProducts(int roomNumber, string productText)
        {
            Console.WriteLine($"🔍 Processing multiple products: '{productText}' for room {roomNumber}");
            
            var products = ParseMultipleProducts(productText);
            var results = new List<string>();
            var totalSuccess = 0;
            var totalFailed = 0;
            
            foreach (var (quantity, name) in products)
            {
                var cleanName = CleanProductName(name);
                var result = await _actionService.AddProductToRoomAsync(roomNumber, cleanName, quantity);
                
                if (result.Success)
                {
                    totalSuccess++;
                    results.Add($"✅ {quantity} x {cleanName}");
                }
                else
                {
                    totalFailed++;
                    results.Add($"❌ {quantity} x {cleanName} - {result.Message}");
                }
            }
            
            var summary = $"📦 Xử lý {products.Count} sản phẩm cho phòng {roomNumber}:\n" +
                         string.Join("\n", results) +
                         $"\n\n📊 Kết quả: {totalSuccess} thành công, {totalFailed} thất bại";
            
            return new ActionResult 
            { 
                Success = totalSuccess > 0, 
                Message = summary 
            };
        }

        private List<(int quantity, string name)> ParseMultipleProducts(string productText)
        {
            var products = new List<(int quantity, string name)>();
            
            // Pattern để match: "2 bò húc và 1 mì gói" hoặc "2 bò húc, 1 mì gói"
            var pattern = @"(\d+)\s+([^và,]+)(?:\s*(?:và|,)\s*)?";
            var matches = Regex.Matches(productText, pattern, RegexOptions.IgnoreCase);
            
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var quantity = int.Parse(match.Groups[1].Value);
                    var name = match.Groups[2].Value.Trim();
                    products.Add((quantity, name));
                    Console.WriteLine($"📦 Parsed product: {quantity} x {name}");
                }
            }
            
            // Nếu không parse được, thử fallback
            if (products.Count == 0)
            {
                products.Add((1, productText));
            }
            
            return products;
        }

        private bool ContainsProductSearchKeywords(string message)
        {
            var keywords = new[] { "có gì", "sản phẩm", "menu", "danh sách", "bán gì", "tìm", "search" };
            return keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string ExtractProductSearchTerm(string message)
        {
            // Trích xuất từ khóa tìm kiếm từ câu
            var terms = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var relevantTerms = terms.Where(t => !IsStopWord(t)).ToList();
            
            if (relevantTerms.Count > 2)
            {
                return string.Join(" ", relevantTerms.Skip(1).Take(2));
            }
            
            return relevantTerms.LastOrDefault() ?? "";
        }

        private bool IsStopWord(string word)
        {
            var stopWords = new[] { "có", "gì", "là", "của", "trong", "tìm", "kiếm", "search", "danh", "sách", "menu", "phòng", "số" };
            return stopWords.Contains(word.ToLower());
        }

        private string CleanProductName(string productName)
        {
            // Loại bỏ số lượng và giá tiền nếu có
            // Pattern: "1 - bò húc: 15,000" → "bò húc"
            var cleaned = productName;
            
            // Loại bỏ pattern "số - tên: giá" 
            var pricePattern = @"^\s*\d+\s*-\s*(.+?)\s*:\s*[\d,]+\s*$";
            var priceMatch = Regex.Match(cleaned, pricePattern, RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                cleaned = priceMatch.Groups[1].Value.Trim();
            }
            
            // Loại bỏ pattern "số - tên"
            var numberPattern = @"^\s*\d+\s*-\s*(.+)$";
            var numberMatch = Regex.Match(cleaned, numberPattern, RegexOptions.IgnoreCase);
            if (numberMatch.Success)
            {
                cleaned = numberMatch.Groups[1].Value.Trim();
            }
            
            // Loại bỏ giá tiền ở cuối: "bò húc: 15,000" → "bò húc"
            var endPricePattern = @"^(.+?)\s*:\s*[\d,]+\s*$";
            var endPriceMatch = Regex.Match(cleaned, endPricePattern, RegexOptions.IgnoreCase);
            if (endPriceMatch.Success)
            {
                cleaned = endPriceMatch.Groups[1].Value.Trim();
            }
            
            // Loại bỏ số lượng ở đầu: "2 sting" → "sting" 
            var quantityPattern = @"^\s*\d+\s+(.+)$";
            var quantityMatch = Regex.Match(cleaned, quantityPattern, RegexOptions.IgnoreCase);
            if (quantityMatch.Success)
            {
                cleaned = quantityMatch.Groups[1].Value.Trim();
            }

            // Loại bỏ các từ khóa không cần thiết
            cleaned = cleaned
                .Replace("sản phẩm", "")
                .Replace("có", "")
                .Replace("gì", "")
                .Replace("món", "")
                .Replace("đồ", "")
                .Replace("thức", "")
                .Replace("uống", "")
                .Replace("ăn", "")
                .Trim();

            Console.WriteLine($"🧹 Clean product name: '{productName}' → '{cleaned}'");
            return cleaned;
        }

        private async Task<ContextData> GatherContextDataAsync()
        {
            var currentTime = DateTime.Now;
            
            var phongs = await _context.Phongs
                .Include(p => p.IdLoaiPhongMacDinhNavigation)
                .ToListAsync();

            var loaiPhongs = await _context.LoaiPhongs.ToListAsync();
            var sanPhams = await _context.SanPhamNhaNghis.ToListAsync();

            var thuePhongs = await _context.ThuePhongs
                .Include(tp => tp.IdKhachHangNavigation)
                .Include(tp => tp.IdLoaiPhongNavigation)
                .Where(tp => !tp.TraPhongs.Any())
                .ToListAsync();

            var phongDangThueDetails = new List<PhongDangThueInfo>();
            foreach (var thue in thuePhongs)
            {
                var phong = phongs.FirstOrDefault(p => p.Id == thue.IdPhong);
                if (phong != null && thue.ThoiGianVao.HasValue)
                {
                    var thoiGianThue = currentTime - thue.ThoiGianVao.Value;
                    
                    var sanPhamInfo = "";
                    if (!string.IsNullOrEmpty(thue.SanPhamDaMua) && thue.SanPhamDaMua != "0")
                    {
                        try
                        {
                            var sanPhamDict = JsonConvert.DeserializeObject<Dictionary<int, int>>(thue.SanPhamDaMua);
                            if (sanPhamDict != null && sanPhamDict.Any())
                            {
                                var sanPhamIds = sanPhamDict.Keys.ToList();
                                var sanPhamsInfo = await _context.SanPhamNhaNghis
                                    .Where(sp => sanPhamIds.Contains(sp.Id))
                                    .ToListAsync();
                                    
                                var sanPhamList = sanPhamsInfo.Select(sp => 
                                    $"{sp.TenSanPham} (SL: {sanPhamDict.GetValueOrDefault(sp.Id, 0)})").ToList();
                                sanPhamInfo = string.Join(", ", sanPhamList);
                            }
                        }
                        catch
                        {
                            if (thue.SanPhamDaMua != "0")
                            {
                                var oldIds = thue.SanPhamDaMua.Split('-').Where(s => int.TryParse(s, out _)).Select(int.Parse);
                                var sanPhamsOld = await _context.SanPhamNhaNghis
                                    .Where(sp => oldIds.Contains(sp.Id))
                                    .ToListAsync();
                                sanPhamInfo = string.Join(", ", sanPhamsOld.Select(sp => sp.TenSanPham));
                            }
                        }
                    }

                    phongDangThueDetails.Add(new PhongDangThueInfo
                    {
                        TenPhong = phong.TenPhong,
                        ThoiGianVao = thue.ThoiGianVao.Value,
                        ThoiGianThue = thoiGianThue,
                        KhachHang = thue.IdKhachHangNavigation?.HoTen ?? "Khách vãng lai",
                        LoaiPhong = thue.IdLoaiPhongNavigation?.TenLoai ?? "Không xác định",
                        SanPhamDaMua = sanPhamInfo
                    });
                }
            }

            var thuePhongHomNay = await _context.ThuePhongs
                .Where(tp => tp.ThoiGianVao.HasValue && tp.ThoiGianVao.Value.Date == currentTime.Date)
                .CountAsync();

            var traPhongHomNay = await _context.TraPhongs
                .Where(tp => tp.ThoiGianTra.HasValue && tp.ThoiGianTra.Value.Date == currentTime.Date)
                .CountAsync();

            return new ContextData
            {
                TongSoPhong = phongs.Count,
                PhongTrong = phongs.Count(p => p.TrangThai == 0),
                PhongDangThue = phongs.Count(p => p.TrangThai == 1),
                PhongDangDonDep = phongs.Count(p => p.TrangThai == 3),
                PhongDangThueDetails = phongDangThueDetails,
                ThuePhongHomNay = thuePhongHomNay,
                TraPhongHomNay = traPhongHomNay,
                NgayHienTai = currentTime.ToString("dd/MM/yyyy"),
                GioHienTai = currentTime.ToString("HH:mm"),
                LoaiPhongs = loaiPhongs,
                SanPhams = sanPhams
            };
        }

        private string CreateSystemPrompt(ContextData context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là trợ lý AI AGENT thông minh cho QUẢN LÝ nhà nghỉ Yến Nhi. Cô Chủ đang nói chuyện với em.");
            sb.AppendLine();
            sb.AppendLine("🤖 KHẢ NĂNG ĐẶC BIỆT:");
            sb.AppendLine("Em có thể TỰ ĐỘNG thực hiện các tác vụ khi Cô Chủ yêu cầu:");
            sb.AppendLine("- 'Đặt phòng số 1 cho Nguyễn Văn A' → Em sẽ tự động đặt phòng");
            sb.AppendLine("- 'Phòng 2 mua nước ngọt' → Em sẽ tự động thêm sản phẩm");
            sb.AppendLine("- 'Trả phòng số 3' → Em sẽ tự động checkout và tính tiền");
            sb.AppendLine("- 'Dọn phòng số 4' → Em sẽ cập nhật trạng thái phòng");
            sb.AppendLine("- 'Kiểm tra phòng 5' → Em sẽ báo cáo chi tiết");
            sb.AppendLine();
            sb.AppendLine("THÔNG TIN HIỆN TẠI:");
            sb.AppendLine($"- Ngày giờ: {context.NgayHienTai} {context.GioHienTai}");
            sb.AppendLine($"- Tổng số phòng: {context.TongSoPhong}");
            sb.AppendLine($"- Phòng trống: {context.PhongTrong}");
            sb.AppendLine($"- Phòng đang thuê: {context.PhongDangThue}");
            sb.AppendLine($"- Phòng đang dọn dẹp: {context.PhongDangDonDep}");
            sb.AppendLine($"- Thuê phòng hôm nay: {context.ThuePhongHomNay}");
            sb.AppendLine($"- Trả phòng hôm nay: {context.TraPhongHomNay}");
            sb.AppendLine();
            
            if (context.PhongDangThueDetails.Any())
            {
                sb.AppendLine("CHI TIẾT PHÒNG ĐANG THUÊ:");
                foreach (var phong in context.PhongDangThueDetails)
                {
                    sb.AppendLine($"- {phong.TenPhong}: {phong.KhachHang}, {phong.ThoiGianThue.Hours}h{phong.ThoiGianThue.Minutes}m");
                    if (!string.IsNullOrEmpty(phong.SanPhamDaMua))
                    {
                        sb.AppendLine($"  + Đã mua: {phong.SanPhamDaMua}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("LOẠI PHÒNG & GIÁ:");
            foreach (var loaiPhong in context.LoaiPhongs)
            {
                sb.AppendLine($"- {loaiPhong.TenLoai}: {loaiPhong.GioDau:N0}₫/h đầu, {loaiPhong.GioSau:N0}₫/h sau, {loaiPhong.QuaDem:N0}₫/đêm");
            }

            sb.AppendLine();
            sb.AppendLine("SẢN PHẨM CÓ SẴN:");
            foreach (var sanPham in context.SanPhams)
            {
                var trangThai = sanPham.Con == true ? "✅" : "❌";
                sb.AppendLine($"- {sanPham.TenSanPham}: {sanPham.Gia:N0}₫ {trangThai}");
            }
            
            sb.AppendLine();
            sb.AppendLine("PHONG CÁCH TRẢ LỜI:");
            sb.AppendLine("- Trả lời NGẮN GỌN, dùng emoji phù hợp");
            sb.AppendLine("- Xưng 'Em', gọi 'Cô Chủ'");
            sb.AppendLine("- Khi thực hiện action thành công: dùng ✅");
            sb.AppendLine("- Khi có lỗi: dùng ❌");
            sb.AppendLine("- Chỉ cung cấp thông tin cần thiết");
            
            return sb.ToString();
        }

        private async Task<string> CallGroqApiAsync(string systemPrompt, string userMessage)
        {
            var httpClient = _httpClientFactory.CreateClient();
            
            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.7,
                max_tokens = 1024
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_groqApiKey}");

            var response = await httpClient.PostAsync(_groqApiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Groq API error: {response.StatusCode} - {responseString}");
            }

            dynamic result = JsonConvert.DeserializeObject(responseString);
            return result.choices[0].message.content;
        }

        private class ContextData
        {
            public int TongSoPhong { get; set; }
            public int PhongTrong { get; set; }
            public int PhongDangThue { get; set; }
            public int PhongDangDonDep { get; set; }
            public List<PhongDangThueInfo> PhongDangThueDetails { get; set; } = new();
            public int ThuePhongHomNay { get; set; }
            public int TraPhongHomNay { get; set; }
            public string NgayHienTai { get; set; } = "";
            public string GioHienTai { get; set; } = "";
            public List<LoaiPhong> LoaiPhongs { get; set; } = new();
            public List<SanPhamNhaNghi> SanPhams { get; set; } = new();
        }

        private class PhongDangThueInfo
        {
            public string TenPhong { get; set; } = "";
            public DateTime ThoiGianVao { get; set; }
            public TimeSpan ThoiGianThue { get; set; }
            public string KhachHang { get; set; } = "";
            public string LoaiPhong { get; set; } = "";
            public string SanPhamDaMua { get; set; } = "";
        }
    }
} 