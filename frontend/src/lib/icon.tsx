import { icons, type LucideProps } from 'lucide-react'

/** Backend'den gelen ikon adını (ör. "CalendarDays") lucide bileşenine çevirir. */
export function DynamicIcon({ name, ...props }: { name: string } & LucideProps) {
  const Cmp = (icons as Record<string, React.ComponentType<LucideProps>>)[name] ?? icons.FileText
  return <Cmp {...props} />
}
