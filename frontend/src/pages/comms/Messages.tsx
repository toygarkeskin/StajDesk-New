import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, MessagesSquare, Send } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, EmptyState, Spinner, UserPicker } from '@/components/ui'
import { api, errorMessage } from '@/lib/api'
import { fmt } from '@/lib/format'
import { usePollInterval } from '@/lib/realtime'
import type { Conversation, DirectoryEntry, Message } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useAuth } from '@/store/auth'

export default function Messages() {
  const me = useAuth((s) => s.user)!
  const qc = useQueryClient()
  const [sp, setSp] = useSearchParams()
  const other = sp.get('u')
  const poll = usePollInterval(10000)
  const convs = useQuery({ queryKey: ['conversations'], queryFn: () => api.get<Conversation[]>('/messages/conversations'), refetchInterval: poll })
  const thread = useQuery({ queryKey: ['thread', other], queryFn: () => api.get<Message[]>(`/messages/${other}`), enabled: !!other, refetchInterval: poll })
  const otherInfo = useQuery({
    queryKey: ['msg-user', other], enabled: !!other && !convs.data?.some((c) => c.user.id === other),
    queryFn: () => api.get<DirectoryEntry>(`/directory/${other}`),
  })
  const current = convs.data?.find((c) => c.user.id === other)
  const name = current?.user.fullName ?? otherInfo.data?.fullName ?? ''
  const [body, setBody] = useState('')
  // Yalnızca mesaj listesi kaydırılır (scrollIntoView tüm sayfayı da kaydırıyordu). Sohbet değişince anında, yeni mesajda yumuşak.
  const list = useRef<HTMLDivElement>(null)
  const lastOther = useRef<string | null>(null)
  useEffect(() => {
    const el = list.current
    if (!el || !thread.data) return
    const switched = lastOther.current !== other
    lastOther.current = other
    el.scrollTo({ top: el.scrollHeight, behavior: switched ? 'instant' : 'smooth' })
  }, [thread.data, other])
  useEffect(() => { if (thread.data) { qc.invalidateQueries({ queryKey: ['msg-count'] }); qc.invalidateQueries({ queryKey: ['conversations'] }) } }, [thread.data?.length]) // eslint-disable-line react-hooks/exhaustive-deps
  const send = useMutation({
    mutationFn: () => api.post<Message>('/messages', { recipientId: other, body }),
    onSuccess: () => { setBody(''); qc.invalidateQueries({ queryKey: ['thread', other] }); qc.invalidateQueries({ queryKey: ['conversations'] }) },
    onError: (e) => toast.error(errorMessage(e)),
  })

  return (
    <div className="card grid h-[calc(100dvh-9.5rem)] min-h-[520px] overflow-hidden md:grid-cols-[320px_1fr]">
      <aside className={cn('flex flex-col border-r border-ink-100 dark:border-white/[0.06]', other && 'hidden md:flex')}>
        <div className="border-b border-ink-100 p-4 dark:border-white/[0.06]">
          <h1 className="mb-3 text-lg font-bold">Mesajlar</h1>
          <UserPicker value={null} onChange={(u) => u && setSp({ u: u.id })} placeholder="Yeni sohbet: çalışan ara…" />
        </div>
        <ul className="min-h-0 flex-1 overflow-y-auto scroll-thin">
          {convs.data?.length === 0 && <EmptyState icon={<MessagesSquare className="h-5 w-5" />} title="Henüz sohbetiniz yok" />}
          {convs.data?.map((c) => (
            <li key={c.user.id}>
              <button onClick={() => setSp({ u: c.user.id })} className={cn('flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-ink-50 dark:hover:bg-white/[0.03]', other === c.user.id && 'bg-ink-50 dark:bg-white/[0.05]')}>
                <Avatar name={c.user.fullName} size={38} />
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-2"><span className="truncate text-sm font-semibold text-ink-900 dark:text-white">{c.user.fullName}</span><span className="shrink-0 text-[11px] muted">{fmt.smart(c.lastAt)}</span></span>
                  <span className="flex items-center gap-2"><span className={cn('truncate text-xs', c.unread ? 'font-semibold text-ink-800 dark:text-ink-100' : 'muted')}>{c.lastFromMe && 'Siz: '}{c.lastMessage}</span>{c.unread > 0 && <span className="ml-auto rounded-full bg-brand-600 px-1.5 text-[10px] font-bold leading-[18px] text-white">{c.unread}</span>}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </aside>
      <section className={cn('flex min-w-0 flex-col', !other && 'hidden md:flex')}>
        {!other ? <div className="grid flex-1 place-items-center"><EmptyState icon={<MessagesSquare className="h-5 w-5" />} title="Bir sohbet seçin" description="Soldan bir sohbet seçin veya yeni bir çalışan arayın." /></div> : (
          <>
            <header className="flex items-center gap-3 border-b border-ink-100 px-4 py-3 dark:border-white/[0.06]">
              <button className="btn-ghost btn-icon md:hidden" onClick={() => setSp({})} aria-label="Geri"><ArrowLeft className="h-4 w-4" /></button>
              {name && <Avatar name={name} size={34} />}<div><div className="font-semibold text-ink-900 dark:text-white">{name}</div><div className="text-xs muted">{current?.jobTitle ?? otherInfo.data?.jobTitle}</div></div>
            </header>
            <div ref={list} className="min-h-0 flex-1 space-y-2 overflow-y-auto bg-ink-50/50 p-4 scroll-thin dark:bg-transparent">
              {thread.isLoading && <div className="grid place-items-center p-6"><Spinner /></div>}
              {thread.data?.map((m) => {
                const mine = m.senderId === me.id
                return (
                  <div key={m.id} className={cn('flex', mine ? 'justify-end' : 'justify-start')}>
                    <div className={cn('max-w-[75%] rounded-2xl px-3.5 py-2 text-sm', mine ? 'rounded-br-md bg-brand-600 text-white' : 'rounded-bl-md bg-white text-ink-800 shadow-soft dark:bg-ink-800 dark:text-ink-100')}>
                      <p className="whitespace-pre-wrap">{m.body}</p>
                      <div className={cn('mt-0.5 text-right text-[10px]', mine ? 'text-white/70' : 'muted')}>{fmt.time(m.createdAt)}{mine && m.readAt && ' · okundu'}</div>
                    </div>
                  </div>
                )
              })}
            </div>
            <form className="flex gap-2 border-t border-ink-100 p-3 dark:border-white/[0.06]" onSubmit={(e) => { e.preventDefault(); if (body.trim()) send.mutate() }}>
              <input className="input" value={body} onChange={(e) => setBody(e.target.value)} placeholder="Mesaj yazın…" aria-label="Mesaj" maxLength={4000} />
              <button className="btn-primary" disabled={!body.trim() || send.isPending} aria-label="Gönder">{send.isPending ? <Spinner /> : <Send className="h-4 w-4" />}</button>
            </form>
          </>
        )}
      </section>
    </div>
  )
}
