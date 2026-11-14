import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Crown, Headset, Plus, Trash2, X } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Avatar, Badge, Drawer, EmptyState, Field, PageHeader, QueryState, Spinner, Toggle, UserPicker } from '@/components/ui'
import { useConfirm } from '@/components/confirm'
import { api, errorMessage } from '@/lib/api'
import { useDepartments, useGroups } from '@/lib/queries'
import { DepartmentOptions } from '@/components/DepartmentOptions'
import type { Group } from '@/lib/types'

function GroupDrawer({ group, onClose }: { group: Group | null; onClose: () => void }) {
  const qc = useQueryClient()
  const confirm = useConfirm()
  const departments = useDepartments()
  const [v, setV] = useState({ name: group?.name ?? '', description: group?.description ?? '', departmentId: group?.department?.id?.toString() ?? '', isSupportGroup: group?.isSupportGroup ?? true })
  const [members, setMembers] = useState(group?.members.map((m) => ({ id: m.userId, fullName: m.fullName, isLead: m.isLead })) ?? [])
  const save = useMutation({
    mutationFn: () => { const b = { ...v, departmentId: v.departmentId ? Number(v.departmentId) : null, memberIds: members.map((m) => m.id), leadIds: members.filter((m) => m.isLead).map((m) => m.id) }; return group ? api.put(`/org/groups/${group.id}`, b) : api.post('/org/groups', b) },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['groups'] }); toast.success('Grup kaydedildi.'); onClose() },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const del = useMutation({ mutationFn: () => api.del(`/org/groups/${group!.id}`), onSuccess: () => { qc.invalidateQueries({ queryKey: ['groups'] }); onClose() }, onError: (e) => toast.error(errorMessage(e)) })
  return (
    <Drawer open onClose={onClose} title={group?.name ?? 'Yeni grup'} footer={<>{group && <button className="btn-ghost mr-auto text-red-600" onClick={async () => (await confirm({ title: 'Grubu sil', message: group.name, danger: true, confirmText: 'Sil' })) && del.mutate()}><Trash2 className="h-4 w-4" /> Sil</button>}<button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-primary" disabled={!v.name.trim() || save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <div className="space-y-4">
        <Field label="Grup adı" required><input className="input" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} /></Field>
        <Field label="Açıklama"><input className="input" value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} /></Field>
        <Field label="Bağlı departman"><select className="input" value={v.departmentId} onChange={(e) => setV({ ...v, departmentId: e.target.value })}><option value="">Seçilmedi</option><DepartmentOptions departments={departments.data} includeStores={false} /></select></Field>
        <Toggle checked={v.isSupportGroup} onChange={(x) => setV({ ...v, isSupportGroup: x })} label="Destek grubu (talepler atanabilir)" />
        <Field label="Üyeler" help="Talep atanabilmesi için üyenin 'tickets.manage' yetkisi olmalıdır.">
          <UserPicker value={null} onChange={(u) => u && !members.some((m) => m.id === u.id) && setMembers([...members, { ...u, isLead: false }])} placeholder="Üye ekle…" />
        </Field>
        <ul className="space-y-1.5">
          {members.map((m) => (
            <li key={m.id} className="flex items-center gap-3 rounded-lg border border-ink-200 px-3 py-2 dark:border-white/10">
              <Avatar name={m.fullName} size={26} /><span className="flex-1 text-sm">{m.fullName}</span>
              <button className={m.isLead ? 'btn-sm btn text-amber-600' : 'btn-ghost btn-sm'} onClick={() => setMembers(members.map((x) => (x.id === m.id ? { ...x, isLead: !x.isLead } : x)))} aria-label="Ekip lideri"><Crown className="h-3.5 w-3.5" /> {m.isLead ? 'Lider' : ''}</button>
              <button className="btn-ghost btn-icon" onClick={() => setMembers(members.filter((x) => x.id !== m.id))} aria-label="Çıkar"><X className="h-4 w-4" /></button>
            </li>
          ))}
        </ul>
      </div>
    </Drawer>
  )
}

export default function Groups() {
  const groups = useGroups()
  const [edit, setEdit] = useState<Group | null | undefined>(undefined)
  return (
    <div>
      <PageHeader title="Destek Grupları (Agent Management)" description="Taleplerin yönlendirildiği ekipler ve üyeleri." actions={<button className="btn-primary" onClick={() => setEdit(null)}><Plus className="h-4 w-4" /> Grup</button>} />
      <QueryState query={groups} empty={(d) => d.length === 0 && <div className="card"><EmptyState title="Grup yok" /></div>}>
        {(list) => (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {list.map((g) => (
              <button key={g.id} onClick={() => setEdit(g)} className="card p-5 text-left transition hover:shadow-pop">
                <div className="flex items-start justify-between gap-2"><div className="flex items-center gap-2"><Headset className="h-4 w-4 text-brand-600" /><span className="font-semibold text-ink-900 dark:text-white">{g.name}</span></div>{g.isSupportGroup && <Badge tone="brand">Destek</Badge>}</div>
                <p className="mt-1 text-sm muted">{g.description}</p>
                <div className="mt-4 flex items-center justify-between"><div className="flex -space-x-2">{g.members.slice(0, 6).map((m) => <Avatar key={m.userId} name={m.fullName} size={28} />)}</div><span className="text-xs muted">{g.members.length} üye · {g.openTicketCount} açık talep</span></div>
              </button>
            ))}
          </div>
        )}
      </QueryState>
      {edit !== undefined && <GroupDrawer group={edit} onClose={() => setEdit(undefined)} />}
    </div>
  )
}
