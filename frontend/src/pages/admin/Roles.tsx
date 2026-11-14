import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock, Plus, Save } from 'lucide-react'
import { Fragment, useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge, Field, Modal, PageHeader, QueryState, Spinner } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { useRoles } from '@/lib/queries'
import type { PermissionDef, Role } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCan } from '@/store/auth'

export default function Roles() {
  const { can } = useCan()
  const editable = can('roles.manage')
  const qc = useQueryClient()
  const roles = useRoles()
  const perms = useQuery({ queryKey: ['permissions'], queryFn: () => api.get<PermissionDef[]>('/org/permissions') })
  const [draft, setDraft] = useState<Record<string, string[]>>({})
  const [open, setOpen] = useState(false)
  const [nr, setNr] = useState({ code: '', name: '', description: '' })
  const groups = useMemo(() => Object.entries((perms.data ?? []).reduce<Record<string, PermissionDef[]>>((a, p) => ((a[p.group] ??= []).push(p), a), {})), [perms.data])
  const get = (r: Role) => draft[r.code] ?? r.permissions
  const toggle = (r: Role, p: string) => setDraft({ ...draft, [r.code]: get(r).includes(p) ? get(r).filter((x) => x !== p) : [...get(r), p] })
  const save = useMutation({
    mutationFn: (r: Role) => api.put(`/org/roles/${r.id}`, { code: r.code, name: r.name, description: r.description, permissions: get(r) }),
    onSuccess: (_, r) => { qc.invalidateQueries({ queryKey: ['roles'] }); setDraft((d) => { const n = { ...d }; delete n[r.code]; return n }); toast.success(`${r.name} rolü güncellendi. Değişiklik kullanıcıların bir sonraki oturum yenilemesinde geçerli olur.`) },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const create = useMutation({ mutationFn: () => api.post('/org/roles', { ...nr, permissions: [] }), onSuccess: () => { qc.invalidateQueries({ queryKey: ['roles'] }); setOpen(false); toast.success('Rol oluşturuldu.') }, onError: (e) => toast.error(errorMessage(e)) })

  return (
    <div>
      <PageHeader title="Roller & Yetkiler" description="Sistem rolleri ve izin matrisi. Yetki kontrolleri her istekte sunucu tarafında yapılır." actions={editable && <button className="btn-primary" onClick={() => setOpen(true)}><Plus className="h-4 w-4" /> Rol</button>} />
      <QueryState query={roles}>
        {(list) => (
          <div className="card overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[1100px] text-sm">
                <thead className="sticky top-0 bg-ink-50 dark:bg-ink-850">
                  <tr>
                    <th className="th sticky left-0 min-w-64 bg-ink-50 dark:bg-ink-850">İzin</th>
                    {list.map((r) => (
                      <th key={r.code} className="th text-center normal-case">
                        <div className="text-[12px] font-semibold text-ink-900 dark:text-white">{r.name}</div>
                        <div className="text-[10px] font-normal muted">{r.userCount} kişi</div>
                        {draft[r.code] && editable && <button className="btn-primary btn-sm mt-1 h-6" disabled={save.isPending} onClick={() => save.mutate(r)}>{save.isPending ? <Spinner /> : <Save className="h-3 w-3" />} Kaydet</button>}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {groups.map(([g, ps]) => (
                    <Fragment key={g}>
                      <tr><td colSpan={list.length + 1} className="bg-ink-50/60 px-4 py-1.5 text-[11px] font-semibold uppercase tracking-wider muted dark:bg-white/[0.02]">{g}</td></tr>
                      {ps.map((p) => (
                        <tr key={p.code} className="tr">
                          <td className="td sticky left-0 bg-white dark:bg-ink-900"><div className="font-medium text-ink-900 dark:text-white">{p.label}</div><div className="font-mono text-[10px] muted">{p.code}</div></td>
                          {list.map((r) => {
                            const locked = !editable || r.code === 'SUPER_ADMIN'
                            const on = get(r).includes(p.code)
                            return (
                              <td key={r.code} className="td text-center">
                                <button disabled={locked} onClick={() => toggle(r, p.code)} aria-label={`${r.name}: ${p.label}`} aria-pressed={on}
                                  className={cn('mx-auto grid h-6 w-6 place-items-center rounded-md border transition', on ? 'border-brand-500 bg-brand-600 text-white' : 'border-ink-200 dark:border-white/15', locked ? 'cursor-not-allowed opacity-70' : 'hover:border-brand-400')}>
                                  {on && <svg viewBox="0 0 12 12" className="h-3 w-3"><path d="M2.5 6.5l2.2 2.2 4.8-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" /></svg>}
                                </button>
                              </td>
                            )
                          })}
                        </tr>
                      ))}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex flex-wrap gap-3 border-t border-ink-100 px-5 py-3 text-xs muted dark:border-white/[0.06]"><span className="inline-flex items-center gap-1"><Lock className="h-3 w-3" /> Super Admin tüm izinlere sahiptir ve değiştirilemez.</span>{!editable && <Badge tone="amber">Görüntüleme modu (roles.manage gerekli)</Badge>}</div>
          </div>
        )}
      </QueryState>
      <Modal open={open} onClose={() => setOpen(false)} title="Yeni rol" footer={<><button className="btn-secondary" onClick={() => setOpen(false)}>Vazgeç</button><button className="btn-primary" disabled={create.isPending} onClick={() => create.mutate()}>{create.isPending && <Spinner />} Oluştur</button></>}>
        <div className="space-y-4">
          <Field label="Kod" required help="Büyük harf ve alt çizgi (ör. BOLGE_MUDURU)"><input className="input font-mono uppercase" value={nr.code} onChange={(e) => setNr({ ...nr, code: e.target.value.toUpperCase() })} /></Field>
          <Field label="Ad" required><input className="input" value={nr.name} onChange={(e) => setNr({ ...nr, name: e.target.value })} /></Field>
          <Field label="Açıklama"><input className="input" value={nr.description} onChange={(e) => setNr({ ...nr, description: e.target.value })} /></Field>
        </div>
      </Modal>
    </div>
  )
}
