namespace StajDesk.Domain;

public enum TicketStatus { Open = 0, InProgress = 1, Pending = 2, Resolved = 3, Closed = 4 }

public enum TicketPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

public enum TicketType { Incident = 0, Request = 1 }
