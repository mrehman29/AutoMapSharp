using System;
using System.Collections.Generic;

namespace AutoMapSharp.Sample.Web.Models;

public class WebOrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class WebOrderItemViewModel
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class WebMultiItemOrder
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<WebOrderItem>? Items { get; set; }
    public HashSet<string>? Tags { get; set; }
    public Dictionary<string, string>? Specs { get; set; }
}

public class WebMultiItemOrderViewModel
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<WebOrderItemViewModel>? Items { get; set; }
    public HashSet<string>? Tags { get; set; }
    public Dictionary<string, string>? Specs { get; set; }
}

public class WebImmutableProduct
{
    public string SkuCode { get; }
    public decimal RetailPrice { get; }
    public string TierTag { get; }

    public WebImmutableProduct(string skuCode, decimal retailPrice, string tierTag)
    {
        SkuCode = skuCode;
        RetailPrice = retailPrice;
        TierTag = tierTag;
    }
}

public class WebProductSource
{
    public string Sku { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public bool IsEnterprise { get; set; }
}

// 4-level deep enterprise hierarchy
public class WebOrgEnterprise
{
    public string Name { get; set; } = string.Empty;
    public WebOrgDivision? Division { get; set; }
}

public class WebOrgDivision
{
    public string Name { get; set; } = string.Empty;
    public WebOrgDepartment? Department { get; set; }
}

public class WebOrgDepartment
{
    public string Name { get; set; } = string.Empty;
    public WebOrgManager? Manager { get; set; }
}

public class WebOrgManager
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class WebOrgEnterpriseViewModel
{
    public string Name { get; set; } = string.Empty;
    public WebOrgDivisionViewModel? Division { get; set; }
}

public class WebOrgDivisionViewModel
{
    public string Name { get; set; } = string.Empty;
    public WebOrgDepartmentViewModel? Department { get; set; }
}

public class WebOrgDepartmentViewModel
{
    public string Name { get; set; } = string.Empty;
    public WebOrgManagerViewModel? Manager { get; set; }
}

public class WebOrgManagerViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Circular reference models
public class WebCircularEmployee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public WebCircularEmployee? Manager { get; set; }
    public List<WebCircularEmployee> Subordinates { get; set; } = new();
}

public class WebCircularEmployeeViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public WebCircularEmployeeViewModel? Manager { get; set; }
    public List<WebCircularEmployeeViewModel> Subordinates { get; set; } = new();
}
