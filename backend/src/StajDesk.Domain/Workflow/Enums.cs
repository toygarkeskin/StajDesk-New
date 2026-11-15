namespace StajDesk.Domain;

public enum SubmissionStatus { Pending = 0, Approved = 1, Rejected = 2, Completed = 3, Cancelled = 4 }

/// <summary>FieldUser: onaylayıcı, formdaki bir "kişi" alanında seçilen çalışandır (ör. vardiya takası yapılacak kişi). Alan boşsa adım atlanır.</summary>
public enum ApproverType { Manager = 0, DepartmentManager = 1, Role = 2, User = 3, ResponsibleDepartment = 4, FieldUser = 5 }

public enum SubmissionActionType { Submitted = 0, Approved = 1, Rejected = 2, Commented = 3, Completed = 4, Cancelled = 5, Skipped = 6 }

public enum LeaveType { Annual = 0, Excuse = 1, Sick = 2, Unpaid = 3, Marriage = 4, Bereavement = 5, Paternity = 6 }
