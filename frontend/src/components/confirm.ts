import { createContext, useContext, type ReactNode } from 'react'

export type ConfirmOpts = { title: string; message: ReactNode; confirmText?: string; danger?: boolean; input?: { label: string; required?: boolean; placeholder?: string } }
export const ConfirmCtx = createContext<(o: ConfirmOpts) => Promise<string | false>>(async () => false)
/** Onay diyaloğu: onaylanırsa girilen metni (veya boş metni), vazgeçilirse false döner. */
export const useConfirm = () => useContext(ConfirmCtx)
