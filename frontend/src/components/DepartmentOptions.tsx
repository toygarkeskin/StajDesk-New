import type { Department } from '@/lib/types'

/** Merkez departmanlar önce, mağazalar sonra; <select> için gruplanmış seçenekler */
export function DepartmentOptions({ departments, includeStores = true }: { departments?: Department[]; includeStores?: boolean }) {
  const hq = departments?.filter((d) => d.type === 'HeadOffice') ?? []
  const region = departments?.filter((d) => d.type === 'Region') ?? []
  const stores = departments?.filter((d) => d.type === 'Store') ?? []
  return (
    <>
      <optgroup label="Genel Müdürlük">{hq.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</optgroup>
      {includeStores && <optgroup label="Bölge Yönetimi">{region.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</optgroup>}
      {includeStores && <optgroup label="Mağazalar">{stores.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</optgroup>}
    </>
  )
}
