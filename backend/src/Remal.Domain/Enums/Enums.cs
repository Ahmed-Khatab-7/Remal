namespace Remal.Domain.Enums;

public enum ProductCategory
{
    Unisex = 0,
    Men = 1,
    Women = 2,
}

public enum ProductStatus
{
    Active = 0,
    OutOfStock = 1,
    Archived = 2,
}

public enum BundleStatus
{
    Active = 0,
    Archived = 1,
}

public enum CollectionStatus
{
    Active = 0,
    Archived = 1,
}

public enum OrderStatus
{
    Pending = 0,
    Preparing = 1,
    Shipping = 2,
    Delivered = 3,
    Cancelled = 4,
    Refunded = 5,
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    InstaPay = 1,
    Wallet = 2,
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3,
}

public enum ExpenseCategory
{
    Inventory = 0,
    Packaging = 1,
    Shipping = 2,
    Marketing = 3,
    Operations = 4,
    Fees = 5,
    Other = 6,
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public enum CouponType
{
    Percent = 0,
    Fixed = 1,
}

public enum PromotionType
{
    /// <summary>اشترِ كمية من حجم معيّن، خد حجم آخر هدية. (Buy N of size X → get size Y free)</summary>
    BuyXGetYFree = 0,
    /// <summary>اشترِ كمية معيّنة → خصم نسبة على إجمالي الطلب. (Buy N → % off)</summary>
    BuyXGetPercentOff = 1,
    /// <summary>لو الطلب فوق مبلغ معيّن → منتج هدية. (Spend over amount → free gift)</summary>
    FreeGiftOverAmount = 2,
    /// <summary>خصم نسبة على إجمالي الطلب فوق مبلغ معيّن. (% off whole order over amount)</summary>
    OrderPercentOver = 3,
}

public enum AuditCategory
{
    Auth = 0,
    Product = 1,
    Bundle = 2,
    Collection = 3,
    Order = 4,
    Customer = 5,
    Coupon = 6,
    Review = 7,
    Expense = 8,
    Settlement = 9,
    Inventory = 10,
    Settings = 11,
    System = 12,
    Payment = 13,
}
