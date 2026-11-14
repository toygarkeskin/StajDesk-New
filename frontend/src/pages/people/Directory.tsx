import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Building2, Mail, MapPin, MessageSquare, Phone } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Avatar, DefinitionList, Drawer, EmptyState, PageHeader, Pagination, QueryState, SearchInput } from '@/components/ui'
import { api } from '@/lib/api'
import { fmt } from '@/lib/format'
import { useDepartments } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { DirectoryEntry, Paged } from '@/lib/types'

export default function Directory() {
  const [sp, setSp] = useSearchParams()
  const [search, setSearch] = useState('')
  const [dept, setDept] = useState('')
  const [page, setPage] = useState(1)
  const departments = useDepartments()
  const params = { search: search || undefined, departmentId: dept || undefined, page, pageSize: 24 }
  const q = useQuery({ queryKey: ['directory', params], queryFn: () => api.get<Paged<DirectoryEntry>>('/directory', params), placeholderData: keepPreviousData })
  const selectedId = sp.get('u')
  const selected = useQuery({ queryKey: ['directory-one', selectedId], queryFn: () => api.get<Paged<DirectoryEntry>>('/directory', { search: '', pageSize: 200 }).then((p) => p.items.find((x) => x.id === selectedId) ?? null), enabled: !!selectedId && !q.data?.items.some((x) => x.id === selectedId) })
  const person = q.data?.items.find((x) => x.id === selectedId) ?? selected.data ?? null

  return (
    <div>
      <PageHeader title="Çalışan Rehberi" description="İş iletişim bilgileri. Kişisel veriler (doğum yılı, adres vb.) gösterilmez." />
      <div className="mb-5 flex flex-col gap-2 sm:flex-row">
        <SearchInput className="sm:w-96" value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder="Ad, unvan, departman veya e-posta…" />
        <select className="input sm:w-72" value={dept} onChange={(e) => { setDept(e.target.value); setPage(1) }} aria-label="Departman"><option value="">Tüm departmanlar</option><DepartmentOptions departments={departments.data} /></select>
      </div>
      <QueryState query={q} empty={(d) => d.items.length === 0 && <div className="card"><EmptyState title="Çalışan bulunamadı" /></div>}>
        {(d) => (
          <>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
              {d.items.map((p) => (
                <button key={p.id} onClick={() => setSp({ u: p.id })} className="card flex items-center gap-3 p-4 text-left transition hover:shadow-pop">
                  <Avatar name={p.fullName} size={44} />
                  <span className="min-w-0">
                    <span className="block truncate font-semibold text-ink-900 dark:text-white">{p.fullName}</span>
                    <span className="block truncate text-xs muted">{p.jobTitle}</span>
                    <span className="mt-0.5 block truncate text-xs muted">{p.department?.name}</span>
                  </span>
                </button>
              ))}
            </div>
            <div className="card mt-4"><Pagination page={d.page} totalPages={d.totalPages} total={d.totalCount} onPage={setPage} /></div>
          </>
        )}
      </QueryState>
      <Drawer open={!!person} onClose={() => setSp({})} title={person?.fullName ?? ''} subtitle={person?.jobTitle} width="max-w-md">
        {person && (
          <div>
            <div className="flex flex-col items-center py-4 text-center"><Avatar name={person.fullName} size={80} /><div className="mt-3 text-lg font-bold text-ink-900 dark:text-white">{person.fullName}</div><div className="text-sm muted">{person.jobTitle}</div></div>
            <div className="mb-5 grid grid-cols-3 gap-2">
              <a href={`mailto:${person.email}`} className="btn-secondary flex-col gap-1 py-6 text-xs"><Mail className="h-4 w-4" />E-posta</a>
              {person.phone ? <a href={`tel:${person.phone.replace(/\s/g, '')}`} className="btn-secondary flex-col gap-1 py-6 text-xs"><Phone className="h-4 w-4" />Ara</a> : <span className="btn-secondary pointer-events-none flex-col gap-1 py-6 text-xs opacity-50"><Phone className="h-4 w-4" />Ara</span>}
              <Link to={`/messages?u=${person.id}`} className="btn-primary flex-col gap-1 py-6 text-xs"><MessageSquare className="h-4 w-4" />Mesaj</Link>
            </div>
            <DefinitionList items={[
              { label: 'Departman', value: <span className="inline-flex items-center gap-1.5"><Building2 className="h-3.5 w-3.5" />{person.department?.name}</span> },
              { label: 'Yönetici', value: person.manager?.fullName ?? '—' }, { label: 'E-posta', value: person.email }, { label: 'Telefon', value: person.phone ?? '—' },
              { label: 'Lokasyon', value: <span className="inline-flex items-center gap-1.5"><MapPin className="h-3.5 w-3.5" />{person.location ?? '—'}</span> }, { label: 'İşe başlama', value: fmt.date(person.hireDate) },
            ]} />
          </div>
        )}
      </Drawer>
    </div>
  )
}
