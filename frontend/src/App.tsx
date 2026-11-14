import { lazy, Suspense, useEffect, type ReactNode } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Guard, Logo, PortalLayout } from './components/layout'
import { Spinner } from './components/ui'
import { refreshSession } from './lib/api'
import { useAuth } from './store/auth'

const Landing = lazy(() => import('./pages/public/Landing'))
const Login = lazy(() => import('./pages/auth/Login'))
const SetPassword = lazy(() => import('./pages/auth/SetPassword'))
const Dashboard = lazy(() => import('./pages/Dashboard'))
const Tickets = lazy(() => import('./pages/tickets/Tickets'))
const TicketDetail = lazy(() => import('./pages/tickets/TicketDetail'))
const NewTicket = lazy(() => import('./pages/tickets/NewTicket'))
const Apps = lazy(() => import('./pages/workflow/Apps'))
const FormApp = lazy(() => import('./pages/workflow/FormApp'))
const Submissions = lazy(() => import('./pages/workflow/Submissions'))
const SubmissionDetail = lazy(() => import('./pages/workflow/SubmissionDetail'))
const Limits = lazy(() => import('./pages/workflow/Limits'))
const Inventory = lazy(() => import('./pages/inventory/Inventory'))
const AssetDetail = lazy(() => import('./pages/inventory/AssetDetail'))
const Knowledge = lazy(() => import('./pages/kb/Knowledge'))
const ArticlePage = lazy(() => import('./pages/kb/ArticlePage'))
const Directory = lazy(() => import('./pages/people/Directory'))
const Profile = lazy(() => import('./pages/people/Profile'))
const Announcements = lazy(() => import('./pages/comms/Announcements'))
const Events = lazy(() => import('./pages/comms/Events'))
const Achievements = lazy(() => import('./pages/comms/Achievements'))
const Surveys = lazy(() => import('./pages/comms/Surveys'))
const SurveyPage = lazy(() => import('./pages/comms/SurveyPage'))
const Documents = lazy(() => import('./pages/comms/Documents'))
const Messages = lazy(() => import('./pages/comms/Messages'))
const Jobs = lazy(() => import('./pages/comms/Jobs'))
const Notifications = lazy(() => import('./pages/comms/Notifications'))
const Tasks = lazy(() => import('./pages/work/Tasks'))
const Projects = lazy(() => import('./pages/work/Projects'))
const Stores = lazy(() => import('./pages/work/Stores'))
const OnCall = lazy(() => import('./pages/work/OnCall'))
const Reports = lazy(() => import('./pages/Reports'))
const Users = lazy(() => import('./pages/admin/Users'))
const AccountRequests = lazy(() => import('./pages/admin/AccountRequests'))
const Organization = lazy(() => import('./pages/admin/Organization'))
const Groups = lazy(() => import('./pages/admin/Groups'))
const Roles = lazy(() => import('./pages/admin/Roles'))
const HelpdeskSettings = lazy(() => import('./pages/admin/HelpdeskSettings'))
const FormBuilder = lazy(() => import('./pages/admin/FormBuilder'))
const ContentAdmin = lazy(() => import('./pages/admin/ContentAdmin'))
const LeaveAdmin = lazy(() => import('./pages/admin/LeaveAdmin'))
const Audit = lazy(() => import('./pages/admin/Audit'))
const SystemHealth = lazy(() => import('./pages/system/SystemHealth'))
const CacheAdmin = lazy(() => import('./pages/system/CacheAdmin'))

function FullScreenLoader() {
  return (
    <div className="grid min-h-screen place-items-center">
      <div className="flex flex-col items-center gap-4"><Logo /><Spinner className="h-5 w-5 text-brand-500" /></div>
    </div>
  )
}

/** Sayfa değişince en üste döner (yalnızca yol değişince; filtre/sekme gibi sorgu değişikliklerinde konum korunur). */
function ScrollToTop() {
  const { pathname } = useLocation()
  useEffect(() => { window.scrollTo({ top: 0, left: 0, behavior: "instant" }) }, [pathname])
  return null
}

function PageLoader() {
  return <div className="grid min-h-[50vh] place-items-center"><Spinner className="h-5 w-5 text-brand-500" /></div>
}

/** Oturum yoksa giriş sayfasına; ilk girişte parola belirlenmemişse parola sayfasına yönlendirir. */
function RequireAuth({ children }: { children: ReactNode }) {
  const { user, bootstrapping } = useAuth()
  const location = useLocation()
  if (bootstrapping) return <FullScreenLoader />
  // Giriş yapmamış ziyaretçi kök adreste genel tanıtım sitesini görür
  if (!user) return location.pathname === '/' ? <Navigate to="/welcome" replace /> : <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (user.mustChangePassword) return <Navigate to="/set-password" replace />
  return <>{children}</>
}

const g = (perms: string[], el: ReactNode) => <Guard perms={perms}>{el}</Guard>

export default function App() {
  const done = useAuth((s) => s.done)
  // Uygulama açılışında httpOnly refresh çereziyle oturumu sessizce geri yükle
  useEffect(() => {
    refreshSession().finally(done)
  }, [done])

  return (
    <Suspense fallback={<FullScreenLoader />}>
      <ScrollToTop />
      <Routes>
        <Route path="/welcome" element={<Landing />} />
        <Route path="/login" element={<Login />} />
        <Route path="/set-password" element={<SetPassword />} />
        <Route element={<RequireAuth><PortalLayout /></RequireAuth>}>
          <Route index element={<Suspense fallback={<PageLoader />}><Dashboard /></Suspense>} />
          <Route path="*" element={<Suspense fallback={<PageLoader />}><PortalRoutes /></Suspense>} />
        </Route>
      </Routes>
    </Suspense>
  )
}

function PortalRoutes() {
    return (
      <Routes>
        <Route path="tickets" element={<Tickets />} />
        <Route path="tickets/new" element={<NewTicket />} />
        <Route path="tickets/:id" element={<TicketDetail />} />
        <Route path="apps" element={<Apps />} />
        <Route path="apps/limits" element={<Limits />} />
        <Route path="apps/:key" element={<FormApp />} />
        <Route path="requests" element={<Submissions mode="requests" />} />
        <Route path="approvals" element={<Submissions mode="approvals" />} />
        <Route path="submissions/:id" element={<SubmissionDetail />} />
        <Route path="inventory" element={<Inventory />} />
        <Route path="inventory/:id" element={<AssetDetail />} />
        <Route path="kb" element={<Knowledge />} />
        <Route path="kb/:id" element={<ArticlePage />} />
        <Route path="directory" element={<Directory />} />
        <Route path="profile" element={<Profile />} />
        <Route path="announcements" element={<Announcements />} />
        <Route path="events" element={<Events />} />
        <Route path="achievements" element={<Achievements />} />
        <Route path="surveys" element={<Surveys />} />
        <Route path="surveys/:id" element={<SurveyPage />} />
        <Route path="documents" element={<Documents />} />
        <Route path="messages" element={<Messages />} />
        <Route path="jobs" element={<Jobs />} />
        <Route path="notifications" element={<Notifications />} />
        <Route path="tasks" element={<Tasks />} />
        <Route path="projects" element={<Projects />} />
        <Route path="stores" element={<Stores />} />
        <Route path="on-call" element={<OnCall />} />
        <Route path="reports" element={g(['reports.view', 'reports.view_department', 'tickets.view_all', 'inventory.view'], <Reports />)} />
        <Route path="admin/users" element={g(['users.manage'], <Users />)} />
        <Route path="admin/account-requests" element={g(['accounts.request', 'accounts.approve'], <AccountRequests />)} />
        <Route path="admin/organization" element={g(['org.manage', 'users.manage'], <Organization />)} />
        <Route path="admin/groups" element={g(['org.manage', 'tickets.configure'], <Groups />)} />
        <Route path="admin/roles" element={g(['roles.manage', 'users.manage'], <Roles />)} />
        <Route path="admin/helpdesk" element={g(['tickets.configure'], <HelpdeskSettings />)} />
        <Route path="admin/forms" element={g(['forms.manage'], <FormBuilder />)} />
        <Route path="admin/content" element={g(['content.manage'], <ContentAdmin />)} />
        <Route path="admin/leave" element={g(['leave.manage'], <LeaveAdmin />)} />
        <Route path="admin/audit" element={g(['audit.view'], <Audit />)} />
        <Route path="system" element={g(['system.manage'], <SystemHealth />)} />
        <Route path="system/cache" element={g(['system.manage'], <CacheAdmin />)} />
        <Route path="*" element={<div className="card mx-auto mt-10 max-w-md p-8 text-center"><h2 className="text-lg font-semibold">Sayfa bulunamadı</h2><p className="mt-1 text-sm muted">Aradığınız sayfa taşınmış veya kaldırılmış olabilir.</p></div>} />
      </Routes>
    )
  }
