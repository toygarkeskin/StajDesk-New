// Backend DTO'larının TypeScript karşılıkları (JSON camelCase, enum'lar string).

export type TicketStatus = 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed'
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type TicketType = 'Incident' | 'Request'
export type AssetType = 'Computer' | 'Laptop' | 'Monitor' | 'Printer' | 'NetworkDevice' | 'Phone' | 'Server' | 'Peripheral' | 'HandheldTerminal'
export type AssetStatus = 'InUse' | 'InStock' | 'InRepair' | 'Retired'
export type SubmissionStatus = 'Pending' | 'Approved' | 'Rejected' | 'Completed' | 'Cancelled'
export type SubmissionActionType = 'Submitted' | 'Approved' | 'Rejected' | 'Commented' | 'Completed' | 'Cancelled' | 'Skipped'
export type ApproverType = 'Manager' | 'DepartmentManager' | 'Role' | 'User' | 'ResponsibleDepartment' | 'FieldUser'
export type DepartmentType = 'HeadOffice' | 'Store' | 'Region'
export type EmploymentType = 'FullTime' | 'PartTime' | 'Intern'
export type AccountRequestStatus = 'Pending' | 'Approved' | 'Rejected'
export type AnnouncementKind = 'Announcement' | 'News'
export type WorkTaskStatus = 'Todo' | 'InProgress' | 'Review' | 'Done'
export type ProjectStatus = 'Planned' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled'
export type AchievementKind = 'Award' | 'Kudos' | 'Milestone'
export type StoreStatus = 'Open' | 'Renovation' | 'Closed'

export interface Envelope<T> { success: boolean; data: T; message?: string | null; errors?: Record<string, string[]> | null; code?: string | null }
export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export interface Ref { id: string; fullName: string }
export interface UserRef { id: string; fullName: string; email: string }
export interface DeptRef { id: number; name: string }
export interface RoleRef { code: string; name: string }
export interface FileDto { id: string; fileName: string; contentType: string; size: number; createdAt: string }

export interface Me {
  id: string; firstName: string; lastName: string; fullName: string; email: string; employeeNo?: string | null
  department?: DeptRef | null; jobTitle?: string | null; manager?: Ref | null; phone?: string | null; birthDate?: string | null
  hireDate?: string | null; location?: string | null; bio?: string | null; employmentType: EmploymentType
  roles: RoleRef[]; permissions: string[]; mustChangePassword: boolean
}
export interface AuthResponse { accessToken: string; expiresAt: string; user: Me }
export interface LoginChallenge { challengeId: string; maskedEmail: string; expiresAt: string; resendAfterSeconds: number; codeLength: number; devMailboxUrl?: string | null }
export interface LoginResult { requiresCode: boolean; challenge?: LoginChallenge | null; auth?: AuthResponse | null }

// ---------- Talepler ----------
export interface TicketListItem {
  id: number; title: string; status: TicketStatus; priority: TicketPriority; type: TicketType
  categoryId: number; categoryName: string; categoryColor: string; requester: UserRef; departmentName?: string | null
  assignee?: UserRef | null; groupName?: string | null; createdAt: string; updatedAt: string; dueAt: string; isOverdue: boolean
  commentCount: number; attachmentCount: number
}
export interface TicketComment { id: number; body: string; isInternal: boolean; author: UserRef; authorIsStaff: boolean; createdAt: string }
export interface TicketHistory { id: number; field: string; oldValue?: string | null; newValue?: string | null; actor?: UserRef | null; createdAt: string }
export interface AssetRef { id: number; name: string; assetTag: string; type: AssetType }
export interface TicketDetail extends Omit<TicketListItem, 'commentCount' | 'attachmentCount' | 'departmentName'> {
  description: string; requesterTitle?: string | null; requesterPhone?: string | null; department?: DeptRef | null; groupId?: number | null
  asset?: AssetRef | null; appliedRule?: string | null; resolvedAt?: string | null; closedAt?: string | null
  allowedTransitions: TicketStatus[]; canEdit: boolean; canManage: boolean
  comments: TicketComment[]; history: TicketHistory[]; attachments: FileDto[]
}
export interface Category { id: number; name: string; description?: string | null; color: string; isActive: boolean; defaultGroupId?: number | null; defaultGroupName?: string | null; ticketCount: number; openTicketCount: number }
export interface RoutingRule {
  id: number; name: string; sortOrder: number; isActive: boolean; categoryId?: number | null; categoryName?: string | null
  departmentId?: number | null; departmentName?: string | null; keyword?: string | null; minPriority?: TicketPriority | null
  assignGroupId?: number | null; assignGroupName?: string | null; assignUserId?: string | null; assignUserName?: string | null
  setPriority?: TicketPriority | null; matchCount: number
}
export interface ArticleListItem {
  id: number; title: string; summary: string; categoryId?: number | null; categoryName?: string | null; categoryColor?: string | null
  isFaq: boolean; author: UserRef; views: number; helpfulCount: number; isPublished: boolean; updatedAt: string
}
export interface Article extends ArticleListItem { content: string; createdAt: string }

// ---------- Organizasyon ----------
export interface Department { id: number; name: string; code?: string | null; type: DepartmentType; parentId?: number | null; manager?: Ref | null; userCount: number; isActive: boolean }
export interface JobTitle { id: number; name: string; userCount: number }
export interface GroupMember { userId: string; fullName: string; jobTitle?: string | null; isLead: boolean }
export interface Group { id: number; name: string; description?: string | null; department?: DeptRef | null; isSupportGroup: boolean; members: GroupMember[]; openTicketCount: number }
export interface Role { id: number; code: string; name: string; description?: string | null; isSystem: boolean; permissions: string[]; userCount: number }
export interface PermissionDef { code: string; group: string; label: string }
export interface UserListItem { id: string; fullName: string; email: string; employeeNo?: string | null; department?: DeptRef | null; jobTitle?: string | null; roles: string[]; isActive: boolean; mustChangePassword: boolean; lastLoginAt?: string | null; createdAt: string }
export interface UserDetail {
  id: string; firstName: string; lastName: string; fullName: string; email: string; employeeNo?: string | null
  departmentId?: number | null; departmentName?: string | null; jobTitleId?: number | null; jobTitle?: string | null; manager?: Ref | null
  phone?: string | null; birthDate?: string | null; hireDate?: string | null; employmentType: EmploymentType; location?: string | null
  isActive: boolean; deactivatedAt?: string | null; deactivationReason?: string | null; mustChangePassword: boolean; lastLoginAt?: string | null; createdAt: string
  roles: RoleRef[]; groups: string[]; assignedAssetCount: number; openTicketCount: number
}
export interface AccountRequest {
  id: number; firstName: string; lastName: string; email: string; employeeNo?: string | null; department: DeptRef; jobTitle?: string | null; manager?: Ref | null
  startDate: string; employmentType: EmploymentType; phone?: string | null; note?: string | null; status: AccountRequestStatus; requestedBy: Ref; createdAt: string
  processedBy?: Ref | null; processedAt?: string | null; rejectReason?: string | null; createdUserId?: string | null
}
export interface DirectoryEntry { id: string; fullName: string; email: string; phone?: string | null; jobTitle?: string | null; department?: DeptRef | null; location?: string | null; manager?: Ref | null; hireDate?: string | null }
export interface Birthday { id: string; fullName: string; jobTitle?: string | null; department?: string | null; month: number; day: number; isToday: boolean }
export interface AuditLog { id: number; userId?: string | null; userName?: string | null; action: string; entityType: string; entityId?: string | null; changes?: string | null; ipAddress?: string | null; createdAt: string }

// ---------- Envanter ----------
export interface Asset {
  id: number; name: string; assetTag: string; type: AssetType; status: AssetStatus; manufacturer?: string | null; model?: string | null; serialNumber?: string | null
  department?: DeptRef | null; location?: string | null; assignedTo?: UserRef | null; purchaseDate?: string | null; warrantyEndsAt?: string | null
  purchaseCost?: number | null; notes?: string | null; createdAt: string; updatedAt: string; openTicketCount: number
}
export interface Assignment { id: number; user: UserRef; assignedBy: UserRef; assignedAt: string; returnedAt?: string | null; returnedBy?: UserRef | null; note?: string | null; returnCondition?: string | null }
export interface AssetDetail { asset: Asset; assignments: Assignment[]; software: { id: number; licenseId: number; name: string; version?: string | null; installedAt: string }[]; tickets: TicketListItem[] }
export interface License { id: number; name: string; vendor?: string | null; version?: string | null; seats: number; used: number; expiresAt?: string | null; cost?: number | null; notes?: string | null; isExpired: boolean }

// ---------- Form ve iş akışı ----------
export type FieldType = 'text' | 'textarea' | 'number' | 'currency' | 'date' | 'select' | 'multiselect' | 'checkbox' | 'email' | 'file' | 'user' | 'description'
export interface FormField { key: string; label: string; type: FieldType; required: boolean; options?: string[] | null; placeholder?: string | null; helpText?: string | null; min?: number | null; max?: number | null; maxLength?: number | null }
export interface WorkflowStep { name: string; approverType: ApproverType; roleCode?: string | null; userId?: string | null; fieldKey?: string | null }
export interface FormSummary { id: number; key: string; name: string; description?: string | null; module: string; icon: string; color: string; kind: string; ownerDepartment?: DeptRef | null; stepCount: number; isActive: boolean; pendingCount: number }
export interface FormContext { leaveRemaining?: number | null; leaveEntitled?: number | null; leaveUsed?: number | null; leavePending?: number | null; limit?: number | null; limitLabel?: string | null }
export interface FormDefinition extends Omit<FormSummary, 'stepCount' | 'pendingCount'> { fields: FormField[]; steps: WorkflowStep[]; allowedRoles: string[]; canManage: boolean; context?: FormContext | null }
export interface SubmissionListItem {
  id: number; number: string; formKey: string; formName: string; module: string; icon: string; color: string; submitter: UserRef; departmentName?: string | null
  status: SubmissionStatus; summary?: string | null; amount?: number | null; currentStepName?: string | null; currentStep: number; stepCount: number; createdAt: string; updatedAt: string
}
export interface SubmissionDetail {
  id: number; number: string; form: FormSummary; submitter: UserRef; submitterTitle?: string | null; department?: DeptRef | null; status: SubmissionStatus
  summary?: string | null; amount?: number | null; fields: { key: string; label: string; type: FieldType; value: unknown; display: string }[]
  steps: { index: number; name: string; state: 'approved' | 'rejected' | 'skipped' | 'current' | 'waiting' | 'cancelled'; approvers: UserRef[] }[]
  actions: { id: number; action: SubmissionActionType; stepName?: string | null; actor?: UserRef | null; comment?: string | null; createdAt: string }[]
  files: FileDto[]; assignee?: UserRef | null; canApprove: boolean; canComplete: boolean; canCancel: boolean; createdAt: string; updatedAt: string; completedAt?: string | null
}
export interface LeaveBalance { userId: string; fullName: string; department?: string | null; year: number; entitled: number; carriedOver: number; used: number; pending: number; remaining: number }
export interface LeaveCalendarEntry { submissionId: number; user: UserRef; leaveType: string; start: string; end: string; days: number; status: SubmissionStatus }
export interface RevenueSummary {
  from: string; to: string; companyWide: boolean; total: number; reports: number; receipts: number; averageBasket?: number | null; pendingCount: number
  byDay: { date: string; amount: number }[]; byStore: { store: string; amount: number; reports: number; receipts: number; averageBasket?: number | null }[]
}
export interface ShiftChangeEntry { submissionId: number; user: UserRef; date: string; from?: string | null; to?: string | null; swapWith?: string | null; status: SubmissionStatus }
export interface Limit { id: number; kind: string; label: string; maxAmount: number; currency: string }
export interface AppEntry { key: string; name: string; module: string; icon: string; color: string; route: string; kind: 'form' | 'module'; description?: string | null; badge: number }

// ---------- İletişim ----------
export interface Announcement { id: number; title: string; summary: string; body: string; kind: AnnouncementKind; isPublic: boolean; isPinned: boolean; publishAt: string; expiresAt?: string | null; department?: DeptRef | null; author: UserRef }
export interface Banner { id: number; title: string; subtitle?: string | null; imageUrl?: string | null; theme: string; linkUrl?: string | null; linkText?: string | null; sortOrder: number; startsAt?: string | null; endsAt?: string | null; isActive: boolean; isLive: boolean }
export interface CompanyEvent { id: number; title: string; description?: string | null; location?: string | null; category: string; startsAt: string; endsAt?: string | null; isPublic: boolean; attendeeCount: number; isAttending: boolean; createdBy: UserRef }
export interface Job { id: number; title: string; department?: DeptRef | null; location?: string | null; employmentType: EmploymentType; description: string; requirements?: string | null; isPublic: boolean; isActive: boolean; closesAt?: string | null; createdAt: string }
export interface Achievement { id: number; user: UserRef; userTitle?: string | null; title: string; description?: string | null; kind: AchievementKind; givenBy: UserRef; likes: number; createdAt: string }
export interface SurveyQuestion { id: string; text: string; type: 'single' | 'multi' | 'text' | 'rating'; options: string[]; required: boolean }
export interface SurveyListItem { id: number; title: string; description?: string | null; isAnonymous: boolean; isActive: boolean; endsAt?: string | null; questionCount: number; responseCount: number; hasResponded: boolean }
export interface Survey { id: number; title: string; description?: string | null; isAnonymous: boolean; isActive: boolean; endsAt?: string | null; questions: SurveyQuestion[]; hasResponded: boolean; canManage: boolean }
export interface SurveyResults { surveyId: number; title: string; responseCount: number; questions: { id: string; text: string; type: string; counts: Record<string, number>; average?: number | null; textAnswers: string[] }[] }
export interface DocumentItem { id: number; title: string; description?: string | null; category: string; file: FileDto; department?: DeptRef | null; uploadedBy: UserRef; downloads: number; createdAt: string }
export interface Conversation { user: UserRef; jobTitle?: string | null; lastMessage: string; lastAt: string; unread: number; lastFromMe: boolean }
export interface Message { id: number; senderId: string; body: string; createdAt: string; readAt?: string | null }
export interface Project { id: number; code: string; name: string; description?: string | null; department?: DeptRef | null; owner: UserRef; status: ProjectStatus; startDate?: string | null; dueDate?: string | null; taskCount: number; doneCount: number }
export interface WorkTask { id: number; projectId?: number | null; projectName?: string | null; title: string; description?: string | null; assignee?: UserRef | null; createdBy: UserRef; status: WorkTaskStatus; priority: TicketPriority; dueDate?: string | null; isOverdue: boolean; completedAt?: string | null; createdAt: string }
export interface StoreItem { id: number; code: string; name: string; city?: string | null; department?: DeptRef | null; manager?: UserRef | null; phone?: string | null; address?: string | null; status: StoreStatus; areaSqm?: number | null; staffCount: number }
export interface OnCall { id: number; user: UserRef; phone?: string | null; jobTitle?: string | null; team: string; startsAt: string; endsAt: string; note?: string | null; isCurrent: boolean }
export interface NotificationItem { id: number; title: string; message: string; kind: string; link?: string | null; isRead: boolean; createdAt: string }

// ---------- Panel, rapor, sistem ----------
export interface Count { key: string; label: string; value: number; color?: string | null }
export interface TrendPoint { date: string; created: number; resolved: number }
export interface Dashboard {
  profile: { fullName: string; jobTitle?: string | null; department?: string | null; manager?: string | null; hireDate?: string | null; yearsOfService: number; roles: string[] }
  leave: LeaveBalance; myOpenTickets: number; myTicketsAwaitingConfirmation: number; myPendingRequests: number; approvalsPending: number; assignedToComplete: number
  unreadNotifications: number; unreadMessages: number; tasks: WorkTask[]; announcements: Announcement[]; events: CompanyEvent[]; birthdays: Birthday[]
  achievements: Achievement[]; onCall: OnCall[]; recentTickets: TicketListItem[]
  support?: { queueOpen: number; unassigned: number; overdue: number; assignedToMe: number; myGroups: number } | null
  ticketStats?: { scope: string; byStatus: Count[]; trend: TrendPoint[]; slaCompliance: number; avgResolutionHours: number } | null
  team?: { teamSize: number; onLeaveToday: number; pendingApprovals: number; onLeave: UserRef[] } | null
  inventory?: { total: number; inUse: number; inStock: number; inRepair: number; warrantyExpiring: number; licensesExpiring: number; myAssets: number } | null
  admin?: { pendingAccountRequests: number; mustChangePassword: number; inactiveUsers: number; activeUsers: number } | null
  generatedAt: string
}
export interface TicketReport {
  scope: string; from: string; to: string; total: number; open: number; resolved: number; overdue: number; avgResolutionHours: number; slaCompliance: number
  byStatus: Count[]; byPriority: Count[]; byCategory: Count[]; byDepartment: Count[]; byGroup: Count[]; trend: TrendPoint[]; aging: Count[]
  agents: { id: string; fullName: string; assigned: number; resolved: number; avgResolutionHours: number; slaCompliance: number }[]
}
export interface WorkflowReport {
  scope: string; from: string; to: string; total: number; pending: number; approved: number; rejected: number; completed: number; avgDecisionHours: number
  byModule: Count[]; byForm: { key: string; name: string; module: string; total: number; pending: number; approved: number; rejected: number; completed: number; avgDecisionHours: number }[]
  byDepartment: Count[]; trend: TrendPoint[]
}
export interface InventoryReport { total: number; totalValue: number; byType: Count[]; byStatus: Count[]; byDepartment: Count[]; warrantyExpiring: Asset[]; licenses: { name: string; seats: number; used: number; expiresAt?: string | null }[] }
export interface PeopleReport { active: number; inactive: number; byDepartmentType: Count[]; byDepartment: Count[]; byEmploymentType: Count[]; newHires90: number; mustChangePassword: number }
export interface PublicHome { banners: Banner[]; news: Announcement[]; jobs: Job[]; events: CompanyEvent[]; stats: { employees: number; stores: number; cities: number; headOfficeDepartments: number; resolvedRequestsThisYear: number } }
export interface MarketData { rates: { code: string; name: string; value: number; source: string; asOf: string }[]; fetchedAt: string; errors: string[] }
export interface Weather { city: string; temperature: number; humidity?: number | null; wind?: number | null; code: number; description: string; days: { date: string; min: number; max: number; code: number; description: string }[]; source: string; fetchedAt: string }
export interface SystemHealth { healthy: boolean; checkedAt: string; components: { name: string; role: string; healthy: boolean; latencyMs: number; detail: string }[]; outboxPending: number; outboxFailed: number }
