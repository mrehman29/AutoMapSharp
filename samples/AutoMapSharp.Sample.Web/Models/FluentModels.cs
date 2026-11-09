using System;

namespace AutoMapSharp.Sample.Web.Models;

public class UserProfileEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string InternalSecurityNotes { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Bio { get; set; }
    public string UnsanitizedFeedback { get; set; } = string.Empty;
}

public class UserProfileViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; } // Skipped -> null
    public string? InternalSecurityNotes { get; set; } // Skipped -> null
    public string TenantBadge { get; set; } = string.Empty; // Constant -> "CORP-GLOBAL"
    public string DisplayName { get; set; } = string.Empty; // MapSource -> FirstName + " " + LastName
    public string FullAddress { get; set; } = string.Empty; // MapSource -> Street + ", " + City + " " + PostalCode
    public bool EligibleForDiscount { get; set; }
    public string DiscountTier { get; set; } = "None"; // If condition: if Age >= 18 -> "Standard", else unchanged
    public string Bio { get; set; } = string.Empty; // Fallback -> "No public biography."
    public string UnsanitizedFeedback { get; set; } = string.Empty; // Trimmed by ValueTransformer
}

public class ConditionFormModel
{
    public int Age { get; set; } = 25;
    public decimal AnnualSpend { get; set; } = 1500m;
    public string? Bio { get; set; }
}

public class ConditionResultViewModel
{
    public int Age { get; set; }
    public decimal AnnualSpend { get; set; }
    public string MemberStatus { get; set; } = "Basic";
    public string Bio { get; set; } = string.Empty;
}

public class ReverseEmployeeEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ConfidentialCode { get; set; } = string.Empty;
}

public class ReverseEmployeeDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? ConfidentialCode { get; set; }
}

public class DirtyTextEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}

public class CleanTextViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}
