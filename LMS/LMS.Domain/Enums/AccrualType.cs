namespace LMS.Domain.Enums;

/// <summary>
/// Controls how leave entitlement is granted.
/// Annual = credited once per year; OneTime = granted once (e.g., maternity);
/// Unlimited = no cap (e.g., unpaid leave).
/// No carry-forward for any type — org policy POL-06.
/// </summary>
public enum AccrualType
{
    Annual = 0,
    OneTime = 1,
    Unlimited = 2,
}
