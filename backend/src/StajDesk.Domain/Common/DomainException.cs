namespace StajDesk.Domain;

/// <summary>Hata durumunda HTTP katmanının uygun koda çevirdiği domain istisnası.</summary>
public class DomainException(string message) : Exception(message);
