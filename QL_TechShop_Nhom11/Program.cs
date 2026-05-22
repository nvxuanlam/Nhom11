
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

string cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=.;Database=TechShopDB;Trusted_Connection=True;TrustServerCertificate=True;";

app.MapGet("/api/products", async () =>
{
    var list = new List<object>();

    await using var con = new SqlConnection(cs);
    await con.OpenAsync();

    var cmd = new SqlCommand(@"
        SELECT 
            Id,
            Name,
            Description,
            Category,
            Price,
            Stock,
            ImageUrl,
            SpecsJson
        FROM Products
        WHERE IsDeleted = 0
        ORDER BY Id
    ", con);

    await using var rd = await cmd.ExecuteReaderAsync();

    while (await rd.ReadAsync())
    {
        list.Add(new
        {
            id = rd.GetInt32(0),
            name = rd.GetString(1),
            description = rd.IsDBNull(2) ? "" : rd.GetString(2),
            cat = rd.GetString(3),
            price = rd.GetDecimal(4),
            stock = rd.GetInt32(5),

            img = rd.IsDBNull(6) ? "" : rd.GetString(6),
            imageUrl = rd.IsDBNull(6) ? "" : rd.GetString(6),

            specs = JsonSerializer.Deserialize<Dictionary<string, string>>(
        rd.IsDBNull(7) ? "{}" : rd.GetString(7)
    )
        });
    }

    return Results.Ok(list);
});

app.MapPost("/api/products", async (ProductInput p) =>
{
    if (string.IsNullOrWhiteSpace(p.Name)) return Results.BadRequest("Tên sản phẩm không được rỗng");
    if (p.Price <= 0) return Results.BadRequest("Giá phải lớn hơn 0");
    if (p.Stock < 0) return Results.BadRequest("Tồn kho không được âm");
    if (string.IsNullOrWhiteSpace(p.Img)) return Results.BadRequest("Sản phẩm phải có hình ảnh");

    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    var cmd = new SqlCommand("""
        INSERT INTO Products(Name,Description,Category,Price,Stock,ImageUrl,SpecsJson)
        OUTPUT INSERTED.Id
        VALUES(@Name,@Description,@Category,@Price,@Stock,@ImageUrl,@SpecsJson)
        """, con);
    cmd.Parameters.AddWithValue("@Name", p.Name);
    cmd.Parameters.AddWithValue("@Description", (object?)p.Description ?? "");
    cmd.Parameters.AddWithValue("@Category", p.Cat);
    cmd.Parameters.AddWithValue("@Price", p.Price);
    cmd.Parameters.AddWithValue("@Stock", p.Stock);
    cmd.Parameters.AddWithValue("@ImageUrl", p.Img);
    cmd.Parameters.AddWithValue("@SpecsJson", JsonSerializer.Serialize(p.Specs ?? new()));
    int id = (int)await cmd.ExecuteScalarAsync();

    await AddStockHistory(con, id, p.Name, 0, p.Stock, "Admin");
    return Results.Ok(new { id });
});

app.MapPut("/api/products/{id:int}", async (int id, ProductInput p) =>
{
    if (string.IsNullOrWhiteSpace(p.Name)) return Results.BadRequest("Tên sản phẩm không được rỗng");
    if (p.Price <= 0) return Results.BadRequest("Giá phải lớn hơn 0");
    if (p.Stock < 0) return Results.BadRequest("Tồn kho không được âm");
    if (string.IsNullOrWhiteSpace(p.Img)) return Results.BadRequest("Sản phẩm phải có hình ảnh");

    await using var con = new SqlConnection(cs);
    await con.OpenAsync();

    int oldStock = 0;
    var get = new SqlCommand("SELECT Stock FROM Products WHERE Id=@Id AND IsDeleted=0", con);
    get.Parameters.AddWithValue("@Id", id);
    var old = await get.ExecuteScalarAsync();
    if (old is null) return Results.NotFound("Không tìm thấy sản phẩm");
    oldStock = Convert.ToInt32(old);

    var cmd = new SqlCommand("""
        UPDATE Products SET Name=@Name,Description=@Description,Category=@Category,Price=@Price,
        Stock=@Stock,ImageUrl=@ImageUrl,SpecsJson=@SpecsJson WHERE Id=@Id AND IsDeleted=0
        """, con);
    cmd.Parameters.AddWithValue("@Id", id);
    cmd.Parameters.AddWithValue("@Name", p.Name);
    cmd.Parameters.AddWithValue("@Description", (object?)p.Description ?? "");
    cmd.Parameters.AddWithValue("@Category", p.Cat);
    cmd.Parameters.AddWithValue("@Price", p.Price);
    cmd.Parameters.AddWithValue("@Stock", p.Stock);
    cmd.Parameters.AddWithValue("@ImageUrl", p.Img);
    cmd.Parameters.AddWithValue("@SpecsJson", JsonSerializer.Serialize(p.Specs ?? new()));
    await cmd.ExecuteNonQueryAsync();

    await AddStockHistory(con, id, p.Name, oldStock, p.Stock, "Admin");
    return Results.Ok(new { ok = true });
});

app.MapDelete("/api/products/{id:int}", async (int id) =>
{
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();

    var check = new SqlCommand("""
        SELECT COUNT(*) FROM OrderItems oi
        JOIN Orders o ON oi.OrderId=o.Id
        WHERE oi.ProductId=@Id AND o.Status<>N'Đã giao'
        """, con);
    check.Parameters.AddWithValue("@Id", id);
    int count = (int)await check.ExecuteScalarAsync();
    if (count > 0) return Results.Conflict("Không thể xóa sản phẩm đang có trong đơn hàng chưa giao");

    var cmd = new SqlCommand("UPDATE Products SET IsDeleted=1 WHERE Id=@Id", con);
    cmd.Parameters.AddWithValue("@Id", id);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/login", async (LoginInput input) =>
{
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    var cmd = new SqlCommand("SELECT Name,Email,Role FROM Accounts WHERE Email=@Email AND PasswordHash=@Pw", con);
    cmd.Parameters.AddWithValue("@Email", input.Email);
    cmd.Parameters.AddWithValue("@Pw", input.Password);
    await using var rd = await cmd.ExecuteReaderAsync();
    if (!await rd.ReadAsync()) return Results.Unauthorized();
    return Results.Ok(new { name = rd.GetString(0), email = rd.GetString(1), role = rd.GetString(2) });
});

app.MapPost("/api/auth/register", async (RegisterInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
        return Results.BadRequest("Thiếu thông tin");
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    var cmd = new SqlCommand("""
        IF EXISTS(SELECT 1 FROM Accounts WHERE Email=@Email)
            THROW 50001, N'Email đã tồn tại', 1;
        INSERT INTO Accounts(Name,Email,PasswordHash,Role) VALUES(@Name,@Email,@Pw,'customer')
        """, con);
    cmd.Parameters.AddWithValue("@Name", input.Name);
    cmd.Parameters.AddWithValue("@Email", input.Email);
    cmd.Parameters.AddWithValue("@Pw", input.Password);
    try { await cmd.ExecuteNonQueryAsync(); }
    catch { return Results.Conflict("Email đã tồn tại"); }
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/orders", async () =>
{
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    return Results.Ok(await LoadOrders(con));
});

app.MapPost("/api/orders", async (OrderInput input) =>
{
    if (input.Items == null || input.Items.Count == 0)
        return Results.BadRequest("Giỏ hàng trống");

    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    await using var tran = await con.BeginTransactionAsync();

    try
    {
        decimal total = 0;

        foreach (var item in input.Items)
        {
            int productId = item.RealProductId;
            int quantity = item.RealQty;

            if (productId <= 0)
                throw new Exception("Mã sản phẩm không hợp lệ");

            if (quantity <= 0)
                throw new Exception("Số lượng sản phẩm không hợp lệ");

            var stockCmd = new SqlCommand("SELECT Price, Stock FROM Products WHERE Id=@Id AND IsDeleted=0", con, (SqlTransaction)tran);
            stockCmd.Parameters.AddWithValue("@Id", productId);

            await using var rd = await stockCmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                throw new Exception("Sản phẩm không tồn tại");

            decimal price = rd.GetDecimal(0);
            int stock = rd.GetInt32(1);
            await rd.CloseAsync();

            if (stock < quantity)
                throw new Exception($"Sản phẩm không đủ tồn kho. Trong kho còn {stock}, bạn đặt {quantity}");

            total += price * quantity;
        }

        string code = "TS" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

        var orderCmd = new SqlCommand("""
            INSERT INTO Orders(Code,CustomerName,Phone,Address,Note,CustomerEmail,Total,Status)
            OUTPUT INSERTED.Id
            VALUES(@Code,@Customer,@Phone,@Address,@Note,@Email,@Total,N'Mới')
            """, con, (SqlTransaction)tran);

        orderCmd.Parameters.AddWithValue("@Code", code);
        orderCmd.Parameters.AddWithValue("@Customer", input.Customer);
        orderCmd.Parameters.AddWithValue("@Phone", input.Phone);
        orderCmd.Parameters.AddWithValue("@Address", input.Addr);
        orderCmd.Parameters.AddWithValue("@Note", (object?)input.Note ?? "");
        orderCmd.Parameters.AddWithValue("@Email", (object?)input.Email ?? "");
        orderCmd.Parameters.AddWithValue("@Total", total);

        int orderId = (int)await orderCmd.ExecuteScalarAsync();

        foreach (var item in input.Items)
        {
            int productId = item.RealProductId;
            int quantity = item.RealQty;

            var detailCmd = new SqlCommand("""
                INSERT INTO OrderItems(OrderId,ProductId,Quantity,UnitPrice)
                SELECT @OrderId, Id, @Qty, Price
                FROM Products
                WHERE Id=@Pid AND IsDeleted=0;

                UPDATE Products
                SET Stock = Stock - @Qty
                WHERE Id=@Pid AND IsDeleted=0;
                """, con, (SqlTransaction)tran);

            detailCmd.Parameters.AddWithValue("@OrderId", orderId);
            detailCmd.Parameters.AddWithValue("@Pid", productId);
            detailCmd.Parameters.AddWithValue("@Qty", quantity);

            await detailCmd.ExecuteNonQueryAsync();
        }

        var his = new SqlCommand("INSERT INTO OrderStatusHistory(OrderId,FromStatus,ToStatus,ChangedBy) VALUES(@Id,N'',N'Mới',N'Hệ thống')", con, (SqlTransaction)tran);
        his.Parameters.AddWithValue("@Id", orderId);
        await his.ExecuteNonQueryAsync();

        await tran.CommitAsync();
        return Results.Ok(new { id = orderId, code, total });
    }
    catch (Exception ex)
    {
        await tran.RollbackAsync();
        return Results.Problem(ex.Message, statusCode: 400);
    }
});

app.MapPut("/api/orders/{id:int}/status", async (int id, StatusInput input) =>
{
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    var currentCmd = new SqlCommand("SELECT Status FROM Orders WHERE Id=@Id", con);
    currentCmd.Parameters.AddWithValue("@Id", id);
    var old = (string?)await currentCmd.ExecuteScalarAsync();
    if (old is null) return Results.NotFound();

    var cmd = new SqlCommand("""
        UPDATE Orders SET Status=@Status WHERE Id=@Id;
        INSERT INTO OrderStatusHistory(OrderId,FromStatus,ToStatus,ChangedBy) VALUES(@Id,@From,@To,N'Admin')
        """, con);
    cmd.Parameters.AddWithValue("@Id", id);
    cmd.Parameters.AddWithValue("@Status", input.Status);
    cmd.Parameters.AddWithValue("@From", old);
    cmd.Parameters.AddWithValue("@To", input.Status);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/stock-history", async () =>
{
    var list = new List<object>();
    await using var con = new SqlConnection(cs);
    await con.OpenAsync();
    var cmd = new SqlCommand("SELECT TOP 50 ProductId,ProductName,OldStock,NewStock,ChangedBy,ChangedAt FROM StockHistory ORDER BY Id DESC", con);
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
    {
        list.Add(new
        {
            pid = rd.GetInt32(0),
            name = rd.GetString(1),
            oldStock = rd.GetInt32(2),
            newStock = rd.GetInt32(3),
            by = rd.GetString(4),
            at = rd.GetDateTime(5).ToString("dd/MM/yyyy HH:mm")
        });
    }
    return Results.Ok(list);
});

app.MapFallbackToFile("index.html");
app.Run();

static async Task AddStockHistory(SqlConnection con, int pid, string name, int oldStock, int newStock, string by)
{
    if (oldStock == newStock) return;
    var cmd = new SqlCommand("INSERT INTO StockHistory(ProductId,ProductName,OldStock,NewStock,ChangedBy) VALUES(@Pid,@Name,@Old,@New,@By)", con);
    cmd.Parameters.AddWithValue("@Pid", pid);
    cmd.Parameters.AddWithValue("@Name", name);
    cmd.Parameters.AddWithValue("@Old", oldStock);
    cmd.Parameters.AddWithValue("@New", newStock);
    cmd.Parameters.AddWithValue("@By", by);
    await cmd.ExecuteNonQueryAsync();
}

static async Task<List<object>> LoadOrders(SqlConnection con)
{
    var orders = new List<object>();
    var cmd = new SqlCommand("SELECT Id,Code,CustomerName,Phone,Address,Note,CustomerEmail,Total,Status,CreatedAt FROM Orders ORDER BY Id DESC", con);
    await using var rd = await cmd.ExecuteReaderAsync();
    var temp = new List<(int id, string code, string customer, string phone, string addr, string note, string email, decimal total, string status, DateTime date)>();
    while (await rd.ReadAsync())
    {
        temp.Add((rd.GetInt32(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.IsDBNull(5) ? "" : rd.GetString(5), rd.IsDBNull(6) ? "" : rd.GetString(6), rd.GetDecimal(7), rd.GetString(8), rd.GetDateTime(9)));
    }
    await rd.CloseAsync();

    foreach (var o in temp)
    {
        var items = new List<object>();
        var itemCmd = new SqlCommand("SELECT ProductId,Quantity FROM OrderItems WHERE OrderId=@Id", con);
        itemCmd.Parameters.AddWithValue("@Id", o.id);
        await using var ir = await itemCmd.ExecuteReaderAsync();
        while (await ir.ReadAsync()) items.Add(new { pid = ir.GetInt32(0), qty = ir.GetInt32(1) });
        await ir.CloseAsync();

        var statusHistory = new List<object>();
        var hcmd = new SqlCommand("SELECT FromStatus,ToStatus,ChangedBy,ChangedAt FROM OrderStatusHistory WHERE OrderId=@Id ORDER BY Id DESC", con);
        hcmd.Parameters.AddWithValue("@Id", o.id);
        await using var hr = await hcmd.ExecuteReaderAsync();
        while (await hr.ReadAsync())
        {
            statusHistory.Add(new { from = hr.GetString(0), to = hr.GetString(1), by = hr.GetString(2), at = hr.GetDateTime(3).ToString("dd/MM/yyyy HH:mm") });
        }

        orders.Add(new
        {
            id = o.id,
            code = o.code,
            customer = o.customer,
            phone = o.phone,
            addr = o.addr,
            note = o.note,
            email = o.email,
            total = o.total,
            status = o.status,
            date = o.date.ToString("dd/MM/yyyy HH:mm"),
            items,
            statusHistory
        });
    }
    return orders;
}

record ProductInput(string Name, string Description, string Cat, decimal Price, int Stock, string Img, Dictionary<string, string>? Specs);
record LoginInput(string Email, string Password);
record RegisterInput(string Name, string Email, string Password);
record CartItemInput
{
    public int Pid { get; set; }
    public int Id { get; set; }
    public int ProductId { get; set; }

    public int Qty { get; set; }
    public int Quantity { get; set; }

    public int RealProductId =>
        Pid > 0 ? Pid :
        (ProductId > 0 ? ProductId : Id);

    public int RealQty =>
        Qty > 0 ? Qty : Quantity;
}
record OrderInput(string Customer, string Phone, string Addr, string? Note, string? Email, List<CartItemInput> Items);
record StatusInput(string Status);
