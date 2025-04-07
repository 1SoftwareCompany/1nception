namespace One.Inception.Multitenancy;

public interface IHaveTenant
{
    string Tenant { get; set; }
}
